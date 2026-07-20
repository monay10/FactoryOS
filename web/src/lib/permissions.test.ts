import { describe, expect, it } from "vitest";
import { grants, permitFor } from "./permissions";

describe("grants", () => {
  it("treats a null held set as unrestricted", () => {
    expect(grants(null, "maintenance.close")).toBe(true);
  });

  it("grants nothing for an empty held set", () => {
    expect(grants([], "maintenance.close")).toBe(false);
  });

  it("honors the global wildcard", () => {
    expect(grants(["*"], "maintenance.close")).toBe(true);
  });

  it("honors a resource wildcard on a boundary", () => {
    expect(grants(["maintenance.*"], "maintenance.close")).toBe(true);
    expect(grants(["maintenance.*"], "maintenance.view")).toBe(true);
    // Boundary: must be the whole resource segment, not a prefix of another resource.
    expect(grants(["maintenance.*"], "maintenancex.close")).toBe(false);
  });

  it("matches an exact permission case-insensitively", () => {
    expect(grants(["Maintenance.Close"], "maintenance.close")).toBe(true);
    expect(grants(["maintenance.view"], "maintenance.close")).toBe(false);
  });

  it("permitFor closes over the held set", () => {
    const holds = permitFor(["maintenance.view"]);
    expect(holds("maintenance.view")).toBe(true);
    expect(holds("maintenance.close")).toBe(false);
  });
});
