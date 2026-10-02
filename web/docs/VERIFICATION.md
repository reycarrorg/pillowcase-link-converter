# Verification record

Date: October 2, 2026. Synthetic inputs only; no real private link list was used or published.

## Passed automated checks

23 Node tests cover existing conversion behavior, in-document Markdown/prose preservation, case/WWW handling, duplicate occurrences versus unique download links, mixed existing API and unsupported URLs, idempotence, query/fragment/path suffix preservation, punctuation boundaries, nested URLs, invalid/lookalike hosts and IDs, no fuzzy/email/FTP conversion, empty input, inert HTML strings, five BOM/encoding round trips, non-BOM UTF-8, malformed encoding rejection, newline conventions, a 10,000-link batch and input limits/filename safety.

Static esbuild bundle completes without external runtime assets. Bundled third-party notices are included. This does not establish that any converted endpoint is currently downloadable.

## Passed browser observations

Codex In-app Browser on the local preview:

- Real WebMCP registration exposes documented conversion/read schemas. Valid conversion changes the visible editor and counts; invalid numeric input is rejected without changing the previous result.
- Actual UI copy displays success; arrow-key tab selection and Ctrl/Cmd+Enter conversion operate.
- A synthetic local `.txt` was selected through the file chooser, displayed with its filename/encoding, and converted successfully in the visible editor.
- Editing after conversion labels output stale and disables export/copy until reconverted.
- Repeat conversion adds no new history record; before restoration restores the editor.
- Enabling device history saves future records. Saved records survive reload while unsaved editor text intentionally does not. Theme choice persists. The tested preferences were returned to the defaults after testing; pre-existing saved records are not deleted.
- At a 320 CSS-pixel viewport, the document width equals the viewport: no page-wide horizontal overflow was observed. This is one breakpoint observation, not a full accessibility audit.
- No application error/warning logs were observed in the checked console sample.

## Limitations / follow-up acceptance

The In-app Browser showed an export-request status without surfacing a completed download event. Actual final save completion and bytes from an exported browser file are **not verified**. Encoding/byte behavior is unit-tested, and local file import was browser-tested. Use the target browser to confirm Export document / Export .txt save as expected. Clipboard-denied behavior exists in code but was not forced in the live browser.

Firefox, Safari, Edge and mobile physical devices have not been individually exercised. No Windows executable/build/runtime check was rerun because Windows source was preserved. No formal screen-reader or WCAG conformance audit was performed. File-host availability and linked-content safety are outside the converter.

## Quick acceptance test

1. Use **Try an example**, then **Convert links**. Expect 2 replacements, 1 unique link and 1 other URL.
2. View **Document**: the heading, notes and both song occurrences remain. **Other URLs** contains the example reference.
3. Copy the download list; it should contain one API URL only.
4. Export the list, and separately export the complete document. Check both files in your browser's download location.
5. Restore the before snapshot, edit a note, reconvert and verify the edited note remains.
6. If desired, enable local history in Settings, make a changed conversion and reload. The saved record should reappear; a unsaved editor draft should not.
