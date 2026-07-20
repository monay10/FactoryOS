// Typed mirrors of the FactoryOS gateway's discovery + read contracts. The shell speaks only these
// shapes — never a plugin by name — so a new module appears in the UI purely by being active on the host.

export interface TenantContext {
  resolved: boolean;
  tenant: string | null;
}

export interface NavItem {
  module: string;
  id: string;
  title: string;
  route: string;
  component: string;
  icon: string | null;
  requiredPermission: string | null;
  order: number;
}

export interface NavSection {
  section: string;
  items: NavItem[];
}

export interface NavCatalog {
  sections: NavSection[];
}

export interface ModuleApiRoute {
  method: string;
  path: string;
  query: string[];
  description?: string;
}

export interface ModuleApiSummary {
  key: string;
  name: string;
  routes: ModuleApiRoute[];
}

export interface TenantBranding {
  tenant: string;
  displayName: string;
  primaryColor: string | null;
  logoUrl: string | null;
}

export interface ShellBootstrap {
  tenant: TenantContext;
  branding: TenantBranding;
  nav: NavCatalog;
  apis: ModuleApiSummary[];
}

export interface SystemStatus {
  product: string;
  version: string;
  modulesInstalled: number;
  modulesActive: number;
  pluginsNeedingAttention: number;
  capabilities: string[];
  eventTypes: number;
}

export interface StoreDependency {
  pluginKey: string;
  minimumVersion: string;
  satisfied: boolean;
}

export interface StorePlugin {
  key: string;
  name: string;
  version: string;
  description: string | null;
  author: string | null;
  state: string;
  provides: string[];
  dependencies: StoreDependency[];
}

export interface StoreCatalog {
  plugins: StorePlugin[];
}

export interface StoreStateTally {
  state: string;
  count: number;
}

export interface StoreSummary {
  total: number;
  byState: StoreStateTally[];
  withUnmetDependencies: number;
}

// ---- Module read models the operator screens render ----

export interface DashboardMachine {
  machineId: string;
  oee: number;
  meetsTarget: boolean;
  asOf: string;
}

export interface DashboardAlert {
  kind: string;
  level: string;
  subject: string;
  occurredAt: string;
}

export interface DashboardBoard {
  tenant: string;
  machines: DashboardMachine[];
  alerts: DashboardAlert[];
  criticalAlertCount: number;
}

export interface BrainCitation {
  source: string;
  chunkId: string;
  score: number;
}

export interface BrainAnswer {
  tenant: string;
  question: string;
  answer: string;
  model: string;
  citations: BrainCitation[];
  answeredAt: string;
  sourceEventId: string;
}

export interface BrainAnswers {
  tenant: string;
  answers: BrainAnswer[];
}

export interface BrainAskAccepted {
  tenant: string;
  questionId: string;
  question: string;
}

export interface ActivityEntry {
  tenant: string;
  category: string;
  headline: string;
  occurredAt: string;
  sourceEventId: string;
}

export interface ActivityFeed {
  tenant: string;
  entries: ActivityEntry[];
}

export interface WorkOrderView {
  number: string;
  title: string;
  status: string;
  assetCode: string | null;
  dueAt: string | null;
}

export interface WorkOrders {
  tenant: string;
  workOrders: WorkOrderView[];
}

export interface QualityLineView {
  lineId: string;
  productId: string;
  inspectedUnits: number;
  defectiveUnits: number;
  defectRate: number;
  breachesThreshold: boolean;
  quarantined: boolean;
}

export interface QualityLines {
  tenant: string;
  lines: QualityLineView[];
}

export interface QuarantineResult {
  tenant: string;
  lineId: string;
  quarantined: boolean;
  newlyQuarantined: boolean;
}

export interface StockView {
  warehouseId: string;
  sku: string;
  onHand: number;
  reorderPoint: number | null;
  belowReorder: boolean;
}

export interface StockResponse {
  tenant: string;
  items: StockView[];
}

export interface OeeSnapshotView {
  machineId: string;
  periodStart: string;
  periodEnd: string;
  availability: number;
  performance: number;
  quality: number;
  oee: number;
  meetsTarget: boolean;
}

export interface OeeSnapshots {
  tenant: string;
  snapshots: OeeSnapshotView[];
}

export interface EnergyMeterView {
  meterId: string;
  metric: string;
  value: number;
  baseline: number;
  deltaPercent: number;
  unit: string;
  readingAt: string;
}

export interface EnergyMeters {
  tenant: string;
  meters: EnergyMeterView[];
}

export interface EnergySpikeView {
  meterId: string;
  metric: string;
  value: number;
  baseline: number;
  deltaPercent: number;
  unit: string;
  readingAt: string;
}

export interface EnergySpikes {
  tenant: string;
  spikes: EnergySpikeView[];
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  permissions: string[];
}
