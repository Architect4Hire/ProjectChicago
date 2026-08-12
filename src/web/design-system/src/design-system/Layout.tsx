import type { HTMLAttributes, ReactNode } from "react";
import { cx } from "./cx";
export function Stack({ className, ...props }: HTMLAttributes<HTMLDivElement>) { return <div className={cx("flex flex-col gap-5", className)} {...props} />; }
export function Cluster({ className, ...props }: HTMLAttributes<HTMLDivElement>) { return <div className={cx("flex flex-wrap items-center gap-3", className)} {...props} />; }
export function Grid({ className, ...props }: HTMLAttributes<HTMLDivElement>) { return <div className={cx("grid gap-5", className)} {...props} />; }
export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
    <div><h1 className="text-title-sm font-semibold text-gray-900 dark:text-white">{title}</h1>{description && <p className="mt-1 max-w-3xl text-sm text-gray-500 dark:text-gray-400">{description}</p>}</div>
    {actions && <div className="flex shrink-0 flex-wrap gap-3">{actions}</div>}
  </header>;
}
