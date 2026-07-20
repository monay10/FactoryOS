import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { percent } from "../lib/format";
import { Card, ErrorNote, Loading } from "../components/ui";

function Factor({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex-1">
      <div className="flex justify-between text-xs text-slate-400">
        <span>{label}</span>
        <span className="tabular-nums">{percent(value)}</span>
      </div>
      <div className="mt-1 h-1.5 rounded bg-slate-200 dark:bg-slate-700">
        <div className="h-full rounded bg-brand" style={{ width: percent(value) }} />
      </div>
    </div>
  );
}

/** oee/Dashboard — OEE per machine-period with its Availability × Performance × Quality breakdown. */
export default function OeeSnapshots({ client }: { client: GatewayClient }) {
  const { data, error, loading } = useAsync(() => client.oeeSnapshots(), [client]);

  if (loading) return <Loading />;
  if (error) return <ErrorNote message={error} />;
  if (!data) return null;

  return (
    <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
      {data.snapshots.map((s, i) => (
        <Card key={`${s.machineId}-${i}`} title={s.machineId}>
          <div className="flex items-baseline justify-between">
            <div className="text-3xl font-bold tabular-nums text-slate-800 dark:text-slate-100">{percent(s.oee)}</div>
            <span className={`text-xs font-semibold ${s.meetsTarget ? "text-emerald-600" : "text-amber-600"}`}>
              {s.meetsTarget ? "on target" : "below target"}
            </span>
          </div>
          <div className="mt-3 flex gap-3">
            <Factor label="Availability" value={s.availability} />
            <Factor label="Performance" value={s.performance} />
            <Factor label="Quality" value={s.quality} />
          </div>
        </Card>
      ))}
      {data.snapshots.length === 0 && <p className="text-sm text-slate-400">No OEE snapshots yet.</p>}
    </div>
  );
}
