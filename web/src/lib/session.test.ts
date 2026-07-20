import { describe, expect, it } from "vitest";
import {
  clearSession,
  loadSession,
  refreshDelayMs,
  saveSession,
  toSession,
  type SessionStore,
} from "./session";
import type { LoginResponse } from "../api/types";

function memoryStore(): SessionStore {
  const map = new Map<string, string>();
  return {
    getItem: (k) => map.get(k) ?? null,
    setItem: (k, v) => void map.set(k, v),
    removeItem: (k) => void map.delete(k),
  };
}

const RESPONSE: LoginResponse = {
  accessToken: "access",
  expiresAt: "2026-07-20T12:15:00Z",
  refreshToken: "refresh",
  refreshTokenExpiresAt: "2026-07-27T12:00:00Z",
  permissions: ["energy.*", "dashboard.view"],
};

describe("session storage", () => {
  it("round-trips a saved session for its tenant", () => {
    const store = memoryStore();
    const session = toSession("acme", "energy", RESPONSE);

    saveSession(session, store);

    expect(loadSession("acme", store)).toEqual(session);
  });

  it("does not return a session stored under a different tenant", () => {
    const store = memoryStore();
    saveSession(toSession("acme", "energy", RESPONSE), store);

    expect(loadSession("globex", store)).toBeNull();
  });

  it("clears a session", () => {
    const store = memoryStore();
    saveSession(toSession("acme", "energy", RESPONSE), store);

    clearSession("acme", store);

    expect(loadSession("acme", store)).toBeNull();
  });

  it("returns null for an unusable payload", () => {
    const store = memoryStore();
    store.setItem("factoryos.session.acme", "{not json");

    expect(loadSession("acme", store)).toBeNull();
  });
});

describe("refreshDelayMs", () => {
  it("schedules a skew ahead of expiry", () => {
    const now = Date.parse("2026-07-20T12:00:00Z");
    // Expiry is 15 minutes out; with a 60s skew the refresh fires at 14 minutes.
    expect(refreshDelayMs("2026-07-20T12:15:00Z", now, 60_000)).toBe(14 * 60_000);
  });

  it("clamps an already-expired token to an immediate refresh", () => {
    const now = Date.parse("2026-07-20T12:20:00Z");
    expect(refreshDelayMs("2026-07-20T12:15:00Z", now, 60_000)).toBe(0);
  });

  it("falls back to the skew for an unparseable expiry", () => {
    expect(refreshDelayMs("not-a-date", 0, 60_000)).toBe(60_000);
  });
});
