import { describe, expect, it } from "vitest";
import { levelTone, percent, stateTone, timeAgo } from "./format";

describe("format", () => {
  it("renders a fraction as a whole percent", () => {
    expect(percent(0.824)).toBe("82%");
    expect(percent(1)).toBe("100%");
  });

  it("labels recent instants relatively", () => {
    const now = new Date("2026-07-20T12:00:00Z");
    expect(timeAgo("2026-07-20T11:59:40Z", now)).toBe("just now");
    expect(timeAgo("2026-07-20T11:30:00Z", now)).toBe("30m ago");
    expect(timeAgo("2026-07-20T09:00:00Z", now)).toBe("3h ago");
    expect(timeAgo("2026-07-18T12:00:00Z", now)).toBe("2d ago");
  });

  it("maps alert levels and plugin states to semantic tones", () => {
    expect(levelTone("Critical")).toBe("critical");
    expect(levelTone("Warning")).toBe("warning");
    expect(levelTone("Info")).toBe("ok");
    expect(levelTone("Something")).toBe("neutral");
    expect(stateTone("Started")).toBe("ok");
    expect(stateTone("Failed")).toBe("bad");
    expect(stateTone("Disabled")).toBe("muted");
  });
});
