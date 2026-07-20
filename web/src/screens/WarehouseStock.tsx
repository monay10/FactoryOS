import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { Badge, Card, ErrorNote, Loading } from "../components/ui";

/** warehouse/Dashboard — stock on hand per SKU, flagged where it is at or below the reorder point. */
export default function WarehouseStock({ client }: { client: GatewayClient }) {
  const { data, error, loading } = useAsync(() => client.warehouseStock(), [client]);

  if (loading) return <Loading />;
  if (error) return <ErrorNote message={error} />;
  if (!data) return null;

  const low = data.items.filter((i) => i.belowReorder).length;

  return (
    <Card title="Stock on hand" actions={<Badge tone={low > 0 ? "warning" : "ok"}>{low} below reorder</Badge>}>
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead className="text-xs uppercase tracking-wide text-slate-400">
            <tr>
              <th className="py-2 pr-4">SKU</th>
              <th className="py-2 pr-4">Warehouse</th>
              <th className="py-2 pr-4 text-right">On hand</th>
              <th className="py-2 pr-4 text-right">Reorder point</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
            {data.items.map((i) => (
              <tr key={`${i.warehouseId}/${i.sku}`} className={i.belowReorder ? "bg-amber-50/60 dark:bg-amber-500/5" : ""}>
                <td className="py-2 pr-4 font-medium text-slate-700 dark:text-slate-200">{i.sku}</td>
                <td className="py-2 pr-4 text-slate-500">{i.warehouseId}</td>
                <td className="py-2 pr-4 text-right tabular-nums">
                  {i.belowReorder ? <Badge tone="warning">{i.onHand}</Badge> : <span className="text-slate-600 dark:text-slate-300">{i.onHand}</span>}
                </td>
                <td className="py-2 pr-4 text-right tabular-nums text-slate-500">{i.reorderPoint ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {data.items.length === 0 && <p className="mt-2 text-sm text-slate-400">No stock tracked yet.</p>}
    </Card>
  );
}
