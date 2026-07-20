import { useState } from "react";
import { useAsync } from "../lib/useAsync";
import type { ScreenProps } from "./registry";
import { Badge, Card, ErrorNote, Loading } from "../components/ui";

function isClosed(status: string): boolean {
  switch (status.toLowerCase()) {
    case "completed":
    case "closed":
    case "done":
      return true;
    default:
      return false;
  }
}

function statusTone(status: string): string {
  if (isClosed(status)) return "ok";
  switch (status.toLowerCase()) {
    case "inprogress":
    case "in_progress":
      return "warning";
    default:
      return "neutral";
  }
}

/** maintenance/WorkOrders — a tenant's maintenance work orders with status, and a permission-guarded close action. */
export default function WorkOrders({ client, holds }: ScreenProps) {
  const [reloadKey, setReloadKey] = useState(0);
  const [closing, setClosing] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const { data, error, loading } = useAsync(() => client.workOrders(), [client, reloadKey]);
  const canClose = holds("maintenance.close");

  async function close(number: string) {
    setClosing(number);
    setActionError(null);
    try {
      await client.closeWorkOrder(number);
      setReloadKey((k) => k + 1);
    } catch (cause) {
      setActionError(cause instanceof Error ? cause.message : "Close failed.");
    } finally {
      setClosing(null);
    }
  }

  if (loading) return <Loading />;
  if (error) return <ErrorNote message={error} />;
  if (!data) return null;

  const open = data.workOrders.filter((w) => !isClosed(w.status)).length;

  return (
    <Card title="Work orders" actions={<Badge tone="neutral">{open} open items</Badge>}>
      {actionError && (
        <div className="mb-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-500/10 dark:text-red-300">
          {actionError}
        </div>
      )}
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead className="text-xs uppercase tracking-wide text-slate-400">
            <tr>
              <th className="py-2 pr-4">Order</th>
              <th className="py-2 pr-4">Asset</th>
              <th className="py-2 pr-4">Status</th>
              <th className="py-2 pr-4">Due</th>
              {canClose && <th className="py-2 pr-4 text-right">Action</th>}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
            {data.workOrders.map((w) => (
              <tr key={w.number}>
                <td className="py-2 pr-4">
                  <div className="font-medium text-slate-700 dark:text-slate-200">{w.title}</div>
                  <div className="text-xs text-slate-400">{w.number}</div>
                </td>
                <td className="py-2 pr-4 text-slate-500">{w.assetCode ?? "—"}</td>
                <td className="py-2 pr-4">
                  <Badge tone={statusTone(w.status)}>{w.status}</Badge>
                </td>
                <td className="py-2 pr-4 text-slate-500">
                  {w.dueAt ? new Date(w.dueAt).toLocaleDateString() : "—"}
                </td>
                {canClose && (
                  <td className="py-2 pr-4 text-right">
                    {isClosed(w.status) ? (
                      <span className="text-xs text-slate-400">—</span>
                    ) : (
                      <button
                        onClick={() => close(w.number)}
                        disabled={closing === w.number}
                        className="rounded-lg border border-slate-200 px-3 py-1 text-xs font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-50 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-700"
                      >
                        {closing === w.number ? "Closing…" : "Close"}
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {data.workOrders.length === 0 && <p className="mt-2 text-sm text-slate-400">No open work orders.</p>}
    </Card>
  );
}
