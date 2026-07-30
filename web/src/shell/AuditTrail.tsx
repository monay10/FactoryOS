import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { auditSeverityTone, timeAgo } from "../lib/format";
import { Badge, Card, ErrorNote, Loading } from "../components/ui";

/**
 * The tenant's audit trail — the platform's immutable, hash-chained record of what it did, newest first. The
 * header carries the verdict of verifying that chain: a broken chain means a record was altered, removed or
 * reordered, and is surfaced loudly. The trail is durable, so it reflects activity from before the last restart.
 */
export default function AuditTrail({ client }: { client: GatewayClient }) {
  const report = useAsync(() => client.platformAudit(), [client]);

  if (report.loading) return <Loading label="Reading the audit trail…" />;
  if (report.error) return <ErrorNote message={report.error} />;
  if (!report.data) return null;

  const { chainValid, verified, records } = report.data;
  const verdict = chainValid ? (
    <Badge tone="ok">Chain verified · {verified}</Badge>
  ) : (
    <Badge tone="critical">Chain broken</Badge>
  );

  return (
    <Card title="Audit trail" actions={verdict}>
      {records.length === 0 ? (
        <p className="text-sm text-slate-400">Nothing audited yet — install or operate a plugin to see records here.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-xs uppercase tracking-wide text-slate-400">
              <tr>
                <th className="py-2 pr-4">When</th>
                <th className="py-2 pr-4">Severity</th>
                <th className="py-2 pr-4">What</th>
                <th className="py-2 pr-4">Actor</th>
                <th className="py-2 pr-4">Detail</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
              {records.map((record) => (
                <tr key={record.sequence}>
                  <td className="whitespace-nowrap py-2 pr-4 text-slate-500">{timeAgo(record.occurredOnUtc)}</td>
                  <td className="py-2 pr-4">
                    <Badge tone={auditSeverityTone(record.severity)}>{record.severity}</Badge>
                    {record.result.toLowerCase() !== "success" && (
                      <span className="ml-1">
                        <Badge tone="warning">{record.result}</Badge>
                      </span>
                    )}
                  </td>
                  <td className="whitespace-nowrap py-2 pr-4 text-slate-600 dark:text-slate-300">
                    <span className="text-slate-400">{record.category}</span> · {record.action}
                  </td>
                  <td className="whitespace-nowrap py-2 pr-4 text-slate-500">{record.actor}</td>
                  <td className="py-2 pr-4 text-slate-600 dark:text-slate-300">{record.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}
