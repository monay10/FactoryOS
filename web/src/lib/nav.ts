import type { NavCatalog, NavItem } from "../api/types";

/** Flattens the section-grouped nav into a single ordered list of items. */
export function flattenNav(nav: NavCatalog): NavItem[] {
  return nav.sections.flatMap((section) => section.items);
}

/** The screen the shell opens on first load — the first item of the first section, or null if empty. */
export function firstScreen(nav: NavCatalog): NavItem | null {
  return flattenNav(nav)[0] ?? null;
}

/** Finds a nav item by its route (the shell's client-side selection key). */
export function findByRoute(nav: NavCatalog, route: string): NavItem | null {
  return flattenNav(nav).find((item) => item.route === route) ?? null;
}
