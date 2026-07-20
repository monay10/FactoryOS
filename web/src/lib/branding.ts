import type { TenantBranding } from "../api/types";

/**
 * Applies a tenant's branding to the document: binds the primary theme color (and a soft tint derived from it)
 * to the CSS variables Tailwind's `brand` colors read, and sets the browser theme-color. Called once the shell
 * bootstraps, so a single build themes itself per tenant (Law 6) with no rebuild.
 */
export function applyBranding(branding: TenantBranding): void {
  const root = document.documentElement;
  const primary = branding.primaryColor?.trim();
  if (primary) {
    root.style.setProperty("--brand-primary", primary);
    // A light tint of the primary for selected-nav backgrounds, computed with color-mix.
    root.style.setProperty("--brand-soft", `color-mix(in srgb, ${primary} 14%, white)`);
    const meta = document.querySelector('meta[name="theme-color"]');
    meta?.setAttribute("content", primary);
  }
}
