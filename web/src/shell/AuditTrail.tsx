import { useState } from "react";
import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { auditSeverityTone, timeAgo } from "../lib/format";
import { Badge, Card, ErrorNote, Loading } from "../components/ui";

/** Saves a blob to the user's downloads as `fileName`, via a transient object URL. */
function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

/**
 * The tenant's audit trail — the platform's immutable, hash-chained record of what it did, newest first. The
 * header carries the verdict of verifying that chain: a broken chain means a record was altered, removed or
 * reordered, and is surfaced loudly. The trail is durable, so it reflects activity from before the last restart.
 */
export default function AuditTrail({ client }: { client: GatewayClient }) {
  const report = useAsync(() => client.platformAudit(), [client]);
  const [busy, setBusy] = useState<string | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);

  async function download(format: "csv" | "json") {
    setBusy(format);
    setExportError(null);
    try {
      saveBlob(await client.platformAuditExport(format), `audit.${format}`);
    } catch (err) {
      setExportError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(null);
    }
  }

  if (report.loading) return <Loading label="Reading the audit trail…" />;
  if (report.error) return <ErrorNote message={report.error} />;
  if (!report.data) return null;

  const { chainValid, verified, records } = report.data;
  const verdict = (
    <div className="flex items-center gap-2">
      {chainValid ? (
        <Badge tone="ok">Chain verified · {verified}</Badge>
      ) : (
        <Badge tone="critical">Chain broken</Badge>
      )}
      {(["csv", "json"] as const).map((format) => (
        <button
          key={format}
          disabled={busy !== null}
          onClick={() => download(format)}
          className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-600 disabled:opacity-50 dark:border-slate-600 dark:text-slate-300"
        >
          {busy === format ? "…" : format.toUpperCase()}
        </button>
      ))}
    </div>
  );

  return (
    <Card title="Audit trail" actions={verdict}>
      {exportError && (
        <div className="mb-3">
          <ErrorNote message={exportError} />
        </div>
      )}
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
