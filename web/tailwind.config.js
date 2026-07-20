/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        // Bound at runtime from tenant branding (Sprint 103); these are the neutral defaults.
        brand: {
          DEFAULT: "var(--brand-primary, #2563eb)",
          soft: "var(--brand-soft, #dbeafe)",
        },
      },
    },
  },
  plugins: [],
};
