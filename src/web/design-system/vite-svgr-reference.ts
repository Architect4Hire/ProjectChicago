// Merge this plugin configuration into Project Chicago's existing vite.config.ts.
// Do NOT replace the existing Vite configuration wholesale.
import svgr from "vite-plugin-svgr";

export const pcdsSvgrPlugin = svgr({
  svgrOptions: { exportType: "named", ref: true, svgo: false, titleProp: true },
  include: "**/*.svg?react",
});
