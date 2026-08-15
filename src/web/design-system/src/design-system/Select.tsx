import type { SelectHTMLAttributes } from "react";
import { cx } from "./cx";
import { controlBase } from "./recipes";

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  invalid?: boolean;
}

// Styled native <select> (Field.tsx's Input sibling): native semantics keep keyboard/screen-reader
// behavior free, appearance-none only swaps the OS affordance for a themed chevron.
export function Select({ className, invalid, children, ...props }: SelectProps) {
  return (
    <span className="relative block">
      <select
        className={cx(
          controlBase,
          "h-11 appearance-none px-3.5 py-2.5 pr-9 text-sm",
          invalid && "border-error-500 focus-visible:border-error-500 focus-visible:ring-error-500/15",
          className,
        )}
        aria-invalid={invalid || undefined}
        {...props}
      >
        {children}
      </select>
      <svg
        aria-hidden="true"
        viewBox="0 0 20 20"
        fill="none"
        className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-gray-500 dark:text-gray-400"
      >
        <path d="M5 7.5 10 12.5 15 7.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </span>
  );
}
