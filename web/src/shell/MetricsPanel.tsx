import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { Badge, Card, ErrorNote, Loading, StatTile } from "../components/ui";

/**
 * The monitoring engine's panel — its lifetime counters and the metric series registered on the host. Counters
 * accumulate as the platform runs (operating a plugin, for instance, feeds the collector). Bridge faults are
 * called out on their own: a non-zero count means a cross-engine integration is misbehaving and deserves a look.
 */
export default function MetricsPanel({ client }: { client: GatewayClient }) {
  const report = useAsync(() => client.platformMetrics(), [client]);

  if (report.loading) return <Loading label="Reading the monitoring engine…" />;
  if (report.error) return <ErrorNote message={report.error} />;
  if (!report.data) return null;

  const m = report.data;
  const activeAlerts = Math.max(0, m.alertsTriggered - m.alertsResolved);

  return (
    <Card
      title="Monitoring"
      actions={
        m.bridgeFaults > 0 ? (
          <Badge tone="critical">{m.bridgeFaults} bridge faults</Badge>
        ) : (
          <Badge tone="muted">{m.definitions.length} series</Badge>
        )
      }
    >
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatTile label="Collected" value={m.collected} hint="samples in" />
        <StatTile label="Sampled" value={m.sampled} hint="points stored" />
        <StatTile label="Active alerts" value={activeAlerts} hint={`${m.alertsTriggered} raised · ${m.alertsResolved} cleared`} />
        <StatTile label="Threshold breaches" value={m.thresholdBreaches} />
      </div>

      <div className="mt-4">
        {m.definitions.length === 0 ? (
          <p className="text-sm text-slate-400">No metric series registered yet.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-400">
                <tr>
                  <th className="py-2 pr-4">Metric</th>
                  <th className="py-2 pr-4">Category</th>
                  <th className="py-2 pr-4">Kind</th>
                  <th className="py-2 pr-4">Unit</th>
                  <th className="py-2 pr-4">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
                {m.definitions.map((definition) => (
                  <tr key={definition.key}>
                    <td className="whitespace-nowrap py-2 pr-4 font-medium text-slate-700 dark:text-slate-200">
                      {definition.key}
                    </td>
                    <td className="whitespace-nowrap py-2 pr-4 text-slate-500">{definition.category}</td>
                    <td className="whitespace-nowrap py-2 pr-4 text-slate-500">{definition.kind}</td>
                    <td className="whitespace-nowrap py-2 pr-4 text-slate-500">{definition.unit}</td>
                    <td className="py-2 pr-4 text-slate-600 dark:text-slate-300">{definition.description}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Card>
  );
}
