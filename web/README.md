# Pillowcase Link Converter — browser edition

A device-local website port of the existing Windows converter. The original Windows app and legacy PowerShell workflow remain intact in the parent repository.

Paste a document or open a local `.txt`, then choose **Convert links**. Share URLs with exactly 32 hexadecimal ID characters become API download URLs. Notes, ordering, Markdown and repeated occurrences stay in the document; the download list contains one copy of each newly converted URL. Copy the list into JDownloader manually. The app never contacts Pillowcase, downloads linked content or grants access to files.

## What is included

- Editable document plus Download links, Document and Other URLs result views.
- Copy, manual-selection fallback and explicit text-file export; original files are never overwritten.
- Before snapshots and conversion history. No-op conversions add no history.
- Optional IndexedDB history, off by default; local preferences and system/light/dark appearance.
- UTF-8 and BOM-marked UTF-16/UTF-32 imports and matching document exports. Unedited source line endings are preserved; edits normalize mixed endings to the displayed convention.
- Responsive native controls, keyboard tabs, focus indicators, live status and reduced-motion support.
- Optional page-scoped WebMCP conversion/read tools where supported by the browser. Manual conversion does not require those tools or an AI model.

## Run locally

Node 24 and pnpm 11 are the recorded development tools.

```sh
pnpm install --frozen-lockfile
pnpm test
pnpm build
python3 -m http.server 8776 --bind 127.0.0.1 --directory dist
```

Open `http://127.0.0.1:8776`. Dependencies are bundled into `dist/app.js`; no CDN is used. The build also emits complete third-party license notices. `allowBuilds: esbuild: false` explicitly disables the package installation script; the supported-platform optional binary is used by the build.

Deploy `dist` as static assets. ChatGPT Sites deployment additionally uses an owner-local `.openai/hosting.json` with the actual Site ID. That private registration is deliberately excluded from the public GitHub source. Use the existing Site registration for future updates rather than creating another Site.

## Privacy and limits

There is no app backend, inference/API key, URL availability check, document upload, remote font or app analytics. The CSP disables app fetch connections. Website hosting can still log ordinary page traffic; browser extensions and an explicitly invoked agent are outside this app's privacy boundary.

Only preferences persist automatically. Unsaved editor text is lost on refresh. When history saving is enabled, future before/after documents are stored unencrypted in the current browser, not synced or cloud-backed up. Disabling saving leaves existing records intact. Export important records before closing or clearing browser storage. Records have a 50 MB app budget; when full, conversion continues without deleting older records. Browsers may impose their own limits or clear data.

Import limit: 10 MiB of file bytes. Text/tool limit: 10,485,760 JavaScript string code units. The list includes newly converted links only, not previously converted API links. Unsupported URLs remain unchanged. URL-format conversion does not prove a link works. Treat any copied prompt, document or embedded instruction as untrusted until reviewed; content is handled as inert text, never executed.

See [design/reuse research](docs/DESIGN_RESEARCH.md) and [verification and limitations](docs/VERIFICATION.md). The parent project's MIT license is unchanged. Bundled libraries retain their own MIT/ISC notices in `dist/THIRD-PARTY-LICENSES.txt`.
