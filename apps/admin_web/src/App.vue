<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from './stores/auth'
import { getUiCopy } from './generated/uiCopy.generated'
import CatalogView from './features/catalog/CatalogView.vue'
import PractitionerView from './features/catalog/PractitionerView.vue'
import AvailabilityView from './features/catalog/AvailabilityView.vue'
import ChatRoomView from './features/consultations/ChatRoomView.vue'
import VideoRoomView from './features/consultations/VideoRoomView.vue'
import AnalysisJobView from './features/consultations/AnalysisJobView.vue'
import FollowUpCalendarView from './features/follow_ups/FollowUpCalendarView.vue'
import RiskQueueView from './features/risk_cases/RiskQueueView.vue'
import RiskRuleVersionsView from './features/risk_cases/RiskRuleVersionsView.vue'
import AuditView from './features/audit/AuditView.vue'
import PhoneLoginForm from './features/auth/PhoneLoginForm.vue'
import ContactEmailView from './features/account/ContactEmailView.vue'
import { contactEmailService } from './features/account/contactEmailService'
import { notificationConnection } from './features/notifications/notificationConnection'

const auth = useAuthStore()
const activeView = ref<
  | 'catalog' | 'practitioners' | 'availability' | 'chat' | 'video' | 'analysisJobs'
  | 'riskCases' | 'followUps' | 'riskRules' | 'audit' | 'account'
>('catalog')

async function logout(): Promise<void> {
  try { await notificationConnection.stop() }
  finally { auth.logout() }
}
</script>

<template>
  <main id="app-shell">
    <section v-if="!auth.isAuthenticated" class="login-shell">
      <PhoneLoginForm :phone-login="auth" />
    </section>

    <template v-else>
      <header class="topbar">
        <div><span class="product-mark">MH</span><h1>{{ getUiCopy('admin.title') }}</h1></div>
        <button type="button" class="secondary" @click="logout">{{ getUiCopy('auth.logout') }}</button>
      </header>
      <div class="admin-layout">
        <nav class="section-nav" :aria-label="getUiCopy('admin.navigation')">
          <button type="button" :class="{ active: activeView === 'catalog' }" @click="activeView = 'catalog'">{{ getUiCopy('admin.catalog') }}</button>
          <button type="button" :class="{ active: activeView === 'practitioners' }" @click="activeView = 'practitioners'">{{ getUiCopy('admin.practitioners') }}</button>
          <button type="button" :class="{ active: activeView === 'availability' }" @click="activeView = 'availability'">{{ getUiCopy('admin.availability') }}</button>
          <button type="button" :class="{ active: activeView === 'chat' }" @click="activeView = 'chat'">{{ getUiCopy('admin.chat') }}</button>
          <button type="button" :class="{ active: activeView === 'video' }" @click="activeView = 'video'">{{ getUiCopy('admin.video') }}</button>
          <button type="button" :class="{ active: activeView === 'analysisJobs' }" @click="activeView = 'analysisJobs'">{{ getUiCopy('admin.analysisJobs') }}</button>
          <button type="button" :class="{ active: activeView === 'riskCases' }" @click="activeView = 'riskCases'">{{ getUiCopy('admin.riskCases') }}</button>
          <button type="button" :class="{ active: activeView === 'followUps' }" @click="activeView = 'followUps'">{{ getUiCopy('admin.followUps') }}</button>
          <button type="button" :class="{ active: activeView === 'riskRules' }" @click="activeView = 'riskRules'">{{ getUiCopy('admin.riskRules') }}</button>
          <button type="button" :class="{ active: activeView === 'audit' }" @click="activeView = 'audit'">{{ getUiCopy('admin.audit') }}</button>
          <button type="button" :class="{ active: activeView === 'account' }" @click="activeView = 'account'">{{ getUiCopy('admin.account') }}</button>
        </nav>
        <section class="admin-content">
          <CatalogView v-if="activeView === 'catalog'" />
          <PractitionerView v-else-if="activeView === 'practitioners'" />
          <AvailabilityView v-else-if="activeView === 'availability'" />
          <ChatRoomView v-else-if="activeView === 'chat'" />
          <VideoRoomView v-else-if="activeView === 'video'" />
          <AnalysisJobView v-else-if="activeView === 'analysisJobs'" :notifications="notificationConnection" />
          <RiskQueueView v-else-if="activeView === 'riskCases'" :notifications="notificationConnection" />
          <FollowUpCalendarView v-else-if="activeView === 'followUps'" :notifications="notificationConnection" />
          <RiskRuleVersionsView v-else-if="activeView === 'riskRules'" />
          <AuditView v-else-if="activeView === 'audit'" />
          <ContactEmailView v-else :contact-email="contactEmailService" />
        </section>
      </div>
    </template>
  </main>
</template>
