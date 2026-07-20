import { describe, expect, it, vi } from "vitest";
import { login, refresh } from "./auth";
import { TENANT_HEADER } from "./client";
import type { LoginResponse } from "./types";

const SESSION: LoginResponse = {
  accessToken: "access-token",
  expiresAt: "2026-07-20T12:15:00Z",
  refreshToken: "refresh-token",
  refreshTokenExpiresAt: "2026-07-27T12:00:00Z",
  permissions: ["*"],
};

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

describe("auth", () => {
  it("posts credentials to /auth/login with the tenant header", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(SESSION));

    const result = await login(
      "acme",
      { tenantId: "11111111-1111-1111-1111-111111111111", userName: "admin", password: "secret" },
      fetchMock as unknown as typeof fetch,
    );

    const [path, init] = fetchMock.mock.calls[0];
    expect(path).toBe("/auth/login");
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>)[TENANT_HEADER]).toBe("acme");
    expect(JSON.parse(init.body as string)).toMatchObject({ userName: "admin", password: "secret" });
    expect(result.refreshToken).toBe("refresh-token");
  });

  it("surfaces a friendly message when login is unauthorized", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({}, 401));

    await expect(
      login("acme", { tenantId: "t", userName: "admin", password: "wrong" }, fetchMock as unknown as typeof fetch),
    ).rejects.toThrow("Invalid credentials.");
  });

  it("posts the refresh token to /auth/refresh and returns the rotated session", async () => {
    const rotated: LoginResponse = { ...SESSION, refreshToken: "rotated-token" };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(rotated));

    const result = await refresh("acme", "refresh-token", fetchMock as unknown as typeof fetch);

    const [path, init] = fetchMock.mock.calls[0];
    expect(path).toBe("/auth/refresh");
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>)[TENANT_HEADER]).toBe("acme");
    expect(JSON.parse(init.body as string)).toEqual({ refreshToken: "refresh-token" });
    expect(result.refreshToken).toBe("rotated-token");
  });

  it("reports an expired session when refresh is unauthorized", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({}, 401));

    await expect(refresh("acme", "stale", fetchMock as unknown as typeof fetch)).rejects.toThrow("Session expired.");
  });
});
