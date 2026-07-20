import { useMemo, useState } from "react";
import { GatewayClient } from "../api/client";
import type { ShellBootstrap } from "../api/types";
import { findByRoute, firstScreen } from "../lib/nav";
import { resolveScreen } from "../screens/registry";
import { Card } from "../components/ui";

/**
 * The customer/operator shell. Its navigation is built entirely from the gateway's `/modules/ui/nav`
 * (delivered inside `/shell`) — section headings and screens are data. The main pane renders the selected
 * screen from the registry; a module the shell has never heard of still appears in the nav.
 */
export default function OperatorShell({
  client,
  bootstrap,
  holds,
}: {
  client: GatewayClient;
  bootstrap: ShellBootstrap;
  holds: (permission: string) => boolean;
}) {
  const [route, setRoute] = useState<string | null>(() => firstScreen(bootstrap.nav)?.route ?? null);
  const active = useMemo(() => (route ? findByRoute(bootstrap.nav, route) : null), [bootstrap.nav, route]);
  const Screen = active ? resolveScreen(active.component) : null;

  return (
    <div className="flex h-full">
      <nav className="w-60 shrink-0 overflow-y-auto border-r border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-800">
        {bootstrap.nav.sections.map((section) => (
          <div key={section.section} className="mb-4">
            <div className="px-2 pb-1 text-xs font-semibold uppercase tracking-wide text-slate-400">
              {section.section || "General"}
            </div>
            {section.items.map((item) => (
              <button
                key={item.route}
                onClick={() => setRoute(item.route)}
                className={`block w-full rounded-lg px-3 py-2 text-left text-sm ${
                  item.route === route
                    ? "bg-brand-soft font-medium text-brand"
                    : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-700"
                }`}
              >
                {item.title}
                <span className="ml-1 text-xs text-slate-400">· {item.module}</span>
              </button>
            ))}
          </div>
        ))}
      </nav>

      <main className="flex-1 overflow-y-auto bg-slate-50 p-5 dark:bg-slate-900">
        {active && (
          <div className="mb-4">
            <h1 className="text-lg font-semibold text-slate-800 dark:text-slate-100">{active.title}</h1>
            <p className="text-xs text-slate-400">
              {active.module} · {active.route}
            </p>
          </div>
        )}
        {Screen ? (
          <Screen client={client} holds={holds} />
        ) : active ? (
          <Card title={active.title}>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              This screen ships in the <span className="font-medium">{active.module}</span> plugin bundle
              (<code className="text-xs">{active.component}</code>) and lazy-loads when installed.
            </p>
          </Card>
        ) : (
          <p className="text-sm text-slate-400">No screens are available for this tenant yet.</p>
        )}
      </main>
    </div>
  );
}
