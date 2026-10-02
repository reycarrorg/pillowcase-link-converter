# Browser-port research and production decisions

Research date: October 2, 2026. This is a working decision record, not a claim that the underlying Pillowcase API is stable or that every browser has been tested.

## Existing product before new production

Baseline: [pillowcase-link-converter at 1ed89bf](https://github.com/reycarrorg/pillowcase-link-converter/tree/1ed89bfb5094d1cb0d5829e5974887ec857cbed5), especially Core.cs, MainWindow.cs and CoreTests.cs. The current C# editor is the behavioral foundation; the older PowerShell URL-only script is retained, not substituted for it. Preserve in-document replacements, repetition, stable deduplication only in the copy list, before snapshots and no-op history behavior. Do not publish a user's real link lists as demo data.

Two independent research lanes reviewed conversion/reuse/privacy and interface/production/accessibility. Requested configurations: GPT-6.1 Sol / High for parsing, file-format and storage decisions; GPT-6.1 Sol / Medium for design patterns. No background campaigns, paid services or nested research managers were started. This records requested settings, not an independent backend attestation.

## Reuse the difficult parts

| Concern | Selected foundation | Why / boundary |
| --- | --- | --- |
| Text conversion contract | Existing repository core | Preserve the established user workflow rather than replace it with a different product. |
| Whole URL boundaries | [linkify-it 5.0.0](https://github.com/markdown-it/linkify-it) | Source offsets and punctuation-aware URL recognition. Disable fuzzy/email/FTP matches; apply our exact host/path/ID allowlist after tokenization. MIT. |
| Reliable optional history | [idb 8.0.3](https://github.com/jakearchibald/idb) | Promise-oriented IndexedDB and explicit transaction completion, not a homegrown wrapper. ISC. |
| Static dependency bundling | [esbuild 0.25.12](https://esbuild.github.io/) | Small ESM output, local assets, no runtime CDN. Build-only MIT dependency. |
| Hosting | [ChatGPT Sites](https://learn.chatgpt.com/docs/sites) | Owner-private initial publication; no database, authentication integration or paid inference needed for text transformation. |
| Controls and exports | Browser textarea/dialog/Blob APIs | Mature native behavior; avoid a framework, rich-text editor or file-export library that adds little here. |

`uc.micro 2.1.0` is linkify-it's MIT-licensed Unicode dependency. Full license notices ship with the bundle. Rich Markdown rendering, jsdiff, FileSaver.js, a backend, a scraper and an AI inference call were evaluated as unnecessary for this scope. This is a browser utility, not a downloader or a Pillowcase bypass.

## Targeted improvements over copying the existing parser

1. Trailing sentence punctuation should remain outside the URL while a valid ID still converts.
2. A share URL embedded inside another host's query must not be converted as though it were a standalone link.
3. Reject lookalike hosts, credentials, ports and malformed IDs; leave their text unchanged.
4. Retain query/fragment/path suffixes in the document while the copied download list uses the base API URL, matching existing intent.

These are covered by focused fixtures and unit tests. No linked host was contacted to establish endpoint availability.

## Interface and production practices

The workbench shows source and results together on desktop, stacks on narrow screens, and keeps one conversion action primary. Counts explain replacements versus unique links. Other URLs are surfaced without treating them as broken downloads. Session and device history are labeled differently. In-place output is editable only on the source side; stale results cannot be copied/exported until reconverted.

- [GOV.UK textarea guidance](https://design-system.service.gov.uk/components/textarea/) informs labeled plain-text entry rather than a decorative upload funnel.
- [WAI tabs pattern](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/) informs selection, arrow keys, Home/End and focus management.
- [WCAG status messages](https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html) informs concise live feedback; entire pasted documents are not live-announced.
- [WCAG reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html), [focus not obscured](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html) and [target size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) inform responsive layout, visible focus and adequately sized actions. They are design criteria, not a completed conformance audit.
- [Carbon notification guidelines](https://carbondesignsystem.com/components/notification/usage/) inform in-context success/warning messages rather than transient-only toasts.
- [MDN reduced motion](https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-reduced-motion) informs disabling nonessential transitions.

System fonts, an ink/teal palette, modest borders and native controls keep the tool recognizable and readable without external images or decorative dependencies. Light/dark modes use shared semantic tokens. Functional icons are local and do not need a paid generation pipeline.

### Visual refinement: OpenAI clarity / Apple precision

The second design pass replaces the initial ink/teal theme with cool porcelain and neutral graphite, a unified split document workbench, compact introductory copy, rounded capsule actions, segmented result controls, consistent functional SVGs and grouped native settings. Restrained materials are limited to chrome/backdrop; document surfaces remain legible and opaque. Main content remains immediately accessible. No Apple or OpenAI logo, proprietary typeface, affiliation claim, generated artwork or new UI dependency is introduced.

The existing design specialist supplied one bounded read-only consultation using [OpenAI brand/design guidance](https://openai.com/brand/), [Apple materials guidance](https://developer.apple.com/design/human-interface-guidelines/materials) and [Apple motion guidance](https://developer.apple.com/design/human-interface-guidelines/motion). These inform the direction rather than prescribe a pixel-identical branded clone. Native controls, the system font stack, the existing local icon geometry and the installed converter/storage libraries are reused. Motion is brief and functional; reduced-motion and reduced-transparency preferences have CSS fallbacks. Browser font defaults are respected; controls and headings can wrap. This applies resource advantage through reuse and targeted verification, not an external-agent campaign or a framework migration.

## File and privacy boundaries

[The textarea standard](https://html.spec.whatwg.org/multipage/form-elements.html#the-textarea-element) and [TextDecoder](https://developer.mozilla.org/en-US/docs/Web/API/TextDecoder) inform keeping an untouched decoded source string separate from the DOM value. Nonedited imported document exports preserve supported BOM and newline conventions. Editing deliberately normalizes mixed endings to the displayed convention. Unlike the Windows edition, the website cannot atomically overwrite an arbitrary original file: explicit exports are the safe, portable behavior.

[Clipboard.writeText](https://developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText) has permission/user-activation constraints. Copy is explicit by default and exposes selection/export fallback. [showSaveFilePicker](https://developer.mozilla.org/en-US/docs/Web/API/Window/showSaveFilePicker) is not the cross-browser baseline, so there is no unsupported promise of silent saves to an arbitrary folder. Exports use standard Blob downloads; the browser controls their final destination.

Local history is optional and unencrypted. No-op conversions do not consume records. App history limits never delete old records automatically. CSP blocks app network connections, imported text is not rendered as HTML, and conversion never fetches a URL. Host traffic, agent access when explicitly invoked, browser storage eviction and installed extensions remain outside those guarantees.

## Production handoff

Keep the Windows workflow unchanged; place the browser edition under `web/` in the existing repository. Pin the lockfile and focused CI. Publish the tested static assets to the same private Site registration. Retain source commit, deployment/version IDs, test scope and unresolved browser limitations in a local receipt. No scheduling is needed for this fixed utility.
