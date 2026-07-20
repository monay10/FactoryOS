import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { timeAgo } from "../lib/format";
import { Badge, Card, ErrorNote, Loading } from "../components/ui";

/** activity/Feed — the per-tenant factory timeline, newest first, across every producing module. */
export default function ActivityFeed({ client }: { client: GatewayClient }) {
  const { data, error, loading } = useAsync(() => client.activityFeed(), [client]);

  if (loading) return <Loading />;
  if (error) return <ErrorNote message={error} />;
  if (!data) return null;

  return (
    <Card title="Factory timeline">
      <ol className="relative space-y-3 border-l border-slate-200 pl-4 dark:border-slate-700">
        {data.entries.map((e) => (
          <li key={e.sourceEventId} className="relative">
            <span className="absolute -left-[21px] top-1.5 h-2 w-2 rounded-full bg-brand" />
            <div className="flex items-center gap-2">
              <Badge tone="neutral">{e.category}</Badge>
              <span className="text-xs text-slate-400">{timeAgo(e.occurredAt)}</span>
            </div>
            <p className="mt-1 text-sm text-slate-700 dark:text-slate-200">{e.headline}</p>
          </li>
        ))}
        {data.entries.length === 0 && <p className="text-sm text-slate-400">Nothing on the timeline yet.</p>}
      </ol>
    </Card>
  );
}
