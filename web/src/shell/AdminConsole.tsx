import { useState } from "react";
import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { stateTone } from "../lib/format";
import { Badge, Card, ErrorNote, Loading, StatTile } from "../components/ui";

/**
 * The platform admin console — the operator of the OS, not the factory floor. It reads the gateway's
 * platform and marketplace surface: `/system` (identity + rollup), `/store/summary` (health headline) and
 * `/store/plugins` (the installed plugins with dependency satisfaction). Built purely from discovery data.
 */
export default function AdminConsole({ client }: { client: GatewayClient }) {
  const [reload, setReload] = useState(0);
  const [busy, setBusy] = useState<string | null>(null);
  const system = useAsync(() => client.system(), [client, reload]);
  const summary = useAsync(() => client.storeSummary(), [client, reload]);
  const catalog = useAsync(() => client.storeCatalog(), [client, reload]);

  async function toggle(key: string, enabled: boolean) {
    setBusy(key);
    try {
      await client.setPluginEnabled(key, enabled);
      setReload((n) => n + 1); // refetch system + store so dependency health re-resolves everywhere
    } finally {
      setBusy(null);
    }
  }

  if (system.loading || summary.loading || catalog.loading) return <Loading />;
  const err = system.error ?? summary.error ?? catalog.error;
  if (err) return <ErrorNote message={err} />;
  if (!system.data || !summary.data || !catalog.data) return null;

  const s = system.data;

  return (
    <div className="mx-auto max-w-5xl space-y-5 p-5">
      <div>
        <h1 className="text-lg font-semibold text-slate-800 dark:text-slate-100">System</h1>
        <p className="text-xs text-slate-400">
          {s.product} · v{s.version}
        </p>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatTile label="Modules active" value={`${s.modulesActive}/${s.modulesInstalled}`} />
        <StatTile label="Needs attention" value={s.pluginsNeedingAttention} hint="unmet dependency" />
        <StatTile label="Capabilities" value={s.capabilities.length} />
        <StatTile label="Event types" value={s.eventTypes} hint="flowing on the bus" />
      </div>

      <Card title="Marketplace — installed plugins" actions={<Badge tone="muted">{summary.data.total} installed</Badge>}>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-xs uppercase tracking-wide text-slate-400">
              <tr>
                <th className="py-2 pr-4">Plugin</th>
                <th className="py-2 pr-4">Version</th>
                <th className="py-2 pr-4">State</th>
                <th className="py-2 pr-4">Dependencies</th>
                <th className="py-2 pr-4"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
              {catalog.data.plugins.map((p) => (
                <tr key={p.key}>
                  <td className="py-2 pr-4">
                    <div className="font-medium text-slate-700 dark:text-slate-200">{p.name}</div>
                    <div className="text-xs text-slate-400">{p.key}</div>
                  </td>
                  <td className="py-2 pr-4 text-slate-500">{p.version}</td>
                  <td className="py-2 pr-4">
                    <Badge tone={stateTone(p.state)}>{p.state}</Badge>
                  </td>
                  <td className="py-2 pr-4">
                    {p.dependencies.length === 0 ? (
                      <span className="text-xs text-slate-400">none</span>
                    ) : (
                      <div className="flex flex-wrap gap-1">
                        {p.dependencies.map((d) => (
                          <Badge key={d.pluginKey} tone={d.satisfied ? "ok" : "bad"}>
                            {d.pluginKey} {d.satisfied ? "✓" : "✗"}
                          </Badge>
                        ))}
                      </div>
                    )}
                  </td>
                  <td className="py-2 pr-4 text-right">
                    {p.state.toLowerCase() !== "failed" && (
                      <button
                        disabled={busy === p.key}
                        onClick={() => toggle(p.key, p.state.toLowerCase() === "disabled")}
                        className={`rounded-md px-2.5 py-1 text-xs font-medium disabled:opacity-50 ${
                          p.state.toLowerCase() === "disabled"
                            ? "bg-brand text-white"
                            : "border border-slate-300 text-slate-600 dark:border-slate-600 dark:text-slate-300"
                        }`}
                      >
                        {busy === p.key ? "…" : p.state.toLowerCase() === "disabled" ? "Enable" : "Disable"}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}
