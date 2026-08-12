import type { ButtonHTMLAttributes, ReactNode } from "react";
import { cx } from "./cx";
import { buttonStyles } from "./recipes";

export type ButtonVariant = keyof typeof buttonStyles.variant;
export type ButtonSize = keyof typeof buttonStyles.size;

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  startIcon?: ReactNode;
  endIcon?: ReactNode;
  isLoading?: boolean;
}
export function Button({ className, variant = "primary", size = "md", startIcon, endIcon, isLoading, disabled, children, type = "button", ...props }: ButtonProps) {
  return (
    <button type={type} className={cx(buttonStyles.base, buttonStyles.size[size], buttonStyles.variant[variant], className)} disabled={disabled || isLoading} aria-busy={isLoading || undefined} {...props}>
      {isLoading ? <span className="size-4 animate-spin rounded-full border-2 border-current border-r-transparent" aria-hidden="true" /> : startIcon}
      <span>{children}</span>
      {!isLoading && endIcon}
    </button>
  );
}
