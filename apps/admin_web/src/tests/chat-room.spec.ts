// @vitest-environment jsdom

import { flushPromises, mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import ChatRoomView from "../features/consultations/ChatRoomView.vue";
import {
  ChatConnectionController,
  type ChatHistoryApi,
  type ChatMessage,
  type ChatPresence,
  type ChatTransport,
} from "../features/consultations/chatConnection";

describe("ChatRoomView", () => {
  it("backfills, sends, and merges repeated realtime messages once", async () => {
    const history = new FakeHistoryApi();
    const transport = new FakeTransport();
    const connection = new ChatConnectionController(history, transport);
    const wrapper = mount(ChatRoomView, { props: { connection } });

    await wrapper.get('[data-testid="session-id"]').setValue("session-1");
    await wrapper.get("form.chat-connect").trigger("submit");
    await flushPromises();
    expect(wrapper.text()).toContain("历史合成消息");

    transport.emitMessage(history.firstMessage);
    await wrapper.vm.$nextTick();
    expect(wrapper.findAll('[data-testid="chat-message"]')).toHaveLength(1);

    await wrapper.get('[data-testid="chat-input"]').setValue("网页合成消息");
    await wrapper.get("form.chat-composer").trigger("submit");
    await flushPromises();
    expect(transport.sentTexts).toEqual(["网页合成消息"]);
    expect(wrapper.text()).toContain("网页合成消息");

    transport.beginReconnect();
    transport.finishReconnect();
    await flushPromises();
    expect(history.afterSequences).toEqual([0, 2]);
    expect(wrapper.text()).toContain("重连补回消息");
  });

  it("disconnects and removes old messages before switching sessions", async () => {
    const history = new FakeHistoryApi();
    const transport = new FakeTransport();
    const connection = new ChatConnectionController(history, transport);

    await connection.connect("session-1");
    expect(connection.snapshot.messages[0]?.sessionId).toBe("session-1");

    await connection.connect("session-2");

    expect(transport.stopCount).toBe(1);
    expect(connection.snapshot.messages).toHaveLength(1);
    expect(connection.snapshot.messages[0]?.sessionId).toBe("session-2");
  });
});

class FakeHistoryApi implements ChatHistoryApi {
  readonly afterSequences: number[] = [];
  readonly firstMessage: ChatMessage = {
    id: "history-session-1",
    sessionId: "session-1",
    senderKind: "User",
    clientMessageId: "client-1",
    sequence: 1,
    text: "历史合成消息",
    sentAt: "2026-08-22T12:01:00Z",
  };

  async getMessages(
    sessionId: string,
    afterSequence: number,
  ): Promise<ChatMessage[]> {
    this.afterSequences.push(afterSequence);
    return afterSequence === 0
      ? [
          {
            ...this.firstMessage,
            id: `history-${sessionId}`,
            sessionId,
          },
        ]
      : [
          {
            id: "history-3",
            sessionId: "session-1",
            senderKind: "Practitioner",
            clientMessageId: "client-3",
            sequence: 3,
            text: "重连补回消息",
            sentAt: "2026-08-22T12:03:00Z",
          },
        ];
  }
}

class FakeTransport implements ChatTransport {
  readonly sentTexts: string[] = [];
  stopCount = 0;
  private messageHandler: (message: ChatMessage) => void = () => undefined;
  private reconnectingHandler: () => void = () => undefined;
  private reconnectedHandler: () => Promise<void> = async () => undefined;

  onMessage(handler: (message: ChatMessage) => void): void {
    this.messageHandler = handler;
  }

  onPresence(_handler: (presence: ChatPresence) => void): void {}

  onReconnecting(handler: () => void): void {
    this.reconnectingHandler = handler;
  }

  onReconnected(handler: () => Promise<void>): void {
    this.reconnectedHandler = handler;
  }

  onClosed(_handler: () => void): void {}

  async start(): Promise<void> {}

  async stop(): Promise<void> {
    this.stopCount += 1;
  }

  async join(_sessionId: string): Promise<void> {}

  async send(
    sessionId: string,
    text: string,
    clientMessageId: string,
  ): Promise<ChatMessage> {
    this.sentTexts.push(text);
    return {
      id: "sent-2",
      sessionId,
      senderKind: "Practitioner",
      clientMessageId,
      sequence: 2,
      text,
      sentAt: "2026-08-22T12:02:00Z",
    };
  }

  emitMessage(message: ChatMessage): void {
    this.messageHandler(message);
  }

  beginReconnect(): void {
    this.reconnectingHandler();
  }

  finishReconnect(): void {
    void this.reconnectedHandler();
  }
}
