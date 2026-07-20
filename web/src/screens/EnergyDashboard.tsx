import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { timeAgo } from "../lib/format";
import { Badge, Card, ErrorNote, Loading } from "../components/ui";

function delta(value: number): { tone: string; text: string } {
  const sign = value > 0 ? "+" : "";
  const tone = value >= 25 ? "critical" : value > 0 ? "warning" : "ok";
  return { tone, text: `${sign}${value}%` };
}

/** energy/Dashboard — live per-meter readings against their rolling baseline, plus a recent-spike feed. */
export default function EnergyDashboard({ client }: { client: GatewayClient }) {
  const meters = useAsync(() => client.energyMeters(), [client]);
  const spikes = useAsync(() => client.energySpikes(), [client]);

  if (meters.loading || spikes.loading) return <Loading />;
  const err = meters.error ?? spikes.error;
  if (err) return <ErrorNote message={err} />;
  if (!meters.data || !spikes.data) return null;

  return (
    <div className="space-y-4">
      <Card title="Meters" actions={<Badge tone="neutral">{meters.data.meters.length} tracked</Badge>}>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {meters.data.meters.map((m) => {
            const d = delta(m.deltaPercent);
            return (
              <div key={`${m.meterId}/${m.metric}`} className="rounded-lg border border-slate-200 p-3 dark:border-slate-700">
                <div className="flex items-center justify-between">
                  <span className="font-mono text-xs text-slate-500">{m.meterId}</span>
                  <Badge tone={d.tone}>{d.text}</Badge>
                </div>
                <div className="mt-1 text-2xl font-bold tabular-nums text-slate-800 dark:text-slate-100">
                  {m.value}
                  <span className="ml-1 text-sm font-normal text-slate-400">{m.unit}</span>
                </div>
                <div className="text-xs text-slate-400">
                  {m.metric} · baseline {m.baseline}
                  {m.unit}
                </div>
              </div>
            );
          })}
          {meters.data.meters.length === 0 && <p className="text-sm text-slate-400">No meters reporting yet.</p>}
        </div>
      </Card>

      <Card title="Recent spikes">
        <ul className="divide-y divide-slate-100 dark:divide-slate-700">
          {spikes.data.spikes.map((s, i) => (
            <li key={i} className="flex items-center justify-between gap-3 py-2">
              <div className="min-w-0">
                <div className="truncate text-sm text-slate-700 dark:text-slate-200">
                  {s.meterId}: {s.metric} {s.value}
                  {s.unit} vs baseline {s.baseline}
                  {s.unit}
                </div>
                <div className="text-xs text-slate-400">{timeAgo(s.readingAt)}</div>
              </div>
              <Badge tone={s.deltaPercent >= 25 ? "critical" : "warning"}>+{s.deltaPercent}%</Badge>
            </li>
          ))}
          {spikes.data.spikes.length === 0 && <p className="text-sm text-slate-400">No spikes — consumption is within baseline.</p>}
        </ul>
      </Card>
    </div>
  );
}
