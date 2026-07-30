import { useState } from "react";
import { GatewayClient } from "../api/client";
import type { PlatformPlugin } from "../api/types";
import { useAsync } from "../lib/useAsync";
import { Badge, Card, ErrorNote, Loading, StatTile } from "../components/ui";
import AuditTrail from "./AuditTrail";

/** Maps a runtime status to a badge tone. Running is good, Failed is bad, a switched-off state is muted. */
function statusTone(status: string): string {
  switch (status.toLowerCase()) {
    case "running":
      return "ok";
    case "failed":
      return "bad";
    case "suspended":
    case "updating":
      return "warning";
    default:
      return "neutral";
  }
}

/** The lifecycle actions offered for a plugin in a given status. Order is the order they are shown. */
function actionsFor(status: string): { verb: string; label: string }[] {
  const s = status.toLowerCase();
  const actions: { verb: string; label: string }[] = [];
  if (s === "loaded" || s === "stopped" || s === "installed") actions.push({ verb: "start", label: "Start" });
  if (s === "running") actions.push({ verb: "stop", label: "Stop" }, { verb: "suspend", label: "Suspend" });
  if (s === "suspended") actions.push({ verb: "resume", label: "Resume" }, { verb: "stop", label: "Stop" });
  if (["running", "suspended", "loaded", "stopped"].includes(s)) actions.push({ verb: "unload", label: "Unload" });
  return actions;
}

/**
 * The platform runtime console — install a plugin for the tenant and drive its lifecycle over the /platform
 * management API. Distinct from the AdminConsole's older /store marketplace view: this operates the plugin
 * runtime directly. Every write is authorized by the caller's permissions; a refusal surfaces its own message.
 */
export default function PlatformConsole({ client }: { client: GatewayClient }) {
  const [reload, setReload] = useState(0);
  const [busy, setBusy] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const plugins = useAsync(() => client.platformPlugins(), [client, reload]);
  const packages = useAsync(() => client.platformPackages(), [client, reload]);

  async function run(token: string, operation: () => Promise<unknown>) {
    setBusy(token);
    setActionError(null);
    try {
      await operation();
      setReload((n) => n + 1);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(null);
    }
  }

  if (plugins.loading || packages.loading) return <Loading label="Reading the plugin runtime…" />;
  const err = plugins.error ?? packages.error;
  if (err) return <ErrorNote message={err} />;
  if (!plugins.data || !packages.data) return null;

  const installed: PlatformPlugin[] = plugins.data;
  const installedKeys = new Set(installed.map((p) => p.key));
  const available = packages.data.filter((pkg) => !installedKeys.has(pkg.key));
  const running = installed.filter((p) => p.status.toLowerCase() === "running").length;

  return (
    <div className="mx-auto max-w-5xl space-y-5 p-5">
      <div>
        <h1 className="text-lg font-semibold text-slate-800 dark:text-slate-100">Plugin runtime</h1>
        <p className="text-xs text-slate-400">Install, run and remove plugins for this tenant, at run time.</p>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        <StatTile label="Installed" value={installed.length} />
        <StatTile label="Running" value={running} />
        <StatTile label="Available" value={available.length} hint="discoverable packages" />
      </div>

      {actionError && <ErrorNote message={actionError} />}

      <Card title="Installed plugins" actions={<Badge tone="muted">{installed.length} installed</Badge>}>
        {installed.length === 0 ? (
          <p className="text-sm text-slate-400">Nothing installed yet — install a package below.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-400">
                <tr>
                  <th className="py-2 pr-4">Plugin</th>
                  <th className="py-2 pr-4">Version</th>
                  <th className="py-2 pr-4">Status</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
                {installed.map((plugin) => (
                  <tr key={plugin.key}>
                    <td className="py-2 pr-4">
                      <div className="font-medium text-slate-700 dark:text-slate-200">{plugin.key}</div>
                      {plugin.failureReason && <div className="text-xs text-red-500">{plugin.failureReason}</div>}
                    </td>
                    <td className="py-2 pr-4 text-slate-500">
                      {plugin.version}
                      {plugin.previousVersion && (
                        <span className="text-xs text-slate-400"> (was {plugin.previousVersion})</span>
                      )}
                    </td>
                    <td className="py-2 pr-4">
                      <Badge tone={statusTone(plugin.status)}>{plugin.status}</Badge>
                      {!plugin.enabled && (
                        <span className="ml-1">
                          <Badge tone="muted">off</Badge>
                        </span>
                      )}
                    </td>
                    <td className="py-2 pr-4">
                      <div className="flex flex-wrap justify-end gap-1.5">
                        {actionsFor(plugin.status).map((action) => (
                          <button
                            key={action.verb}
                            disabled={busy !== null}
                            onClick={() => run(`${plugin.key}:${action.verb}`, () =>
                              client.pluginLifecycle(plugin.key, action.verb))}
                            className="rounded-md border border-slate-300 px-2.5 py-1 text-xs font-medium text-slate-600 disabled:opacity-50 dark:border-slate-600 dark:text-slate-300"
                          >
                            {busy === `${plugin.key}:${action.verb}` ? "…" : action.label}
                          </button>
                        ))}
                        <button
                          disabled={busy !== null}
                          onClick={() => run(`${plugin.key}:remove`, () => client.removePlugin(plugin.key))}
                          className="rounded-md px-2.5 py-1 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:text-red-400 dark:hover:bg-red-500/10"
                        >
                          {busy === `${plugin.key}:remove` ? "…" : "Remove"}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <Card title="Available packages" actions={<Badge tone="muted">{available.length} available</Badge>}>
        {available.length === 0 ? (
          <p className="text-sm text-slate-400">No packages left to install.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-400">
                <tr>
                  <th className="py-2 pr-4">Package</th>
                  <th className="py-2 pr-4">Version</th>
                  <th className="py-2 pr-4">Requests</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
                {available.map((pkg) => (
                  <tr key={`${pkg.key}@${pkg.version}`}>
                    <td className="py-2 pr-4">
                      <div className="font-medium text-slate-700 dark:text-slate-200">{pkg.key}</div>
                      <div className="text-xs text-slate-400">{pkg.signed ? "signed" : "unsigned"}</div>
                    </td>
                    <td className="py-2 pr-4 text-slate-500">{pkg.version}</td>
                    <td className="py-2 pr-4">
                      {pkg.requested.length === 0 ? (
                        <span className="text-xs text-slate-400">none</span>
                      ) : (
                        <div className="flex flex-wrap gap-1">
                          {pkg.requested.map((permission) => (
                            <Badge key={permission} tone="neutral">
                              {permission}
                            </Badge>
                          ))}
                        </div>
                      )}
                    </td>
                    <td className="py-2 pr-4 text-right">
                      <button
                        disabled={busy !== null}
                        onClick={() => run(`${pkg.key}:install`, () =>
                          client.installPlugin(pkg.key, pkg.version, pkg.requested))}
                        className="rounded-md bg-brand px-2.5 py-1 text-xs font-medium text-white disabled:opacity-50"
                      >
                        {busy === `${pkg.key}:install` ? "…" : "Install"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <AuditTrail client={client} />
    </div>
  );
}
