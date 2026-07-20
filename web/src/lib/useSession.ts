import { useCallback, useEffect, useRef, useState } from "react";
import { login as loginRequest, refresh as refreshRequest } from "../api/auth";
import {
  clearSession,
  DEMO_TENANT_ID,
  loadSession,
  refreshDelayMs,
  saveSession,
  toSession,
  type Session,
} from "./session";

/** Credentials collected by the login form. */
export interface Credentials {
  tenantId: string;
  userName: string;
  password: string;
}

/** The session surface exposed to the shell: current session, sign-in/out actions, and whether a sign-in is pending. */
export interface SessionController {
  session: Session | null;
  signingIn: boolean;
  signIn: (credentials: Credentials) => Promise<void>;
  signOut: () => void;
  defaultTenantId: string;
}

/**
 * Owns the SPA's authenticated session for a tenant: it restores a persisted session on load, exchanges credentials
 * for one via <c>/auth/login</c>, and silently renews the short-lived access token via <c>/auth/refresh</c> a minute
 * before it expires. A failed renewal ends the session (the user signs in again) rather than leaving a dead token in
 * play. The session is scoped per tenant, so switching factories never carries one identity into another.
 */
export function useSession(tenant: string): SessionController {
  const [session, setSession] = useState<Session | null>(() => loadSession(tenant));
  const [signingIn, setSigningIn] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Re-anchor on the persisted session whenever the tenant changes.
  useEffect(() => {
    setSession(loadSession(tenant));
  }, [tenant]);

  const apply = useCallback((next: Session | null) => {
    setSession(next);
    if (next) saveSession(next);
  }, []);

  const signOut = useCallback(() => {
    clearSession(tenant);
    setSession(null);
  }, [tenant]);

  const signIn = useCallback(
    async (credentials: Credentials) => {
      setSigningIn(true);
      try {
        const response = await loginRequest(tenant, credentials);
        apply(toSession(tenant, credentials.userName, response));
      } finally {
        setSigningIn(false);
      }
    },
    [tenant, apply],
  );

  // Silent renewal: schedule a refresh a skew ahead of expiry; a failure ends the session.
  useEffect(() => {
    if (timer.current) clearTimeout(timer.current);
    if (!session) return;

    const delay = refreshDelayMs(session.expiresAt, Date.now());
    timer.current = setTimeout(() => {
      void (async () => {
        try {
          const response = await refreshRequest(session.tenant, session.refreshToken);
          apply(toSession(session.tenant, session.userName, response));
        } catch {
          clearSession(session.tenant);
          setSession(null);
        }
      })();
    }, delay);

    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, [session, apply]);

  return { session, signingIn, signIn, signOut, defaultTenantId: DEMO_TENANT_ID };
}
