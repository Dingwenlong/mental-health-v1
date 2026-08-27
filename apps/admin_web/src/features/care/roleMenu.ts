export type MenuView =
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
  | "account";
export type MenuItem = { view: MenuView; label: string };
export function menusForRoles(roles: readonly string[]): MenuItem[] {
  const menus: MenuItem[] = [];
  if (
    roles.some((role) =>
      ["Doctor", "Counselor", "OperationsAdmin"].includes(role),
    )
  )
    menus.push({ view: "overview", label: "care.overview" });
  if (roles.includes("Doctor"))
    menus.push(
      { view: "subjects", label: "care.subjects" },
      { view: "plans", label: "care.plans" },
      { view: "riskCases", label: "admin.riskCases" },
      { view: "followUps", label: "admin.followUps" },
    );
  if (roles.includes("Counselor"))
    menus.push({ view: "consultations", label: "care.consultations" });
  if (roles.includes("OperationsAdmin"))
    menus.push(
      { view: "catalog", label: "admin.catalog" },
      { view: "practitioners", label: "admin.practitioners" },
      { view: "availability", label: "admin.availability" },
      { view: "riskRules", label: "admin.riskRules" },
      { view: "audit", label: "admin.audit" },
    );
  menus.push({ view: "account", label: "admin.account" });
  return menus;
}
