<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from "vue";
import { getUiCopy, type UiCopyKey } from "../../generated/uiCopy.generated";
import {
  createBrowserRtcPeer,
  type BrowserRtcPeer,
  type RtcSnapshot,
} from "./browserRtcPeer";

const props = defineProps<{
  peer?: BrowserRtcPeer;
  initialSessionId?: string;
}>();
const peer = props.peer ?? createBrowserRtcPeer();
const sessionId = ref(props.initialSessionId ?? "");
const snapshot = ref<RtcSnapshot>(peer.snapshot);
const localVideo = ref<HTMLVideoElement | null>(null);
const remoteVideo = ref<HTMLVideoElement | null>(null);
const unsubscribe = peer.subscribe((next) => {
  snapshot.value = next;
  void nextTick(attachStreams);
});

const busy = computed(() => snapshot.value.state === "signaling");
const inRoom = computed(
  () => busy.value || snapshot.value.state === "connected",
);

async function connect(): Promise<void> {
  await peer.start(sessionId.value);
}

function attachStreams(): void {
  if (localVideo.value)
    localVideo.value.srcObject = snapshot.value.localStream ?? null;
  if (remoteVideo.value) {
    remoteVideo.value.srcObject = snapshot.value.remoteStream ?? null;
  }
}

function statusLabel(): string {
  if (snapshot.value.state === "signaling" && snapshot.value.roomJoined) {
    return getUiCopy("video.waitingForPeer");
  }
  const keys: Record<RtcSnapshot["state"], UiCopyKey> = {
    idle: "video.start",
    signaling: "video.signaling",
    connected: "video.connected",
    failed: "video.failed",
    closed: "video.closed",
  };
  return getUiCopy(keys[snapshot.value.state]);
}

onMounted(attachStreams);
onBeforeUnmount(() => {
  unsubscribe();
  void peer.close();
});
</script>

<template>
  <section
    class="workspace-panel video-workspace"
    data-testid="video-room"
    :data-room-ready="snapshot.roomJoined === true"
  >
    <div class="section-header">
      <div>
        <h2>{{ getUiCopy("video.title") }}</h2>
        <p data-testid="rtc-status">{{ statusLabel() }}</p>
      </div>
    </div>

    <form class="video-connect" @submit.prevent="connect">
      <label>
        {{ getUiCopy("video.sessionId") }}
        <input
          v-model.trim="sessionId"
          data-testid="video-session-id"
          required
        />
      </label>
      <button type="submit" :disabled="!sessionId || busy">
        {{ getUiCopy("video.start") }}
      </button>
    </form>

    <div v-if="inRoom" class="video-grid">
      <figure>
        <video ref="remoteVideo" autoplay playsinline></video>
        <figcaption>{{ getUiCopy("video.remote") }}</figcaption>
      </figure>
      <figure>
        <video ref="localVideo" autoplay muted playsinline></video>
        <figcaption>{{ getUiCopy("video.local") }}</figcaption>
      </figure>
    </div>

    <button v-if="inRoom" type="button" class="danger" @click="peer.close()">
      {{ getUiCopy("video.end") }}
    </button>
  </section>
</template>
