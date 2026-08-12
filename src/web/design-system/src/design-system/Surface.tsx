import type { HTMLAttributes } from "react";
import { cx } from "./cx";
import { surfaceStyles } from "./recipes";

type SurfaceProps = HTMLAttributes<HTMLDivElement> & {
  radius?: keyof typeof surfaceStyles.radius;
  elevation?: keyof typeof surfaceStyles.elevation;
};
export function Surface({ className, radius = "lg", elevation = "flat", ...props }: SurfaceProps) {
  return <div className={cx(surfaceStyles.base, surfaceStyles.radius[radius], surfaceStyles.elevation[elevation], className)} {...props} />;
}

export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <Surface className={cx("p-5 sm:p-6", className)} {...props} />;
}
