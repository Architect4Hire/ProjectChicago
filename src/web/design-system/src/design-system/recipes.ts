export const focusRing =
  "outline-none focus-visible:ring-4 focus-visible:ring-brand-500/15 focus-visible:border-brand-500";

export const controlBase =
  `w-full rounded-lg border border-gray-300 bg-white text-gray-900 shadow-theme-xs transition-colors placeholder:text-gray-400 disabled:cursor-not-allowed disabled:bg-gray-100 disabled:text-gray-500 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90 dark:disabled:bg-gray-800 ${focusRing}`;
export const buttonStyles = {
  base: `inline-flex items-center justify-center gap-2 rounded-lg font-medium transition-[background-color,border-color,color,box-shadow,transform] duration-150 active:translate-y-px disabled:pointer-events-none disabled:opacity-50 ${focusRing}`,
  size: {
    sm: "min-h-9 px-3 py-2 text-sm",
    md: "min-h-11 px-4 py-2.5 text-sm",
    lg: "min-h-12 px-5 py-3 text-base",
  },
  variant: {
    primary: "bg-brand-500 text-white shadow-theme-xs hover:bg-brand-600",
    secondary: "bg-gray-900 text-white shadow-theme-xs hover:bg-gray-800 dark:bg-white dark:text-gray-900 dark:hover:bg-gray-100",
    outline: "border border-gray-300 bg-white text-gray-700 shadow-theme-xs hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-white/5",
    ghost: "text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-white/5",
    danger: "bg-error-600 text-white shadow-theme-xs hover:bg-error-700",
  },
} as const;
export const surfaceStyles = {
  base: "border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900",
  radius: { sm: "rounded-lg", md: "rounded-xl", lg: "rounded-2xl" },
  elevation: { flat: "", raised: "shadow-theme-sm", overlay: "shadow-theme-xl" },
} as const;
