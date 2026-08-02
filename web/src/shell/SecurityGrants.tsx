import { useState } from "react";
import { GatewayClient } from "../api/client";
import type { SecurityGrants as SecurityGrantsView } from "../api/types";
import { useAsync } from "../lib/useAsync";
import { Badge, Card } from "../components/ui";

/**
 * The security-grants panel — browse and change the permissions granted directly to subjects in this tenant. The
 * roster lists every subject that holds a grant, so an operator need not know a subject to find one; picking one loads
 * it into the editor. Managing grants is an authenticated, permission-gated action: a caller without
 * `security.read`/`security.grant` is refused with a message it surfaces. Grants shown are a subject's own, not the
 * permissions its roles carry.
 */
export default function SecurityGrants({ client }: { client: GatewayClient }) {
  const [subject, setSubject] = useState("");
  const [permission, setPermission] = useState("");
  const [view, setView] = useState<SecurityGrantsView | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const roster = useAsync(() => client.securityRoster(), [client, reload]);

  async function run(token: string, operation: () => Promise<SecurityGrantsView | void>) {
    setBusy(token);
    setError(null);
    try {
      const result = await operation();
      if (result) setView(result);
      setReload((n) => n + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(null);
    }
  }

  const loaded = view?.subject.trim().toLowerCase() === subject.trim().toLowerCase() ? view : null;

  function load(who: string) {
    setSubject(who);
    if (who.trim()) run("load", () => client.securityGrants(who.trim()));
  }

  async function grant() {
    const who = subject.trim();
    const what = permission.trim();
    if (who && what) {
      await run("grant", () => client.grantPermission(who, what));
      setPermission("");
    }
  }

  function revoke(what: string) {
    const who = subject.trim();
    // Revoke returns no body, so re-read to reflect the change.
    run(`revoke:${what}`, async () => {
      await client.revokePermission(who, what);
      return client.securityGrants(who);
    });
  }

  return (
    <Card title="Security grants" actions={<Badge tone="muted">tenant-scoped</Badge>}>
      <p className="mb-3 text-xs text-slate-400">
        The permissions granted directly to a subject — not the ones its roles carry. Managing grants needs the
        <span className="font-medium"> security.grant</span> permission.
      </p>

      <div>
        <div className="text-xs uppercase tracking-wide text-slate-400">Subjects with grants</div>
        {roster.loading ? (
          <p className="mt-1 text-sm text-slate-400">Reading the roster…</p>
        ) : roster.error ? (
          <p className="mt-1 text-sm text-red-500">{roster.error}</p>
        ) : !roster.data || roster.data.subjects.length === 0 ? (
          <p className="mt-1 text-sm text-slate-400">No subjects hold a direct grant yet.</p>
        ) : (
          <div className="mt-1 flex flex-wrap gap-1.5">
            {roster.data.subjects.map((s) => (
              <button
                key={s.subject}
                disabled={busy !== null}
                onClick={() => load(s.subject)}
                className={`rounded-md border px-2 py-1 text-xs font-medium disabled:opacity-50 ${
                  loaded?.subject === s.subject
                    ? "border-brand text-brand"
                    : "border-slate-300 text-slate-600 dark:border-slate-600 dark:text-slate-300"
                }`}
              >
                {s.subject}
                <span className="ml-1 text-slate-400">{s.grants.length}</span>
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="mt-4 flex flex-wrap items-end gap-2">
        <label className="flex flex-col text-xs text-slate-500">
          Subject
          <input
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && load(subject)}
            placeholder="user:alice"
            className="mt-1 rounded-md border border-slate-300 px-2 py-1 text-sm text-slate-700 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200"
          />
        </label>
        <button
          disabled={busy !== null || subject.trim() === ""}
          onClick={() => load(subject)}
          className="rounded-md border border-slate-300 px-2.5 py-1 text-xs font-medium text-slate-600 disabled:opacity-50 dark:border-slate-600 dark:text-slate-300"
        >
          {busy === "load" ? "…" : "Load"}
        </button>
      </div>

      {error && <p className="mt-3 text-sm text-red-500">{error}</p>}

      {loaded && (
        <div className="mt-4 space-y-3">
          <div>
            <div className="text-xs uppercase tracking-wide text-slate-400">Grants for {loaded.subject}</div>
            {loaded.grants.length === 0 ? (
              <p className="mt-1 text-sm text-slate-400">No direct grants.</p>
            ) : (
              <div className="mt-1 flex flex-wrap gap-1.5">
                {loaded.grants.map((g) => (
                  <span key={g} className="inline-flex items-center gap-1">
                    <Badge tone="neutral">{g}</Badge>
                    <button
                      disabled={busy !== null}
                      onClick={() => revoke(g)}
                      title={`Revoke ${g}`}
                      className="text-xs text-red-500 hover:text-red-600 disabled:opacity-50"
                    >
                      {busy === `revoke:${g}` ? "…" : "×"}
                    </button>
                  </span>
                ))}
              </div>
            )}
          </div>

          <div className="flex flex-wrap items-end gap-2">
            <label className="flex flex-col text-xs text-slate-500">
              Grant a permission
              <input
                value={permission}
                onChange={(e) => setPermission(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && grant()}
                placeholder="energy.read"
                className="mt-1 rounded-md border border-slate-300 px-2 py-1 text-sm text-slate-700 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200"
              />
            </label>
            <button
              disabled={busy !== null || permission.trim() === ""}
              onClick={grant}
              className="rounded-md bg-brand px-2.5 py-1 text-xs font-medium text-white disabled:opacity-50"
            >
              {busy === "grant" ? "…" : "Grant"}
            </button>
          </div>
        </div>
      )}
    </Card>
  );
}
