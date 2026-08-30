<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useAuthStore } from "./stores/auth";
import { ApiProblemError, apiClient } from "./api/client";
import CatalogView from "./features/catalog/CatalogView.vue";
import PractitionerView from "./features/catalog/PractitionerView.vue";
import AvailabilityView from "./features/catalog/AvailabilityView.vue";
import ChatRoomView from "./features/consultations/ChatRoomView.vue";
import VideoRoomView from "./features/consultations/VideoRoomView.vue";
import AnalysisJobView from "./features/consultations/AnalysisJobView.vue";
import FollowUpCalendarView from "./features/follow_ups/FollowUpCalendarView.vue";
import RiskQueueView from "./features/risk_cases/RiskQueueView.vue";
import RiskRuleVersionsView from "./features/risk_cases/RiskRuleVersionsView.vue";
import AuditView from "./features/audit/AuditView.vue";
import PhoneLoginForm from "./features/auth/PhoneLoginForm.vue";
import ContactEmailView from "./features/account/ContactEmailView.vue";
import { contactEmailService } from "./features/account/contactEmailService";
import { notificationConnection } from "./features/notifications/notificationConnection";
import CareWorkspace from "./features/care/CareWorkspace.vue";
import ConsultationList from "./features/care/ConsultationList.vue";
import { c, type Consultation } from "./features/care/careService";
import { menusForRoles, type MenuView } from "./features/care/roleMenu";
import AppIcon, { type AppIconName } from "./components/AppIcon.vue";
const auth = useAuthStore();
const roles = ref<string[]>([]);
const accountReady = ref(false);
const accountError = ref(false);
const activeView = ref<MenuView | "chat" | "video" | "analysisJobs">(
  "overview",
);
const selectedSession = ref<string | undefined>();
const menus = computed(() => menusForRoles(roles.value));
const primaryRole = computed(() => {
  if (roles.value.includes("Doctor")) return c("option.doctor");
  if (roles.value.includes("Counselor")) return c("option.counselor");
  return c("admin.operations");
});
let generation = 0;
async function loadAccount(): Promise<void> {
  const request = ++generation;
  accountReady.value = false;
  accountError.value = false;
  roles.value = [];
  selectedSession.value = undefined;
  try {
    const profile = await apiClient.get<{ roles: string[] }>("account/me");
    if (request !== generation) return;
    roles.value = profile.roles;
    activeView.value = menus.value[0]?.view ?? "account";
    accountReady.value = true;
  } catch (error) {
    if (request !== generation) return;
    accountError.value = true;
    if (error instanceof ApiProblemError && error.problem.status === 401)
      auth.logout();
  }
}
watch(
  () => auth.isAuthenticated,
  (value) => {
    if (value) void loadAccount();
    else {
      generation++;
      roles.value = [];
      accountReady.value = false;
      selectedSession.value = undefined;
    }
  },
  { immediate: true },
);
function select(view: MenuView): void {
  selectedSession.value = undefined;
  activeView.value = view;
}
function openSession(
  session: Consultation,
  mode: "chat" | "video" | "analysisJobs",
): void {
  if (!roles.value.includes("Counselor")) return;
  selectedSession.value = session.id;
  activeView.value = mode;
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
      <PhoneLoginForm :phone-login="auth" />
    </section>
    <template v-else>
      <header class="topbar">
        <div class="topbar-brand">
          <span class="product-mark">{{ c("app.title") }}</span>
          <div>
            <h1>{{ c("admin.title") }}</h1>
            <p>{{ primaryRole }}</p>
          </div>
        </div>
        <button type="button" class="secondary topbar-logout" @click="logout">
          <AppIcon name="logout" :size="18" />
          <span>{{ c("auth.logout") }}</span>
        </button>
      </header>
      <section v-if="!accountReady" class="workspace-panel">
        <p>{{ c(accountError ? "care.retry" : "care.loading") }}</p>
        <button v-if="accountError" type="button" @click="loadAccount">
          {{ c("care.refresh") }}
        </button>
      </section>
      <div v-else class="admin-layout">
        <nav class="section-nav" :aria-label="c('admin.navigation')">
          <p class="nav-heading">{{ c("admin.navigation") }}</p>
          <button
            v-for="menu in menus"
            :key="menu.view"
            type="button"
            :class="{ active: activeView === menu.view }"
            :aria-current="activeView === menu.view ? 'page' : undefined"
            @click="select(menu.view)"
          >
            <AppIcon :name="menu.view as AppIconName" />
            <span>{{ c(menu.label) }}</span>
          </button>
        </nav>
        <section class="admin-content">
          <CareWorkspace v-if="activeView === 'overview'" mode="overview" />
          <CareWorkspace
            v-else-if="activeView === 'subjects' && roles.includes('Doctor')"
            mode="subjects"
          />
          <CareWorkspace
            v-else-if="activeView === 'plans' && roles.includes('Doctor')"
            mode="plans"
          />
          <ConsultationList
            v-else-if="
              activeView === 'consultations' && roles.includes('Counselor')
            "
            @open="openSession"
          />
          <CatalogView
            v-else-if="
              activeView === 'catalog' && roles.includes('OperationsAdmin')
            "
          />
          <PractitionerView
            v-else-if="
              activeView === 'practitioners' &&
              roles.includes('OperationsAdmin')
            "
          />
          <AvailabilityView
            v-else-if="
              activeView === 'availability' && roles.includes('OperationsAdmin')
            "
          />
          <ChatRoomView
            v-else-if="activeView === 'chat' && roles.includes('Counselor')"
            :key="selectedSession"
            :initial-session-id="selectedSession"
          />
          <VideoRoomView
            v-else-if="activeView === 'video' && roles.includes('Counselor')"
            :key="selectedSession"
            :initial-session-id="selectedSession"
          />
          <AnalysisJobView
            v-else-if="
              activeView === 'analysisJobs' && roles.includes('Counselor')
            "
            :key="selectedSession"
            :initial-session-id="selectedSession"
            :notifications="notificationConnection"
          />
          <RiskQueueView
            v-else-if="activeView === 'riskCases' && roles.includes('Doctor')"
            :notifications="notificationConnection"
          />
          <FollowUpCalendarView
            v-else-if="activeView === 'followUps' && roles.includes('Doctor')"
            :notifications="notificationConnection"
          />
          <RiskRuleVersionsView
            v-else-if="
              activeView === 'riskRules' && roles.includes('OperationsAdmin')
            "
          />
          <AuditView
            v-else-if="
              activeView === 'audit' && roles.includes('OperationsAdmin')
            "
          />
          <ContactEmailView
            v-else-if="activeView === 'account'"
            :contact-email="contactEmailService"
          />
        </section>
      </div>
    </template>
  </main>
</template>
