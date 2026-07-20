import { describe, expect, it } from "vitest";
import { resolveScreen, SCREEN_REGISTRY } from "./registry";

describe("screen registry", () => {
  it("resolves every first-party module screen by its manifest component id", () => {
    // These ids are the `component` values declared in each plugin's module.json `ui` block.
    for (const id of [
      "dashboard/OperationsBoard",
      "brain/Answers",
      "activity/Feed",
      "maintenance/WorkOrders",
      "quality/Dashboard",
      "warehouse/Dashboard",
      "oee/Dashboard",
      "energy/Dashboard",
    ]) {
      expect(resolveScreen(id)).toBe(SCREEN_REGISTRY[id]);
      expect(resolveScreen(id)).toBeTruthy();
    }
  });

  it("returns null for a component the shell has not been built with (graceful fallback)", () => {
    expect(resolveScreen("store-plugin/Unknown")).toBeNull();
    expect(resolveScreen("carbon/Dashboard")).toBeNull();
  });
});
