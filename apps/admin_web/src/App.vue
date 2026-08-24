<script setup lang="ts">
import { reactive, ref } from "vue";
import { useAuthStore } from "./stores/auth";
import { getUiCopy, type UiCopyKey } from "./generated/uiCopy.generated";
import CatalogView from "./features/catalog/CatalogView.vue";
import PractitionerView from "./features/catalog/PractitionerView.vue";
import AvailabilityView from "./features/catalog/AvailabilityView.vue";
import ChatRoomView from "./features/consultations/ChatRoomView.vue";
import VideoRoomView from "./features/consultations/VideoRoomView.vue";
import AnalysisJobView from "./features/consultations/AnalysisJobView.vue";
import FollowUpCalendarView from "./features/follow_ups/FollowUpCalendarView.vue";
import RiskQueueView from "./features/risk_cases/RiskQueueView.vue";
import RiskRuleVersionsView from "./features/risk_cases/RiskRuleVersionsView.vue";
import { notificationConnection } from "./features/notifications/notificationConnection";

const auth = useAuthStore();
const activeView = ref<
  | "catalog"
  | "practitioners"
  | "availability"
  | "chat"
  | "video"
  | "analysisJobs"
  | "riskCases"
  | "followUps"
  | "riskRules"
>("catalog");
const credentials = reactive({ email: "", password: "", totpCode: "" });

async function submitLogin(): Promise<void> {
  if (auth.needsMfaSetup) {
    await auth.completeMfaSetup(credentials.totpCode);
    return;
  }
  await auth.login(
    credentials.email,
    credentials.password,
    auth.needsMfaCode ? credentials.totpCode : undefined,
  );
}

function label(key: string): string {
  return getUiCopy(key as UiCopyKey);
}

async function logout(): Promise<void> {
  try {
    await notificationConnection.stop();
  } finally {
    auth.logout();
  }
}
</script>

<template>
  <main id="app-shell">
    <section v-if="!auth.isAuthenticated" class="login-shell">
      <form class="login-card" @submit.prevent="submitLogin">
        <h1>{{ getUiCopy("admin.title") }}</h1>
        <label
          >{{ getUiCopy("auth.email")
          }}<input v-model.trim="credentials.email" type="email" required
        /></label>
        <label
          >{{ getUiCopy("auth.password")
          }}<input v-model="credentials.password" type="password" required
        /></label>
        <template v-if="auth.needsMfaSetup || auth.needsMfaCode">
          <p v-if="auth.needsMfaSetup">{{ getUiCopy("auth.mfaSetup") }}</p>
          <code v-if="auth.needsMfaSetup">{{ auth.mfaManualKey }}</code>
          <label
            >{{ getUiCopy("auth.mfaCode")
            }}<input
              v-model.trim="credentials.totpCode"
              inputmode="numeric"
              maxlength="6"
              required
          /></label>
        </template>
        <p v-if="auth.errorCopyKey" class="error" role="alert">
          {{ label(auth.errorCopyKey) }}
        </p>
        <button type="submit" :disabled="auth.isBusy">
          {{
            getUiCopy(
              auth.isBusy
                ? "auth.loggingIn"
                : auth.needsMfaSetup
                  ? "auth.enableMfa"
                  : "auth.login",
            )
          }}
        </button>
      </form>
    </section>

    <template v-else>
      <header class="topbar">
        <div>
          <span class="product-mark">MH</span>
          <h1>{{ getUiCopy("admin.title") }}</h1>
        </div>
        <button type="button" class="secondary" @click="logout">
          {{ getUiCopy("auth.logout") }}
        </button>
      </header>
      <div class="admin-layout">
        <nav class="section-nav" :aria-label="getUiCopy('admin.navigation')">
          <button
            type="button"
            :class="{ active: activeView === 'catalog' }"
            @click="activeView = 'catalog'"
          >
            {{ getUiCopy("admin.catalog") }}
          </button>
          <button
            type="button"
            :class="{ active: activeView === 'practitioners' }"
            @click="activeView = 'practitioners'"
          >
            {{ getUiCopy("admin.practitioners") }}
          </button>
          <button
            type="button"
            :class="{ active: activeView === 'availability' }"
            @click="activeView = 'availability'"
          >
            {{ getUiCopy("admin.availability") }}
          </button>
          <button
            type="button"
            :class="{ active: activeView === 'chat' }"
            @click="activeView = 'chat'"
          >
            {{ getUiCopy("admin.chat") }}
          </button>
          <button
            type="button"
            :class="{ active: activeView === 'video' }"
            @click="activeView = 'video'"
          >
            {{ getUiCopy("admin.video") }}
          </button>
          <button
            type="button"
            :class="{ active: activeView === 'analysisJobs' }"
            @click="activeView = 'analysisJobs'"
          >
            {{ getUiCopy("admin.analysisJobs") }}
          </button>
          <button
            type="button"
            :class="{ active: activeView === 'riskCases' }"
            @click="activeView = 'riskCases'"
          >
            {{ getUiCopy("admin.riskCases") }}
          </button>
          <button
            type="button"
            :class="{ active: activeView === 'followUps' }"
            @click="activeView = 'followUps'"
          >
            {{ getUiCopy("admin.followUps") }}
          </button>
          <button
            type="button"
            :class="{ active: activeView === 'riskRules' }"
            @click="activeView = 'riskRules'"
          >
            {{ getUiCopy("admin.riskRules") }}
          </button>
        </nav>
        <section class="admin-content">
          <CatalogView v-if="activeView === 'catalog'" />
          <PractitionerView v-else-if="activeView === 'practitioners'" />
          <AvailabilityView v-else-if="activeView === 'availability'" />
          <ChatRoomView v-else-if="activeView === 'chat'" />
          <VideoRoomView v-else-if="activeView === 'video'" />
          <AnalysisJobView
            v-else-if="activeView === 'analysisJobs'"
            :notifications="notificationConnection"
          />
          <RiskQueueView
            v-else-if="activeView === 'riskCases'"
            :notifications="notificationConnection"
          />
          <FollowUpCalendarView
            v-else-if="activeView === 'followUps'"
            :notifications="notificationConnection"
          />
          <RiskRuleVersionsView v-else />
        </section>
      </div>
    </template>
  </main>
</template>
