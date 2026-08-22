import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import { tokenStore } from "../../api/client";

export type RtcState = "idle" | "signaling" | "connected" | "failed" | "closed";

export type RtcSnapshot = {
  state: RtcState;
  roomJoined?: boolean;
  localStream?: MediaStream;
  remoteStream?: MediaStream;
};

export interface BrowserRtcPeer {
  readonly snapshot: RtcSnapshot;
  subscribe(listener: (snapshot: RtcSnapshot) => void): () => void;
  start(sessionId: string): Promise<void>;
  close(): Promise<void>;
}

type DescriptionEnvelope = { sessionId: string; sdp: string };
type IceCandidateEnvelope = {
  sessionId: string;
  candidate: string;
  sdpMid?: string | null;
  sdpMLineIndex?: number | null;
};

export class SignalRBrowserRtcPeer implements BrowserRtcPeer {
  private readonly hubUrl: string;
  private readonly listeners = new Set<(snapshot: RtcSnapshot) => void>();
  private readonly pendingCandidates: RTCIceCandidateInit[] = [];
  private connection: HubConnection | null = null;
  private peer: RTCPeerConnection | null = null;
  private sessionId: string | null = null;
  private current: RtcSnapshot = { state: "idle" };
  private closing = false;
  private releasePromise: Promise<void> | null = null;
  private generation = 0;

  constructor(hubUrl: string) {
    this.hubUrl = hubUrl;
  }

  get snapshot(): RtcSnapshot {
    return this.current;
  }

  subscribe(listener: (snapshot: RtcSnapshot) => void): () => void {
    this.listeners.add(listener);
    listener(this.snapshot);
    return () => this.listeners.delete(listener);
  }

  async start(sessionId: string): Promise<void> {
    const normalized = sessionId.trim();
    if (!normalized) throw new Error("Session id is required.");

    const generation = ++this.generation;
    await this.release(false);
    if (generation !== this.generation) return;
    this.closing = false;
    this.sessionId = normalized;
    this.update({ state: "signaling" });

    try {
      const localStream = await navigator.mediaDevices.getUserMedia({
        audio: true,
        video: { facingMode: "user" },
      });
      if (generation !== this.generation) {
        localStream.getTracks().forEach((track) => track.stop());
        return;
      }
      this.update({ state: "signaling", localStream });
      const peer = new RTCPeerConnection({ iceServers: [] });
      this.peer = peer;
      for (const track of localStream.getTracks()) {
        peer.addTrack(track, localStream);
      }
      peer.ontrack = (event) => {
        if (generation !== this.generation) return;
        const remoteStream = event.streams[0] ?? new MediaStream([event.track]);
        this.update({ ...this.current, remoteStream });
      };
      peer.onicecandidate = (event) => {
        if (
          generation !== this.generation ||
          !event.candidate ||
          !this.connection ||
          !this.sessionId
        )
          return;
        void this.connection
          .invoke(
            "RelayIceCandidate",
            this.sessionId,
            event.candidate.candidate,
            event.candidate.sdpMid,
            event.candidate.sdpMLineIndex,
          )
          .catch(() => this.fail(generation));
      };
      peer.onconnectionstatechange = () => {
        if (generation !== this.generation) return;
        if (peer.connectionState === "connected") {
          this.update({ ...this.current, state: "connected" });
        } else if (
          !this.closing &&
          (peer.connectionState === "failed" ||
            peer.connectionState === "disconnected")
        ) {
          this.fail(generation);
        }
      };

      const connection = new HubConnectionBuilder()
        .withUrl(this.hubUrl, {
          accessTokenFactory: () => tokenStore.read() ?? "",
          withCredentials: false,
        })
        .configureLogging(LogLevel.Warning)
        .build();
      this.connection = connection;
      connection.on("OfferReceived", (description: DescriptionEnvelope) => {
        if (generation !== this.generation) return;
        void this.answerOffer(description).catch(() => this.fail(generation));
      });
      connection.on(
        "IceCandidateReceived",
        (candidate: IceCandidateEnvelope) => {
          if (generation !== this.generation) return;
          void this.acceptCandidate(candidate).catch(() =>
            this.fail(generation),
          );
        },
      );
      connection.onclose(() => {
        if (!this.closing) this.fail(generation);
      });

      await connection.start();
      if (generation !== this.generation) return;
      await connection.invoke("JoinRoom", normalized);
      if (generation !== this.generation) return;
      this.update({ ...this.current, roomJoined: true });
      await this.waitForConnected(generation);
    } catch {
      if (generation === this.generation) {
        await this.release(false);
        if (!this.closing) this.update({ state: "failed" });
      }
    }
  }

