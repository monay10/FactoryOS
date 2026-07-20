import { useEffect, useMemo, useState } from "react";
import { GatewayClient } from "./api/client";
import { useAsync } from "./lib/useAsync";
import { useSession } from "./lib/useSession";
import { permitFor } from "./lib/permissions";
import { applyBranding } from "./lib/branding";
import OperatorShell from "./shell/OperatorShell";
import AdminConsole from "./shell/AdminConsole";
import LoginPanel from "./components/LoginPanel";
import { ErrorNote, Loading } from "./components/ui";

type Area = "operator" | "admin";

/** Resolves the tenant from the URL (?tenant=acme) with a dev default, so a single build serves any factory. */
function resolveTenant(): string {
  const fromUrl = new URLSearchParams(window.location.search).get("tenant");
  return fromUrl && fromUrl.trim().length > 0 ? fromUrl.trim() : "acme";
}

/** Resolves the caller's permissions from the URL (?perms=a,b). Absent → null → unrestricted (RBAC is additive). */
function resolvePermissions(): string[] | null {
  const raw = new URLSearchParams(window.location.search).get("perms");
  if (raw === null) return null;
  return raw.split(",").map((p) => p.trim()).filter((p) => p.length > 0);
}

export default function App() {
  const tenant = useMemo(resolveTenant, []);
  const permissions = useMemo(resolvePermissions, []);
  const { session, signingIn, signIn, signOut, defaultTenantId } = useSession(tenant);
  // A dev ?token= still works for a bare host; a real signed session always wins over it and over the ?perms= fallback.
  const urlToken = useMemo(() => new URLSearchParams(window.location.search).get("token"), []);
  const accessToken = session?.accessToken ?? urlToken;

  const client = useMemo(
    // A signed session drives permissions from its token's claims, so the dev ?perms= fallback only applies when
    // signed out. Rebuilds whenever the access token rotates so a renewed token takes effect immediately.
    () => new GatewayClient(tenant, fetch, accessToken ? null : permissions, accessToken),
    [tenant, permissions, accessToken],
  );
  // What the caller effectively holds: a signed session's own permissions, else the dev ?perms= set, else unrestricted.
  const holds = useMemo(
    () => permitFor(session ? session.permissions : permissions),
    [session, permissions],
  );
  const [area, setArea] = useState<Area>("operator");
  const [showLogin, setShowLogin] = useState(false);
  const { data: bootstrap, error, loading } = useAsync(() => client.shell(), [client]);

  useEffect(() => {
    if (bootstrap) applyBranding(bootstrap.branding);
  }, [bootstrap]);

  // Close the login panel once a session is in hand.
  useEffect(() => {
    if (session) setShowLogin(false);
  }, [session]);

  const brandName = bootstrap?.branding.displayName ?? tenant;
  const logoUrl = bootstrap?.branding.logoUrl ?? null;

  return (
    <div className="flex h-full flex-col bg-slate-50 dark:bg-slate-900">
      <header className="flex items-center justify-between border-b border-slate-200 bg-white px-4 py-2.5 dark:border-slate-700 dark:bg-slate-800">
        <div className="flex items-center gap-2">
          {logoUrl ? (
            <img src={logoUrl} alt={brandName} className="h-7 w-7 rounded-lg object-cover" />
          ) : (
            <span className="grid h-7 w-7 place-items-center rounded-lg bg-brand text-sm font-bold text-white">
              {brandName.charAt(0).toUpperCase()}
            </span>
          )}
          <span className="font-semibold text-slate-800 dark:text-slate-100">{brandName}</span>
          <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-500 dark:bg-slate-700 dark:text-slate-300">
            FactoryOS · {tenant}
          </span>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex rounded-lg bg-slate-100 p-0.5 text-sm dark:bg-slate-700">
            {(["operator", "admin"] as Area[]).map((a) => (
              <button
                key={a}
                onClick={() => setArea(a)}
                className={`rounded-md px-3 py-1 capitalize ${
                  area === a ? "bg-white text-brand shadow-sm dark:bg-slate-800" : "text-slate-500 dark:text-slate-300"
                }`}
              >
                {a}
              </button>
            ))}
          </div>
          {session ? (
            <div className="flex items-center gap-2 text-sm">
              <span className="text-slate-500 dark:text-slate-300">{session.userName}</span>
              <button
                onClick={signOut}
                className="rounded-lg border border-slate-200 px-3 py-1 text-slate-600 hover:bg-slate-50 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-700"
              >
                Sign out
              </button>
            </div>
          ) : (
            <button
              onClick={() => setShowLogin(true)}
              className="rounded-lg bg-brand px-3 py-1 text-sm font-semibold text-white"
            >
              Sign in
            </button>
          )}
        </div>
      </header>

      <div className="min-h-0 flex-1">
        {showLogin && !session ? (
          <LoginPanel
            brandName={brandName}
            defaultTenantId={defaultTenantId}
            signingIn={signingIn}
            onSubmit={signIn}
            onCancel={() => setShowLogin(false)}
          />
        ) : (
          <>
            {loading && <Loading label="Bootstrapping the shell…" />}
            {error && (
              <div className="p-5">
                <ErrorNote message={error} />
              </div>
            )}
            {bootstrap &&
              (area === "operator" ? (
                <OperatorShell client={client} bootstrap={bootstrap} holds={holds} />
              ) : (
                <AdminConsole client={client} />
              ))}
          </>
        )}
      </div>
    </div>
  );
}
