# How to use the app

## Paste a batch

Open the app, paste your mixed text into the editor, and choose **Convert links**. Matching `pillows.su/f/…` URLs are replaced in the same locations. The unique converted links are copied automatically unless that option is disabled in Settings.

Open JDownloader, select **LinkGrabber**, press **Ctrl+V**, inspect the results, and start only the downloads you recognize and are authorized to access.

## Work with a text file

Choose **File → Open** or drag a `.txt` file onto the window. The app remembers its encoding, BOM, and line endings. On conversion it first creates a complete History snapshot, asks before changing the file, then performs a verified atomic replacement.

Use **File → Save As** if you want the converted text in a separate file instead.

## Undo a conversion

Choose **Restore before** in the result card. The app restores the original snapshot and writes a separate Restore record to History, so the audit trail remains append-only.

## Understand the counts

- **Occurrences** includes repeated share links because every occurrence changes in the editor.
- **Unique download links** is the deduplicated list copied for JDownloader.
- **Other URLs** are left unchanged and recorded in `Unsupported Links.txt` for that run.
- API links that were already converted are left unchanged.

## Change preferences

Open **Tools → Settings** to choose the History folder, turn automatic clipboard copy on or off, change the warning shown before an opened file is updated, or select **System**, **Light**, or **Dark** appearance. System mode follows the current Windows color preference.

## Keyboard shortcuts

| Action | Shortcut |
|---|---|
| New | Ctrl+N |
| Open | Ctrl+O |
| Save | Ctrl+S |
| Save As | Ctrl+Shift+S |
| Convert | Ctrl+Enter |
| Help | F1 |

## Troubleshooting

- If no links change, check that each share URL ends in exactly 32 hexadecimal characters.
- If clipboard access is temporarily unavailable, choose **Copy links** again.
- If a file is locked by another program, close it there and retry; your editor text remains available.
- Conversion does not solve JDownloader HTTP 429 errors. Pause repeated retries and reduce JDownloader to one simultaneous download/chunk before resuming a partial download.
