<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from "vue";
import { getUiCopy, type UiCopyKey } from "../../generated/uiCopy.generated";
import {
  createChatConnection,
  type ChatConnection,
  type ChatSnapshot,
} from "./chatConnection";

const props = defineProps<{
  connection?: ChatConnection;
  initialSessionId?: string;
}>();
const chat = props.connection ?? createChatConnection();
const sessionId = ref(props.initialSessionId ?? "");
const draft = ref("");
const errorCopyKey = ref<UiCopyKey | null>(null);
const snapshot = ref<ChatSnapshot>(chat.snapshot);
const unsubscribe = chat.subscribe((next) => {
  snapshot.value = next;
});

const connected = computed(() => snapshot.value.status === "connected");
const counselorOnline = computed(() =>
  snapshot.value.presences.some(
    (presence) => presence.kind === "Practitioner" && presence.online,
  ),
);

async function connect(): Promise<void> {
  errorCopyKey.value = null;
  try {
    await chat.connect(sessionId.value);
  } catch {
    errorCopyKey.value = "error.retry";
  }
}

async function send(): Promise<void> {
  const text = draft.value.trim();
  if (!text) return;
  errorCopyKey.value = null;
  try {
    const requestId =
      typeof crypto.randomUUID === "function"
        ? `web-${crypto.randomUUID()}`
        : `web-${Date.now()}`;
    await chat.send(text, requestId);
    draft.value = "";
  } catch {
    errorCopyKey.value = "error.retry";
  }
}

function statusLabel(): string {
  const keys: Record<ChatSnapshot["status"], UiCopyKey> = {
    connecting: "chat.connecting",
    connected: "chat.connected",
    reconnecting: "chat.reconnecting",
    disconnected: "chat.disconnected",
  };
  return getUiCopy(keys[snapshot.value.status]);
}

onBeforeUnmount(() => {
  unsubscribe();
  void chat.disconnect();
});
</script>

<template>
  <section class="workspace-panel chat-workspace">
    <div class="section-header">
      <div>
        <h2>{{ getUiCopy("chat.title") }}</h2>
        <p>
          {{ statusLabel() }} ·
          {{
            getUiCopy(
              counselorOnline
                ? "chat.counselorOnline"
                : "chat.counselorOffline",
            )
          }}
        </p>
      </div>
    </div>

    <form class="chat-connect" @submit.prevent="connect">
      <label>
        {{ getUiCopy("chat.sessionId") }}
        <input v-model.trim="sessionId" data-testid="session-id" required />
      </label>
      <button
        data-testid="join-chat"
        type="submit"
        :disabled="!sessionId || snapshot.status === 'connecting'"
      >
        {{ getUiCopy("chat.connect") }}
      </button>
    </form>

    <p v-if="errorCopyKey" class="error" role="alert">
      {{ getUiCopy(errorCopyKey) }}
    </p>

    <ol v-if="snapshot.messages.length" class="chat-messages">
      <li
        v-for="message in snapshot.messages"
        :key="message.id"
        data-testid="chat-message"
      >
        <strong>{{
          getUiCopy(
            message.senderKind === "Practitioner"
              ? "chat.counselor"
              : "chat.me",
          )
        }}</strong>
        <span>{{ message.text }}</span>
      </li>
    </ol>
    <p v-else class="empty">{{ getUiCopy("chat.noMessages") }}</p>

    <form class="chat-composer" @submit.prevent="send">
      <input
        v-model="draft"
        data-testid="chat-input"
        :placeholder="getUiCopy('chat.messageHint')"
        maxlength="4000"
        :disabled="!connected"
      />
      <button
        data-testid="send-chat"
        type="submit"
        :disabled="!connected || !draft.trim()"
      >
        {{ getUiCopy("chat.send") }}
      </button>
    </form>
  </section>
</template>
