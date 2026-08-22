import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import { apiClient, tokenStore } from "../../api/client";

export type ChatMessage = {
  id: string;
  sessionId: string;
  senderKind: "User" | "Practitioner";
  clientMessageId: string;
  sequence: number;
  text: string;
  sentAt: string;
};

export type ChatPresence = {
  userId: string;
  kind: "User" | "Practitioner";
  online: boolean;
};

export type ChatConnectionStatus =
  "disconnected" | "connecting" | "connected" | "reconnecting";

export type ChatSnapshot = {
  status: ChatConnectionStatus;
  messages: ChatMessage[];
  presences: ChatPresence[];
};

export interface ChatHistoryApi {
  getMessages(sessionId: string, afterSequence: number): Promise<ChatMessage[]>;
}

export interface ChatTransport {
  onMessage(handler: (message: ChatMessage) => void): void;
  onPresence(handler: (presence: ChatPresence) => void): void;
  onReconnecting(handler: () => void): void;
  onReconnected(handler: () => Promise<void>): void;
  onClosed(handler: () => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
  join(sessionId: string): Promise<void>;
  send(
    sessionId: string,
    text: string,
    clientMessageId: string,
  ): Promise<ChatMessage>;
}

export interface ChatConnection {
  readonly snapshot: ChatSnapshot;
  subscribe(listener: (snapshot: ChatSnapshot) => void): () => void;
  connect(sessionId: string): Promise<void>;
  disconnect(): Promise<void>;
  send(text: string, clientMessageId: string): Promise<void>;
}

export class ChatConnectionController implements ChatConnection {
  private readonly messages = new Map<string, ChatMessage>();
  private readonly presences = new Map<string, ChatPresence>();
  private readonly listeners = new Set<(snapshot: ChatSnapshot) => void>();
  private readonly history: ChatHistoryApi;
  private readonly transport: ChatTransport;
  private status: ChatConnectionStatus = "disconnected";
  private sessionId: string | null = null;

  constructor(history: ChatHistoryApi, transport: ChatTransport) {
    this.history = history;
    this.transport = transport;
    transport.onMessage((message) => this.mergeMessage(message));
    transport.onPresence((presence) => {
      this.presences.set(presence.userId, presence);
      this.notify();
    });
    transport.onReconnecting(() => {
      this.status = "reconnecting";
      this.notify();
    });
    transport.onReconnected(async () => this.afterReconnect());
    transport.onClosed(() => {
      this.status = "disconnected";
      this.notify();
    });
  }

  get snapshot(): ChatSnapshot {
    return {
      status: this.status,
      messages: [...this.messages.values()].sort(
        (left, right) => left.sequence - right.sequence,
      ),
      presences: [...this.presences.values()],
    };
  }

  subscribe(listener: (snapshot: ChatSnapshot) => void): () => void {
    this.listeners.add(listener);
    listener(this.snapshot);
    return () => this.listeners.delete(listener);
  }

  async connect(sessionId: string): Promise<void> {
    const normalized = sessionId.trim();
    if (!normalized) throw new Error("Session id is required.");
    if (this.status === "connected" && this.sessionId === normalized) return;
    if (this.sessionId !== null && this.sessionId !== normalized) {
      this.sessionId = null;
      this.messages.clear();
      this.presences.clear();
      this.status = "disconnected";
      this.notify();
      await this.transport.stop();
    }
    this.sessionId = normalized;
    this.status = "connecting";
    this.notify();
    try {
      await this.transport.start();
      await this.transport.join(normalized);
      await this.backfill();
      this.status = "connected";
      this.notify();
    } catch (error) {
      this.status = "disconnected";
      this.notify();
      throw error;
    }
  }

  async disconnect(): Promise<void> {
    this.sessionId = null;
    await this.transport.stop();
    this.status = "disconnected";
    this.notify();
  }

  async send(text: string, clientMessageId: string): Promise<void> {
    const normalized = text.trim();
    if (!this.sessionId || this.status !== "connected" || !normalized) {
      throw new Error("Chat is not ready.");
    }
    const message = await this.transport.send(
      this.sessionId,
      normalized,
      clientMessageId,
    );
    this.mergeMessage(message);
  }

  private async afterReconnect(): Promise<void> {
    if (!this.sessionId) return;
    try {
      await this.transport.join(this.sessionId);
      await this.backfill();
      this.status = "connected";
      this.notify();
    } catch {
      this.status = "disconnected";
      this.notify();
    }
  }

  private async backfill(): Promise<void> {
    if (!this.sessionId) return;
    const lastSequence = [...this.messages.values()].reduce(
      (current, message) => Math.max(current, message.sequence),
      0,
    );
    const missed = await this.history.getMessages(this.sessionId, lastSequence);
    let changed = false;
    for (const message of missed) {
      if (this.messages.has(message.id)) continue;
      this.messages.set(message.id, message);
      changed = true;
    }
    if (changed) this.notify();
  }

  private mergeMessage(message: ChatMessage): void {
    if (this.messages.has(message.id)) return;
    this.messages.set(message.id, message);
    this.notify();
  }

  private notify(): void {
    const snapshot = this.snapshot;
    for (const listener of this.listeners) listener(snapshot);
  }
}

class ApiChatHistory implements ChatHistoryApi {
  getMessages(
    sessionId: string,
    afterSequence: number,
  ): Promise<ChatMessage[]> {
    return apiClient.get<ChatMessage[]>(
      `consultations/${sessionId}/messages?afterSequence=${afterSequence}`,
    );
  }
}

class SignalRChatTransport implements ChatTransport {
  private readonly connection: HubConnection;

  constructor(hubUrl: string) {
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => tokenStore.read() ?? "",
        withCredentials: false,
      })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000])
      .configureLogging(LogLevel.Warning)
      .build();
  }

  onMessage(handler: (message: ChatMessage) => void): void {
    this.connection.on("MessageReceived", handler);
  }

  onPresence(handler: (presence: ChatPresence) => void): void {
    this.connection.on("PresenceChanged", handler);
  }

  onReconnecting(handler: () => void): void {
    this.connection.onreconnecting(handler);
  }

  onReconnected(handler: () => Promise<void>): void {
    this.connection.onreconnected(() => void handler());
  }

  onClosed(handler: () => void): void {
    this.connection.onclose(handler);
  }

  async start(): Promise<void> {
    if (this.connection.state === HubConnectionState.Disconnected) {
      await this.connection.start();
    }
  }

  async stop(): Promise<void> {
    await this.connection.stop();
  }

  async join(sessionId: string): Promise<void> {
    await this.connection.invoke("JoinSession", sessionId);
  }

  send(
    sessionId: string,
    text: string,
    clientMessageId: string,
  ): Promise<ChatMessage> {
    return this.connection.invoke<ChatMessage>(
      "SendMessage",
      sessionId,
      text,
      clientMessageId,
    );
  }
}

export function createChatConnection(): ChatConnection {
  const hubUrl =
    import.meta.env.VITE_CHAT_HUB_URL ?? "http://127.0.0.1:5165/hubs/chat";
  return new ChatConnectionController(
    new ApiChatHistory(),
    new SignalRChatTransport(hubUrl),
  );
}
