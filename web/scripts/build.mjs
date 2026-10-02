import { build } from "esbuild";
import { readFile, writeFile } from "node:fs/promises";
await build({
  entryPoints: ["src/app.mjs"], bundle: true, minify: true,
  outfile: "dist/app.js", format: "esm", target: "es2022",
  legalComments: "linked", sourcemap: false,
});
const notices = [
  ["Pillowcase Link Converter — MIT", "LICENSE"],
  ["linkify-it 5.0.0 — MIT", "node_modules/linkify-it/LICENSE"],
  ["uc.micro 2.1.0 — MIT", "node_modules/.pnpm/uc.micro@2.1.0/node_modules/uc.micro/LICENSE.txt"],
  ["idb 8.0.3 — ISC", "node_modules/idb/LICENSE"],
];
await writeFile("dist/THIRD-PARTY-LICENSES.txt", (await Promise.all(notices.map(async ([title, file]) => `${title}\n${"=".repeat(title.length)}\n\n${await readFile(file, "utf8")}`))).join("\n\n"));
console.log("Browser bundle ready; all dependencies are hosted locally.");
