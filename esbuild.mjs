import { build } from "esbuild";

await build({
  entryPoints: ["extension/src/extension.ts"],
  bundle: true,
  external: ["vscode"],
  format: "cjs",
  logLevel: "info",
  outfile: "dist/extension.cjs",
  platform: "node",
  sourcemap: true,
  target: "node20",
});
