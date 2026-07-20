import { describe, expect, it } from "vitest";
import { findByRoute, firstScreen, flattenNav } from "./nav";
import type { NavCatalog } from "../api/types";

const nav: NavCatalog = {
  sections: [
    {
      section: "Experience",
      items: [
        { module: "dashboard", id: "d", title: "Operations", route: "/dashboard", component: "dashboard/OperationsBoard", icon: null, requiredPermission: null, order: 1 },
      ],
    },
    {
      section: "AI",
      items: [
        { module: "brain", id: "b", title: "Brain", route: "/brain/answers", component: "brain/Answers", icon: null, requiredPermission: null, order: 21 },
      ],
    },
  ],
};

describe("nav", () => {
  it("flattens sections preserving order", () => {
    expect(flattenNav(nav).map((i) => i.route)).toEqual(["/dashboard", "/brain/answers"]);
  });

  it("opens the first screen of the first section", () => {
    expect(firstScreen(nav)?.route).toBe("/dashboard");
  });

  it("finds an item by route", () => {
    expect(findByRoute(nav, "/brain/answers")?.module).toBe("brain");
    expect(findByRoute(nav, "/nope")).toBeNull();
  });
});
