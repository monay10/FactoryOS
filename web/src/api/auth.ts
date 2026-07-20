import { TENANT_HEADER } from "./client";
import type { LoginResponse } from "./types";

/**
 * Exchanges credentials for a signed access token via the Identity layer (`POST /auth/login`). The returned token
 * is sent as a Bearer on subsequent requests, and the gateway derives the caller's permissions from its claims —
 * so navigation is filtered by a real identity, not a client-supplied list.
 */
export async function login(
  tenant: string,
  credentials: { tenantId: string; userName: string; password: string },
  fetchImpl: typeof fetch = fetch,
): Promise<LoginResponse> {
  const response = await fetchImpl("/auth/login", {
    method: "POST",
    headers: { [TENANT_HEADER]: tenant, "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify(credentials),
  });
  if (!response.ok) {
    throw new Error(response.status === 401 ? "Invalid credentials." : `Login failed: ${response.status}`);
  }
  return (await response.json()) as LoginResponse;
}

/**
 * Rotates a still-active refresh token into a fresh access/refresh pair (`POST /auth/refresh`). Because the server
 * revokes the presented token on rotation, the caller must replace its stored refresh token with the returned one —
 * a replayed token is rejected. This lets the SPA renew a short-lived access token without re-prompting for
 * credentials; a 401 means the session has ended and the user must sign in again.
 */
export async function refresh(
  tenant: string,
  refreshToken: string,
  fetchImpl: typeof fetch = fetch,
): Promise<LoginResponse> {
  const response = await fetchImpl("/auth/refresh", {
    method: "POST",
    headers: { [TENANT_HEADER]: tenant, "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
  if (!response.ok) {
    throw new Error(response.status === 401 ? "Session expired." : `Refresh failed: ${response.status}`);
  }
  return (await response.json()) as LoginResponse;
}
