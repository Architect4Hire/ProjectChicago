import type { InputHTMLAttributes, ReactNode } from "react";
import { cx } from "./cx";
import { controlBase } from "./recipes";
export function Field({ label, hint, error, required, children }: { label: string; hint?: string; error?: string; required?: boolean; children: ReactNode }) {
  return <label className="block text-sm font-medium text-gray-700 dark:text-gray-300">
    <span>{label}{required && <span className="ml-0.5 text-error-500" aria-hidden="true">*</span>}</span>
    <span className="mt-1.5 block">{children}</span>
    {(error || hint) && <span className={cx("mt-1.5 block text-xs", error ? "text-error-600" : "text-gray-500 dark:text-gray-400")}>{error || hint}</span>}
  </label>;
}
export function Input({ className, invalid, ...props }: InputHTMLAttributes<HTMLInputElement> & { invalid?: boolean }) {
  return <input className={cx(controlBase, "h-11 px-3.5 py-2.5 text-sm", invalid && "border-error-500 focus-visible:border-error-500 focus-visible:ring-error-500/15", className)} aria-invalid={invalid || undefined} {...props} />;
}
