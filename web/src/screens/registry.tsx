import type { ComponentType } from "react";
import { GatewayClient } from "../api/client";
import OperationsBoard from "./OperationsBoard";
import BrainAnswers from "./BrainAnswers";
import ActivityFeed from "./ActivityFeed";
import WorkOrders from "./WorkOrders";
import QualityLines from "./QualityLines";
import WarehouseStock from "./WarehouseStock";
import OeeSnapshots from "./OeeSnapshots";
import EnergyDashboard from "./EnergyDashboard";

export interface ScreenProps {
  client: GatewayClient;
  /** Whether the caller holds a permission (wildcard-aware). A UX aid — the gateway re-checks every write. */
  holds: (permission: string) => boolean;
}

/**
 * Maps a manifest `component` id to the React component that renders it — the frontend counterpart of the
 * "screens are data, lazy-loaded by component id" contract. A first-party build ships these statically; a
 * Store plugin would register its component here at load time. Unknown ids fall back gracefully.
 */
export const SCREEN_REGISTRY: Record<string, ComponentType<ScreenProps>> = {
  "dashboard/OperationsBoard": OperationsBoard,
  "brain/Answers": BrainAnswers,
  "activity/Feed": ActivityFeed,
  "maintenance/WorkOrders": WorkOrders,
  "quality/Dashboard": QualityLines,
  "warehouse/Dashboard": WarehouseStock,
  "oee/Dashboard": OeeSnapshots,
  "energy/Dashboard": EnergyDashboard,
};

export function resolveScreen(component: string): ComponentType<ScreenProps> | null {
  return SCREEN_REGISTRY[component] ?? null;
}
