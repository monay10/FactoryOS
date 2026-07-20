// Small pure formatting helpers, unit-tested so the screens stay declarative.

/** Formats a 0..1 fraction as a whole-number percentage, e.g. 0.824 -> "82%". */
export function percent(fraction: number): string {
  return `${Math.round(fraction * 100)}%`;
}

/** A compact "time ago" label from an ISO instant, relative to `now` (defaults to the current time). */
export function timeAgo(iso: string, now: Date = new Date()): string {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return "";
  const seconds = Math.max(0, Math.round((now.getTime() - then) / 1000));
  if (seconds < 60) return "just now";
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  return `${days}d ago`;
}

/** Maps an alert level to a semantic badge tone. */
export function levelTone(level: string): "critical" | "warning" | "ok" | "neutral" {
  switch (level.toLowerCase()) {
    case "critical":
      return "critical";
    case "warning":
      return "warning";
    case "info":
      // A positive/resolving signal (e.g. a work order closed) — surfaced calmly, not as a concern.
      return "ok";
    default:
      return "neutral";
  }
}

/** Maps a plugin lifecycle state to a semantic tone for the admin console. */
export function stateTone(state: string): "ok" | "muted" | "bad" {
  switch (state.toLowerCase()) {
    case "started":
      return "ok";
    case "failed":
      return "bad";
    default:
      return "muted";
  }
}
