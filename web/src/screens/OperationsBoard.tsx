import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { levelTone, percent, timeAgo } from "../lib/format";
import { Badge, Card, ErrorNote, Loading, StatTile } from "../components/ui";

/** dashboard/OperationsBoard — the wall board: latest OEE per machine and the live alert feed. */
export default function OperationsBoard({ client }: { client: GatewayClient }) {
  const { data, error, loading } = useAsync(() => client.dashboardBoard(), [client]);

  if (loading) return <Loading />;
  if (error) return <ErrorNote message={error} />;
  if (!data) return null;

  const belowTarget = data.machines.filter((m) => !m.meetsTarget).length;

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatTile label="Machines" value={data.machines.length} />
        <StatTile label="Below target" value={belowTarget} />
        <StatTile label="Alerts" value={data.alerts.length} />
        <StatTile label="Critical" value={data.criticalAlertCount} hint="needs attention now" />
      </div>

      <Card title="Machine OEE">
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {data.machines.map((m) => (
            <div
              key={m.machineId}
              className={`rounded-lg border p-3 ${
                m.meetsTarget
                  ? "border-emerald-200 bg-emerald-50 dark:border-emerald-500/30 dark:bg-emerald-500/10"
                  : "border-amber-200 bg-amber-50 dark:border-amber-500/30 dark:bg-amber-500/10"
              }`}
            >
              <div className="text-sm font-medium text-slate-600 dark:text-slate-300">{m.machineId}</div>
              <div className="mt-1 text-2xl font-bold text-slate-800 dark:text-slate-100">{percent(m.oee)}</div>
              <div className="text-xs text-slate-400">{m.meetsTarget ? "on target" : "below target"}</div>
            </div>
          ))}
          {data.machines.length === 0 && <p className="text-sm text-slate-400">No machines reporting yet.</p>}
        </div>
      </Card>

      <Card title="Live alerts">
        <ul className="divide-y divide-slate-100 dark:divide-slate-700">
          {data.alerts.map((a, i) => (
            <li key={i} className="flex items-center justify-between gap-3 py-2">
              <div className="min-w-0">
                <div className="truncate text-sm text-slate-700 dark:text-slate-200">{a.subject}</div>
                <div className="text-xs text-slate-400">
                  {a.kind} · {timeAgo(a.occurredAt)}
                </div>
              </div>
              <Badge tone={levelTone(a.level)}>{a.level}</Badge>
            </li>
          ))}
          {data.alerts.length === 0 && <p className="text-sm text-slate-400">All clear.</p>}
        </ul>
      </Card>
    </div>
  );
}
