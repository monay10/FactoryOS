import { useState } from "react";
import { useAsync } from "../lib/useAsync";
import { percent } from "../lib/format";
import type { ScreenProps } from "./registry";
import { Badge, Card, ErrorNote, Loading } from "../components/ui";

/** quality/Dashboard — per-line/product defect rates, flagged where they breach, with a permission-guarded quarantine. */
export default function QualityLines({ client, holds }: ScreenProps) {
  const [reloadKey, setReloadKey] = useState(0);
  const [pending, setPending] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const { data, error, loading } = useAsync(() => client.qualityLines(), [client, reloadKey]);
  const canQuarantine = holds("quality.quarantine");

  async function quarantine(lineId: string) {
    setPending(lineId);
    setActionError(null);
    try {
      await client.quarantineLine(lineId);
      setReloadKey((k) => k + 1);
    } catch (cause) {
      setActionError(cause instanceof Error ? cause.message : "Quarantine failed.");
    } finally {
      setPending(null);
    }
  }

  if (loading) return <Loading />;
  if (error) return <ErrorNote message={error} />;
  if (!data) return null;

  const breaching = data.lines.filter((l) => l.breachesThreshold).length;

  return (
    <Card
      title="Quality by line"
      actions={<Badge tone={breaching > 0 ? "warning" : "ok"}>{breaching} breaching</Badge>}
    >
      {actionError && (
        <div className="mb-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-500/10 dark:text-red-300">
          {actionError}
        </div>
      )}
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead className="text-xs uppercase tracking-wide text-slate-400">
            <tr>
              <th className="py-2 pr-4">Line / product</th>
              <th className="py-2 pr-4 text-right">Inspected</th>
              <th className="py-2 pr-4 text-right">Defective</th>
              <th className="py-2 pr-4 text-right">Defect rate</th>
              {canQuarantine && <th className="py-2 pr-4 text-right">Action</th>}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
            {data.lines.map((l) => (
              <tr key={`${l.lineId}/${l.productId}`}>
                <td className="py-2 pr-4">
                  <span className="font-medium text-slate-700 dark:text-slate-200">{l.lineId}</span>
                  <span className="text-slate-400"> · {l.productId}</span>
                  {l.quarantined && (
                    <span className="ml-2 align-middle">
                      <Badge tone="warning">Quarantined</Badge>
                    </span>
                  )}
                </td>
                <td className="py-2 pr-4 text-right tabular-nums text-slate-500">{l.inspectedUnits}</td>
                <td className="py-2 pr-4 text-right tabular-nums text-slate-500">{l.defectiveUnits}</td>
                <td className="py-2 pr-4 text-right">
                  <Badge tone={l.breachesThreshold ? "critical" : "ok"}>{percent(l.defectRate)}</Badge>
                </td>
                {canQuarantine && (
                  <td className="py-2 pr-4 text-right">
                    {l.quarantined ? (
                      <span className="text-xs text-slate-400">—</span>
                    ) : (
                      <button
                        onClick={() => quarantine(l.lineId)}
                        disabled={pending === l.lineId}
                        className="rounded-lg border border-slate-200 px-3 py-1 text-xs font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-50 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-700"
                      >
                        {pending === l.lineId ? "Holding…" : "Quarantine"}
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {data.lines.length === 0 && <p className="mt-2 text-sm text-slate-400">No inspections recorded yet.</p>}
    </Card>
  );
}
