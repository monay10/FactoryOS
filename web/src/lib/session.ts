import type { LoginResponse } from "../api/types";

/**
 * A signed session held by the SPA. The access token drives every gateway call (the gateway derives permissions from
 * its claims); the refresh token renews the access token silently before it expires. Sessions are scoped per tenant so
 * switching factories never carries one tenant's identity into another.
 */
export interface Session {
  tenant: string;
  userName: string;
  accessToken: string;
  refreshToken: string;
  /** ISO-8601 UTC instant the access token expires. */
  expiresAt: string;
  permissions: string[];
}

/** The demo seed tenant. Production resolves a tenant slug to its id server-side; the SPA never invents identities. */
export const DEMO_TENANT_ID = "11111111-1111-1111-1111-111111111111";

/** Builds a session from a login/refresh response for the given tenant and user. */
export function toSession(tenant: string, userName: string, response: LoginResponse): Session {
  return {
    tenant,
    userName,
    accessToken: response.accessToken,
    refreshToken: response.refreshToken,
    expiresAt: response.expiresAt,
    permissions: response.permissions,
  };
}

function storageKey(tenant: string): string {
  return `factoryos.session.${tenant}`;
}

/** A minimal storage shape so the helpers are testable without a browser. */
export interface SessionStore {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

function defaultStore(): SessionStore | null {
  try {
    return typeof window !== "undefined" ? window.localStorage : null;
  } catch {
    // Access to localStorage can throw (private mode, disabled storage); treat as no persistence.
    return null;
  }
}

/** Loads the persisted session for a tenant, or null when none is stored or the payload is unusable. */
export function loadSession(tenant: string, store: SessionStore | null = defaultStore()): Session | null {
  if (!store) return null;
  const raw = store.getItem(storageKey(tenant));
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as Session;
    if (parsed && parsed.accessToken && parsed.refreshToken && parsed.tenant === tenant) {
      return parsed;
    }
    return null;
  } catch {
    return null;
  }
}

/** Persists a session for its tenant. */
export function saveSession(session: Session, store: SessionStore | null = defaultStore()): void {
  store?.setItem(storageKey(session.tenant), JSON.stringify(session));
}

/** Clears any persisted session for a tenant. */
export function clearSession(tenant: string, store: SessionStore | null = defaultStore()): void {
  store?.removeItem(storageKey(tenant));
}

/**
 * The delay, in milliseconds, before the access token should be silently refreshed: a skew (default 60s) ahead of
 * expiry so a renewed token is always in hand before the old one lapses. Clamped to a non-negative value, so an
 * already-expired token refreshes immediately.
 */
export function refreshDelayMs(expiresAtIso: string, nowMs: number, skewMs = 60_000): number {
  const expiry = Date.parse(expiresAtIso);
  if (Number.isNaN(expiry)) return skewMs;
  return Math.max(0, expiry - nowMs - skewMs);
}
