/**
 * Whether a held permission set grants a required permission, honoring the same wildcard convention the gateway
 * enforces server-side: `*` grants everything, `resource.*` grants every action on a module, and an exact
 * `resource.action` grants only itself (case-insensitively). A `null` held set means unrestricted (no identity was
 * resolved) and grants everything — so the UI mirrors additive RBAC. This is a UX aid only: the gateway is the real
 * authority and re-checks every write, so a spoofed client can hide a button but never perform the action.
 */
export function grants(held: readonly string[] | null, required: string): boolean {
  if (held === null) return true;
  return held.some((grant) => matches(grant, required));
}

function matches(grant: string, required: string): boolean {
  if (grant === "*") return true;

  const g = grant.toLowerCase();
  const r = required.toLowerCase();
  if (g === r) return true;

  if (g.endsWith(".*")) {
    // "resource." — a boundary match so "energy.*" grants "energy.view" but not "energyx.view".
    return r.startsWith(g.slice(0, -1));
  }

  return false;
}

/** Builds a `holds` predicate over a held permission set, for passing into screens. */
export function permitFor(held: readonly string[] | null): (permission: string) => boolean {
  return (permission) => grants(held, permission);
}
