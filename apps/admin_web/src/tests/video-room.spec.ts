// @vitest-environment jsdom

import { flushPromises, mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import VideoRoomView from "../features/consultations/VideoRoomView.vue";
import {
  SignalRBrowserRtcPeer,
  type BrowserRtcPeer,
  type RtcSnapshot,
} from "../features/consultations/browserRtcPeer";

describe("VideoRoomView", () => {
  it("shows failed instead of connected when the peer connection fails", async () => {
    const peer = new FakeRtcPeer("failed");
    const wrapper = mount(VideoRoomView, { props: { peer } });

    await wrapper.get('[data-testid="video-session-id"]').setValue("session-1");
    await wrapper.get("form.video-connect").trigger("submit");
    await flushPromises();

    expect(wrapper.get('[data-testid="rtc-status"]').text()).toBe(
      "视频没有连上",
    );
    expect(wrapper.text()).not.toContain("视频已连接");
    wrapper.unmount();
    expect(peer.closeCount).toBe(1);
  });

  it("shows connected only after the peer reports connected", async () => {
    const peer = new FakeRtcPeer("connected");
    const wrapper = mount(VideoRoomView, { props: { peer } });

    await wrapper.get('[data-testid="video-session-id"]').setValue("session-1");
    await wrapper.get("form.video-connect").trigger("submit");
    await flushPromises();

    expect(wrapper.get('[data-testid="rtc-status"]').text()).toBe("视频已连接");
    wrapper.unmount();
  });

  it("stops a camera stream that resolves after the room is closed", async () => {
    let resolveCapture!: (stream: MediaStream) => void;
    const capture = new Promise<MediaStream>((resolve) => {
      resolveCapture = resolve;
    });
    const stop = vi.fn();
    const getUserMedia = vi.fn(() => capture);
    const originalMediaDevices = navigator.mediaDevices;
    Object.defineProperty(navigator, "mediaDevices", {
      configurable: true,
      value: { getUserMedia },
    });

    try {
      const peer = new SignalRBrowserRtcPeer("https://localhost/hubs/rtc");
      const starting = peer.start("session-1");
      await vi.waitFor(() => expect(getUserMedia).toHaveBeenCalledOnce());
      const closing = peer.close();
      resolveCapture({ getTracks: () => [{ stop }] } as unknown as MediaStream);

      await Promise.all([starting, closing]);
      expect(stop).toHaveBeenCalledOnce();
      expect(peer.snapshot.state).toBe("closed");
    } finally {
      Object.defineProperty(navigator, "mediaDevices", {
        configurable: true,
        value: originalMediaDevices,
      });
    }
  });
});

class FakeRtcPeer implements BrowserRtcPeer {
  private readonly listeners = new Set<(snapshot: RtcSnapshot) => void>();
  readonly finalState: RtcSnapshot["state"];
  snapshot: RtcSnapshot = { state: "idle" };
  closeCount = 0;

  constructor(finalState: RtcSnapshot["state"]) {
    this.finalState = finalState;
  }

  subscribe(listener: (snapshot: RtcSnapshot) => void): () => void {
    this.listeners.add(listener);
    listener(this.snapshot);
    return () => this.listeners.delete(listener);
  }

  async start(_sessionId: string): Promise<void> {
    this.snapshot = { state: "signaling" };
    this.notify();
    this.snapshot = { state: this.finalState };
    this.notify();
  }

  async close(): Promise<void> {
    this.closeCount += 1;
    this.snapshot = { state: "closed" };
    this.notify();
  }

  private notify(): void {
    for (const listener of this.listeners) listener(this.snapshot);
  }
}
