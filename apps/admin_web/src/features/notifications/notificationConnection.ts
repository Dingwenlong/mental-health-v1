import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import { tokenStore } from "../../api/client";

export type ClinicalNotification =
  | {
      kind: "analysis";
      sessionId: string;
      status: string;
      transcriptRevision: number | null;
    }
  | {
      kind: "risk-case";
      caseId: string;
      currentLevel: string;
      status: string;
    }
  | {
      kind: "follow-up";
      taskId: string;
      status: string;
      dueAt: string | null;
      conflictCode: string | null;
    };

export interface ClinicalNotificationSource {
  subscribe(listener: (notification: ClinicalNotification) => void): () => void;
}

class NotificationConnection implements ClinicalNotificationSource {
  private readonly connection: HubConnection;
  private readonly listeners = new Set<
    (notification: ClinicalNotification) => void
  >();
  private startPromise: Promise<void> | null = null;

  constructor() {
    const baseUrl =
      import.meta.env.VITE_NOTIFICATION_HUB_URL ??
      "http://127.0.0.1:5165/hubs/notifications";
    this.connection = new HubConnectionBuilder()
      .withUrl(baseUrl, { accessTokenFactory: () => tokenStore.read() ?? "" })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on("AnalysisStatusChanged", (value) => {
      this.publish({ kind: "analysis", ...value });
    });
    this.connection.on("RiskCaseChanged", (value) => {
      this.publish({ kind: "risk-case", ...value });
    });
    this.connection.on("FollowUpChanged", (value) => {
      this.publish({ kind: "follow-up", ...value });
    });
  }

  subscribe(
    listener: (notification: ClinicalNotification) => void,
  ): () => void {
    this.listeners.add(listener);
    void this.ensureStarted();
    return () => this.listeners.delete(listener);
  }

  async stop(): Promise<void> {
    if (this.connection.state !== HubConnectionState.Disconnected) {
      await this.connection.stop();
    }
  }

  private publish(notification: ClinicalNotification): void {
    for (const listener of this.listeners) listener(notification);
  }

  private async ensureStarted(): Promise<void> {
    if (this.connection.state !== HubConnectionState.Disconnected) return;
    if (!this.startPromise) {
      this.startPromise = this.connection.start().finally(() => {
        this.startPromise = null;
      });
    }
    try {
      await this.startPromise;
    } catch {
      // The page remains usable with manual refresh when the local API is not running.
    }
  }
}

export const notificationConnection = new NotificationConnection();
