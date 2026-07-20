import { useState } from "react";
import type { Credentials } from "../lib/useSession";

/**
 * The credential sign-in panel. It collects a tenant id, user name and password and hands them to the session
 * controller, surfacing a failed attempt inline. The tenant id defaults to the demo seed tenant; production resolves
 * a tenant slug to its id server-side, so an operator would not normally type one.
 */
export default function LoginPanel({
  brandName,
  defaultTenantId,
  signingIn,
  onSubmit,
  onCancel,
}: {
  brandName: string;
  defaultTenantId: string;
  signingIn: boolean;
  onSubmit: (credentials: Credentials) => Promise<void>;
  onCancel?: () => void;
}) {
  const [tenantId, setTenantId] = useState(defaultTenantId);
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      await onSubmit({ tenantId, userName, password });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Sign-in failed.");
    }
  }

  const field =
    "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-brand focus:ring-1 focus:ring-brand dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100";
  const label = "block text-xs font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400";

  return (
    <div className="grid h-full place-items-center p-6">
      <form
        onSubmit={handleSubmit}
        className="w-full max-w-sm space-y-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-800"
      >
        <div>
          <h1 className="text-lg font-semibold text-slate-800 dark:text-slate-100">Sign in to {brandName}</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Your permissions come from the signed token — the navigation shows exactly what your role allows.
          </p>
        </div>

        <div className="space-y-1">
          <label className={label} htmlFor="login-tenant">
            Tenant ID
          </label>
          <input id="login-tenant" className={field} value={tenantId} onChange={(e) => setTenantId(e.target.value)} />
        </div>

        <div className="space-y-1">
          <label className={label} htmlFor="login-user">
            User name
          </label>
          <input
            id="login-user"
            className={field}
            value={userName}
            autoComplete="username"
            onChange={(e) => setUserName(e.target.value)}
          />
        </div>

        <div className="space-y-1">
          <label className={label} htmlFor="login-password">
            Password
          </label>
          <input
            id="login-password"
            type="password"
            className={field}
            value={password}
            autoComplete="current-password"
            onChange={(e) => setPassword(e.target.value)}
          />
        </div>

        {error && (
          <div className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-500/10 dark:text-red-300">
            {error}
          </div>
        )}

        <div className="flex items-center gap-2">
          <button
            type="submit"
            disabled={signingIn || userName.length === 0 || password.length === 0}
            className="flex-1 rounded-lg bg-brand px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            {signingIn ? "Signing in…" : "Sign in"}
          </button>
          {onCancel && (
            <button
              type="button"
              onClick={onCancel}
              className="rounded-lg px-3 py-2 text-sm text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200"
            >
              Cancel
            </button>
          )}
        </div>
      </form>
    </div>
  );
}
