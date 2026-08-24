import { apiClient } from "../../api/client";

export type RiskRuleVersion = {
  id: string;
  version: string;
  weights: Record<string, number>;
  thresholds: number[];
  crisisRulesEnabled: boolean;
  active: boolean;
  createdAt: string;
  activatedAt: string | null;
};

export type RiskRuleInput = {
  version: string;
  scaleWeight: number;
  textWeight: number;
  audioWeight: number;
  videoWeight: number;
  trendWeight: number;
  thresholds: number[];
  crisisRulesEnabled: true;
};

export interface RiskRuleService {
  list(): Promise<RiskRuleVersion[]>;
  create(input: RiskRuleInput): Promise<RiskRuleVersion>;
  activate(version: string): Promise<RiskRuleVersion>;
}

export const riskRuleService: RiskRuleService = {
  list: () => apiClient.get<RiskRuleVersion[]>("admin/risk-rules"),
  create: (input) => apiClient.post<RiskRuleVersion>("admin/risk-rules", input),
  activate: (version) =>
    apiClient.post<RiskRuleVersion>(`admin/risk-rules/${version}/activate`),
};
