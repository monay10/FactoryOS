import type {
  ActivityFeed,
  BrainAnswers,
  BrainAskAccepted,
  DashboardBoard,
  EnergyMeters,
  EnergySpikes,
  OeeSnapshots,
  QualityLines,
  QuarantineResult,
  ShellBootstrap,
  StockResponse,
  StoreCatalog,
  StorePlugin,
  StoreSummary,
  SystemStatus,
  WorkOrders,
  WorkOrderView,
} from "./types";

/** The tenant header the gateway resolves at the edge; the query fallback exists but the header is preferred. */
export const TENANT_HEADER = "X-FactoryOS-Tenant";

/** The permissions header the gateway resolves at the edge to filter navigation (RBAC). */
export const PERMISSIONS_HEADER = "X-FactoryOS-Permissions";

/**
 * A thin client over the FactoryOS gateway. Every call is same-origin and tenant-scoped: the tenant travels
 * in the header, never in component code. The client knows only the gateway's stable discovery + read routes.
 * An optional permission set is sent so the gateway filters navigation to what the caller may see.
 */
export class GatewayClient {
  constructor(
    private readonly tenant: string,
    private readonly fetchImpl: typeof fetch = fetch,
    private readonly permissions: readonly string[] | null = null,
    private readonly accessToken: string | null = null,
  ) {}

  private headers(extra?: Record<string, string>): Record<string, string> {
    const h: Record<string, string> = { [TENANT_HEADER]: this.tenant, Accept: "application/json", ...extra };
    if (this.accessToken) {
      // A signed session wins: the gateway derives permissions from the token's claims.
      h["Authorization"] = `Bearer ${this.accessToken}`;
    } else if (this.permissions !== null) {
      // Dev/tools fallback: send an explicit permission set. Omitted entirely when unrestricted.
      h[PERMISSIONS_HEADER] = this.permissions.join(",");
    }
    return h;
  }

  private async get<T>(path: string): Promise<T> {
    const response = await this.fetchImpl(path, { headers: this.headers() });
    if (!response.ok) {
      throw new Error(`GET ${path} failed: ${response.status}`);
    }
    return (await response.json()) as T;
  }

  shell(): Promise<ShellBootstrap> {
    return this.get<ShellBootstrap>("/shell");
  }

  system(): Promise<SystemStatus> {
    return this.get<SystemStatus>("/system");
  }

  storeCatalog(): Promise<StoreCatalog> {
    return this.get<StoreCatalog>("/store/plugins");
  }

  storeSummary(): Promise<StoreSummary> {
    return this.get<StoreSummary>("/store/summary");
  }

  dashboardBoard(): Promise<DashboardBoard> {
    return this.get<DashboardBoard>("/m/dashboard/board");
  }

  brainAnswers(): Promise<BrainAnswers> {
    return this.get<BrainAnswers>("/m/brain/answers");
  }

  /**
   * Poses a question to the Company Brain. Asking is decoupled from answering: this publishes a
   * BrainQuestionAsked (202 Accepted) and the grounded answer arrives asynchronously at /m/brain/answers.
   */
  async askBrain(question: string, askedBy?: string): Promise<BrainAskAccepted> {
    const response = await this.fetchImpl("/m/brain/ask", {
      method: "POST",
      headers: this.headers({ "Content-Type": "application/json" }),
      body: JSON.stringify({ question, askedBy }),
    });
    if (!response.ok) {
      throw new Error(`ask failed: ${response.status}`);
    }
    return (await response.json()) as BrainAskAccepted;
  }

  activityFeed(): Promise<ActivityFeed> {
    return this.get<ActivityFeed>("/m/activity/feed");
  }

  workOrders(): Promise<WorkOrders> {
    return this.get<WorkOrders>("/m/maintenance/workorders");
  }

  /**
   * Closes a work order (a write action). The gateway authorizes it against the caller's `maintenance.close`
   * permission — a 403 means the signed session lacks it. Returns the order in its closed state.
   */
  async closeWorkOrder(number: string): Promise<WorkOrderView> {
    const response = await this.fetchImpl(`/m/maintenance/workorders/${encodeURIComponent(number)}/close`, {
      method: "POST",
      headers: this.headers(),
    });
    if (!response.ok) {
      throw new Error(
        response.status === 403 ? "You do not have permission to close work orders." : `close failed: ${response.status}`,
      );
    }
    return (await response.json()) as WorkOrderView;
  }

  qualityLines(): Promise<QualityLines> {
    return this.get<QualityLines>("/m/quality/lines");
  }

  /**
   * Places a quality line under quarantine (a write action). The gateway authorizes it against the caller's
   * `quality.quarantine` permission — a 403 means the signed session lacks it. Returns the quarantine result.
   */
  async quarantineLine(lineId: string): Promise<QuarantineResult> {
    const response = await this.fetchImpl(`/m/quality/lines/${encodeURIComponent(lineId)}/quarantine`, {
      method: "POST",
      headers: this.headers(),
    });
    if (!response.ok) {
      throw new Error(
        response.status === 403 ? "You do not have permission to quarantine lines." : `quarantine failed: ${response.status}`,
      );
    }
    return (await response.json()) as QuarantineResult;
  }

  warehouseStock(): Promise<StockResponse> {
    return this.get<StockResponse>("/m/warehouse/stock");
  }

  oeeSnapshots(): Promise<OeeSnapshots> {
    return this.get<OeeSnapshots>("/m/oee/snapshots");
  }

  energyMeters(): Promise<EnergyMeters> {
    return this.get<EnergyMeters>("/m/energy/meters");
  }

  energySpikes(): Promise<EnergySpikes> {
    return this.get<EnergySpikes>("/m/energy/spikes");
  }

  /** Store write side (Phase 5): enable or disable an installed plugin; returns its refreshed entry. */
  async setPluginEnabled(key: string, enabled: boolean): Promise<StorePlugin> {
    const action = enabled ? "enable" : "disable";
    const response = await this.fetchImpl(`/store/plugins/${encodeURIComponent(key)}/${action}`, {
      method: "POST",
      headers: this.headers(),
    });
    if (!response.ok) {
      throw new Error(`${action} ${key} failed: ${response.status}`);
    }
    return (await response.json()) as StorePlugin;
  }
}
