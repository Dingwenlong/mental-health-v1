<script setup lang="ts">
import {
  Activity,
  BarChart3,
  CalendarClock,
  CalendarDays,
  ClipboardList,
  FileClock,
  LayoutDashboard,
  LibraryBig,
  LogOut,
  Mail,
  MessageSquareText,
  RefreshCw,
  Settings2,
  ShieldCheck,
  UserRoundCog,
  UsersRound,
  type LucideIcon,
} from "lucide-vue-next";

export type AppIconName =
  | "overview"
  | "subjects"
  | "plans"
  | "consultations"
  | "catalog"
  | "practitioners"
  | "availability"
  | "riskCases"
  | "followUps"
  | "riskRules"
  | "audit"
  | "account"
  | "refresh"
  | "records"
  | "privacy"
  | "logout";

const props = withDefaults(
  defineProps<{ name: AppIconName; size?: number; strokeWidth?: number }>(),
  { size: 20, strokeWidth: 1.8 },
);

const icons: Record<AppIconName, LucideIcon> = {
  overview: LayoutDashboard,
  subjects: UsersRound,
  plans: ClipboardList,
  consultations: MessageSquareText,
  catalog: LibraryBig,
  practitioners: UserRoundCog,
  availability: CalendarDays,
  riskCases: Activity,
  followUps: CalendarClock,
  riskRules: Settings2,
  audit: FileClock,
  account: Mail,
  refresh: RefreshCw,
  records: BarChart3,
  privacy: ShieldCheck,
  logout: LogOut,
};
</script>

<template>
  <component
    :is="icons[props.name]"
    aria-hidden="true"
    :size="props.size"
    :stroke-width="props.strokeWidth"
  />
</template>