  async close(): Promise<void> {
    this.closing = true;
    this.generation += 1;
    this.update({ state: "closed" });
    await this.release(true);
  }

  private async answerOffer(description: DescriptionEnvelope): Promise<void> {
    if (!this.peer || description.sessionId !== this.sessionId) return;
    await this.peer.setRemoteDescription({
      type: "offer",
      sdp: description.sdp,
    });
    while (this.pendingCandidates.length) {
      await this.peer.addIceCandidate(this.pendingCandidates.shift());
    }
    const answer = await this.peer.createAnswer();
    await this.peer.setLocalDescription(answer);
    if (!answer.sdp || !this.connection || !this.sessionId) {
      throw new Error("RTC answer is missing.");
    }
    await this.connection.invoke("RelayAnswer", this.sessionId, answer.sdp);
  }

  private async acceptCandidate(
    candidate: IceCandidateEnvelope,
  ): Promise<void> {
    if (!this.peer || candidate.sessionId !== this.sessionId) return;
    const value: RTCIceCandidateInit = {
      candidate: candidate.candidate,
      sdpMid: candidate.sdpMid ?? null,
      sdpMLineIndex: candidate.sdpMLineIndex ?? null,
    };
    if (!this.peer.remoteDescription) {
      this.pendingCandidates.push(value);
      return;
    }
    await this.peer.addIceCandidate(value);
  }

  private waitForConnected(generation: number): Promise<void> {
    return new Promise((resolve, reject) => {
      const startedAt = Date.now();
      const timer = window.setInterval(() => {
        if (this.current.state === "connected") {
          window.clearInterval(timer);
          resolve();
        } else if (
          this.current.state === "failed" ||
          this.current.state === "closed" ||
          generation !== this.generation ||
          Date.now() - startedAt >= 15_000
        ) {
          window.clearInterval(timer);
          reject(new Error("RTC connection timed out."));
        }
      }, 100);
    });
  }

  private fail(generation: number): void {
    if (
      generation !== this.generation ||
      this.closing ||
      this.current.state === "failed"
    )
      return;
    this.update({ ...this.current, state: "failed" });
    void this.release(false).catch(() => undefined);
  }

  private release(leaveRoom: boolean): Promise<void> {
    if (this.releasePromise) return this.releasePromise;
    const operation = this.releaseResources(leaveRoom).finally(() => {
      this.releasePromise = null;
    });
    this.releasePromise = operation;
    return operation;
  }

  private async releaseResources(leaveRoom: boolean): Promise<void> {
    const connection = this.connection;
    const peer = this.peer;
    const sessionId = this.sessionId;
    const localStream = this.current.localStream;
    const remoteStream = this.current.remoteStream;
    this.connection = null;
    this.peer = null;
    this.sessionId = null;
    this.pendingCandidates.length = 0;

    if (
      leaveRoom &&
      connection?.state === HubConnectionState.Connected &&
      sessionId
    ) {
      try {
        await connection.invoke("LeaveRoom", sessionId);
      } catch {
        // The hub may already be gone. Local tracks still have to stop.
      }
    }
    if (connection) {
      try {
        await connection.stop();
      } catch {
        // The local WebRTC resources below must still be released.
      }
    }
    try {
      peer?.close();
    } catch {
      // Continue stopping media tracks.
    }
    localStream?.getTracks().forEach((track) => track.stop());
    remoteStream?.getTracks().forEach((track) => track.stop());
    this.update({ state: this.current.state });
  }

  private update(snapshot: RtcSnapshot): void {
    this.current = snapshot;
    for (const listener of this.listeners) listener(this.snapshot);
  }
}

export function createBrowserRtcPeer(): BrowserRtcPeer {
  return new SignalRBrowserRtcPeer(
    import.meta.env.VITE_RTC_HUB_URL ?? "http://127.0.0.1:5165/hubs/rtc",
  );
}
