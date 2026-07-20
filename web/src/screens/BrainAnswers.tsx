import { useState } from "react";
import { GatewayClient } from "../api/client";
import { useAsync } from "../lib/useAsync";
import { timeAgo } from "../lib/format";
import { Badge, Card, ErrorNote, Loading } from "../components/ui";

/** brain/Answers — the Company Brain's grounded Q&A. Asking publishes an event; the answer arrives async. */
export default function BrainAnswers({ client }: { client: GatewayClient }) {
  const [reload, setReload] = useState(0);
  const { data, error, loading } = useAsync(() => client.brainAnswers(), [client, reload]);
  const [draft, setDraft] = useState("");
  const [pending, setPending] = useState<string | null>(null);

  async function ask(e: React.FormEvent) {
    e.preventDefault();
    const question = draft.trim();
    if (!question) return;
    setDraft("");
    setPending(question);
    try {
      await client.askBrain(question, "user:operator");
      // The grounded answer is produced asynchronously (RAG + LLM over the bus); refetch shortly after.
      window.setTimeout(() => {
        setPending(null);
        setReload((n) => n + 1);
      }, 1500);
    } catch {
      setPending(null);
    }
  }

  if (loading) return <Loading />;
  if (error) return <ErrorNote message={error} />;
  if (!data) return null;

  return (
    <div className="space-y-4">
      <Card title="Ask the Company Brain">
        <form className="flex gap-2" onSubmit={ask}>
          <input
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder="Why did press-1 spike last night?"
            className="flex-1 rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm dark:border-slate-600 dark:bg-slate-900"
          />
          <button
            type="submit"
            disabled={pending !== null}
            className="rounded-lg bg-brand px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {pending ? "Asking…" : "Ask"}
          </button>
        </form>
        {pending && (
          <p className="mt-2 text-xs text-slate-400">
            Grounding “{pending}” against the knowledge base — the answer will appear below.
          </p>
        )}
      </Card>

      <Card title="Recent answers">
        <ul className="space-y-4">
          {pending && (
            <li className="rounded-lg border border-dashed border-slate-200 p-3 dark:border-slate-700">
              <div className="flex items-center justify-between gap-2">
                <div className="text-sm font-medium text-slate-500 dark:text-slate-400">{pending}</div>
                <Badge tone="neutral">thinking…</Badge>
              </div>
              <p className="mt-1 animate-pulse text-sm text-slate-400">Retrieving grounded context…</p>
            </li>
          )}
          {data.answers.map((a) => (
            <li key={a.sourceEventId} className="rounded-lg border border-slate-100 p-3 dark:border-slate-700">
              <div className="flex items-center justify-between gap-2">
                <div className="text-sm font-medium text-slate-700 dark:text-slate-200">{a.question}</div>
                <Badge tone="neutral">{a.model}</Badge>
              </div>
              <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{a.answer}</p>
              <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-slate-400">
                <span>{timeAgo(a.answeredAt)}</span>
                {a.citations.map((c) => (
                  <span key={c.chunkId} className="rounded bg-slate-100 px-1.5 py-0.5 dark:bg-slate-700">
                    {c.source}
                  </span>
                ))}
              </div>
            </li>
          ))}
          {data.answers.length === 0 && !pending && (
            <p className="text-sm text-slate-400">No answers yet — ask something.</p>
          )}
        </ul>
      </Card>
    </div>
  );
}
