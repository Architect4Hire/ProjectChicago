import type { HTMLAttributes } from "react";
import { cx } from "./cx";

export type BadgeTone = "brand" | "success" | "warning" | "error" | "gray";

const toneStyles: Record<BadgeTone, string> = {
  brand: "bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-400",
  success: "bg-success-50 text-success-700 dark:bg-success-500/15 dark:text-success-400",
  warning: "bg-warning-50 text-warning-700 dark:bg-warning-500/15 dark:text-warning-400",
  error: "bg-error-50 text-error-700 dark:bg-error-500/15 dark:text-error-400",
  gray: "bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300",
};

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone;
}

// Status/priority indicator. Text is always the meaning-carrying element (tone is
// reinforcement only), so no status may be communicated by this component's color alone.
export function Badge({ tone = "gray", className, children, ...props }: BadgeProps) {
  return (
    <span
      className={cx(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium",
        toneStyles[tone],
        className,
      )}
      {...props}
    >
      {children}
    </span>
  );
}
