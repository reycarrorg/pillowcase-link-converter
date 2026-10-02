import { openDB } from "idb";
import { transform, decodeDocument, encodeDocument, detectNewline, applyEditorNewlines, documentFilename, MAX_BYTES } from "./converter.mjs";

const byId = id => document.getElementById(id);
const editor = byId("document"), output = byId("output");
const tabs = [...document.querySelectorAll("[role=tab]")];
const SETTINGS_KEY = "pillowcase.preferences.v1";
const HISTORY_BUDGET = 50 * 1024 * 1024;
const initialPreferences = { theme: "system", autoCopy: false, persistentHistory: false };
let preferences = { ...initialPreferences };
try {
  const stored = JSON.parse(localStorage.getItem(SETTINGS_KEY) || "null");
  if (stored && typeof stored === "object") {
    if (["system", "light", "dark"].includes(stored.theme)) preferences.theme = stored.theme;
    preferences.autoCopy = stored.autoCopy === true;
    preferences.persistentHistory = stored.persistentHistory === true;
  }
} catch { /* Storage is optional; the converter remains usable. */ }

const state = {
  document: { text: "", encoding: "utf-8", bom: false, newline: "LF", name: "Untitled document" },
  result: null, view: "links", stale: false, lastChange: null, history: [],
  database: null, operation: 0, loading: false,
};
const systemAppearance = matchMedia("(prefers-color-scheme: dark)");
function applyTheme() {
  document.documentElement.dataset.theme = preferences.theme === "system"
    ? systemAppearance.matches ? "dark" : "light" : preferences.theme;
}
systemAppearance.addEventListener("change", applyTheme);
applyTheme();
byId("theme").value = preferences.theme;
byId("auto-copy").checked = preferences.autoCopy;
byId("persistent-history").checked = preferences.persistentHistory;

function announce(message, warning = false) {
  const feedback = byId("feedback");
  feedback.hidden = false;
  feedback.dataset.kind = warning ? "warning" : "success";
  feedback.textContent = message;
}

function savePreferences() {
  try { localStorage.setItem(SETTINGS_KEY, JSON.stringify(preferences)); }
  catch { announce("Preferences could not be saved in this browser. Conversion still works.", true); }
}

function renderDocument() {
  editor.value = state.document.text;
  byId("file-name").textContent = state.document.name;
  byId("file-detail").textContent = `${state.document.encoding.toUpperCase()}${state.document.bom ? " BOM" : ""} · ${state.document.newline} · local only`;
  byId("restore").disabled = !state.lastChange;
}

function viewText() {
  if (!state.result) return "";
  if (state.view === "document") return state.result.convertedText;
  if (state.view === "unsupported") return state.result.unsupportedLinks.join("\n");
  return state.result.uniqueConvertedLinks.join("\n");
}

function renderResult() {
  const r = state.result, text = viewText();
  byId("receipt").hidden = !r;
  if (r) {
    byId("changed-count").textContent = r.beforeOccurrences.length.toLocaleString();
    byId("unique-count").textContent = r.uniqueConvertedLinks.length.toLocaleString();
    byId("review-count").textContent = r.unsupportedLinks.length.toLocaleString();
  }
  byId("result-state").textContent = state.stale ? "Edited · convert again" : r ? r.changed ? "Converted" : "No changes needed" : "Waiting for input";
  byId("result-state").classList.toggle("stale", state.stale);
  output.value = text;
  output.hidden = !r || !text;
  byId("empty-state").hidden = !!r && !!text;
  if (r && !text) {
    byId("empty-state").querySelector("h3").textContent = state.view === "unsupported" ? "Nothing to review." : "No new download links.";
    byId("empty-state").querySelector("p").textContent = state.view === "unsupported" ? "All recognized URLs use a supported Pillowcase format." : "Already-converted links stay in your document. Only newly converted links enter this list.";
    byId("empty-state").querySelector("span:last-child").textContent = "Your document is preserved.";
  }
  const labels = { links: "Copy links", document: "Copy document", unsupported: "Copy other URLs" };
  const hints = {
    links: r ? `${r.duplicateCount.toLocaleString()} repeated ${r.duplicateCount === 1 ? "link" : "links"} omitted from this list only. ${r.alreadyConvertedLinks.length.toLocaleString()} already-converted URLs left in the document.` : "Only links converted in this run appear in the download list.",
    document: "Complete converted text, with your notes, Markdown, and repeated links intact.",
    unsupported: "These URLs were not converted. They remain in the document; no link availability was checked.",
  };
  byId("copy").textContent = labels[state.view];
  byId("export").textContent = state.view === "document" ? "Export document" : "Export .txt";
  byId("output-label").textContent = labels[state.view].replace("Copy", "Read-only");
  byId("output-hint").textContent = state.stale ? "Results are from the previous conversion. Convert the edited document before copying or exporting." : hints[state.view];
  for (const id of ["copy", "export", "select-output"]) byId(id).disabled = !text || state.stale;
  for (const tab of tabs) {
    const active = tab.dataset.view === state.view;
    tab.setAttribute("aria-selected", String(active));
    tab.tabIndex = active ? 0 : -1;
    if (active) byId("output-panel").setAttribute("aria-labelledby", tab.id);
  }
}

function setView(view, focus = false) {
  state.view = view;
  renderResult();
  if (focus) tabs.find(tab => tab.dataset.view === view).focus();
}
tabs.forEach((tab, index) => {
  tab.addEventListener("click", () => setView(tab.dataset.view));
  tab.addEventListener("keydown", event => {
    let next;
    if (event.key === "ArrowRight") next = (index + 1) % tabs.length;
    if (event.key === "ArrowLeft") next = (index + tabs.length - 1) % tabs.length;
    if (event.key === "Home") next = 0;
    if (event.key === "End") next = tabs.length - 1;
    if (next !== undefined) { event.preventDefault(); setView(tabs[next].dataset.view, true); }
  });
});

function startDocument(doc, name = "Untitled document") {
  state.operation++;
  state.document = { ...doc, name };
  if (state.document.newline === "None") state.document.newline = "LF";
  state.result = null; state.stale = false; state.lastChange = null;
  byId("feedback").hidden = true;
  state.view = "links";
  renderDocument(); renderResult();
  // Restore the initial empty-state copy after another document's empty result.
  byId("empty-state").querySelector("h3").textContent = "A cleaner handoff.";
  byId("empty-state").querySelector("p").textContent = "Your converted links will appear here, ready to copy into JDownloader.";
  byId("empty-state").querySelector("span:last-child").textContent = "The document keeps repeats. The copy list doesn’t.";
}

editor.addEventListener("input", () => {
  state.operation++;
  state.document.text = applyEditorNewlines(editor.value, state.document.newline);
  state.stale = !!state.result;
  renderResult();
});

function downloadBytes(bytes, name, type = "text/plain") {
  const url = URL.createObjectURL(new Blob([bytes], { type }));
  const link = document.createElement("a");
  link.href = url; link.download = name;
  document.body.append(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 15000);
}

function copyText(text, label) {
  if (!navigator.clipboard?.writeText) {
    announce("Clipboard access is unavailable. Use Select text and your keyboard copy command, or export a file.", true);
    return Promise.resolve(false);
  }
  // Call before awaiting history or hashing so browser user activation is retained.
  return navigator.clipboard.writeText(text).then(() => {
    announce(`${label} copied.`);
    return true;
  }, () => {
    announce("The browser denied clipboard access. Use Select text, then ⌘C / Ctrl+C, or export a file.", true);
    return false;
  });
}

async function getDatabase() {
  if (state.database) return state.database;
  state.database = openDB("pillowcase.history.v1", 1, {
    upgrade(db) { db.createObjectStore("runs", { keyPath: "id" }); },
    blocked() { announce("Local history is waiting for another tab to close. Conversion still works.", true); },
    blocking() { state.database?.then(db => db.close()); state.database = null; },
    terminated() { state.database = null; },
  });
  try { return await state.database; }
  catch (error) { state.database = null; throw error; }
}

async function loadSavedHistory() {
  try {
    const db = await getDatabase();
    const saved = await db.getAll("runs");
    const combined = new Map(state.history.map(run => [run.id, run]));
    for (const run of saved) {
      if (run?.schemaVersion === 1 && typeof run.originalText === "string" && typeof run.convertedText === "string" && typeof run.createdAt === "string" && typeof run.filename === "string" && ["utf-8", "utf-16le", "utf-16be", "utf-32le", "utf-32be"].includes(run.encoding)) combined.set(run.id, { ...run, saved: true });
    }
    state.history = [...combined.values()].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    renderHistory();
  } catch { announce("This browser could not load saved history. Session history and conversion remain available.", true); }
}

async function sha256(bytes) {
  if (!crypto.subtle) return null;
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return [...new Uint8Array(digest)].map(byte => byte.toString(16).padStart(2, "0")).join("");
}

async function recordChange(result, source, action) {
  const originalBytes = encodeDocument(result.originalText, source.encoding, source.bom);
  const convertedBytes = encodeDocument(result.convertedText, source.encoding, source.bom);
  const record = {
    schemaVersion: 1, id: crypto.randomUUID(), createdAt: new Date().toISOString(),
    action, filename: source.name, encoding: source.encoding, bom: source.bom,
    newline: source.newline, originalText: result.originalText, convertedText: result.convertedText,
    convertedOccurrences: result.beforeOccurrences.length, uniqueConvertedLinks: result.uniqueConvertedLinks,
    unsupportedLinks: result.unsupportedLinks, alreadyConvertedLinks: result.alreadyConvertedLinks,
    beforeOccurrences: result.beforeOccurrences, afterOccurrences: result.afterOccurrences,
    hashByteFormat: "Document encoded with the recorded encoding and BOM; not JSON export bytes",
    originalSha256: await sha256(originalBytes), convertedSha256: await sha256(convertedBytes), saved: false,
  };
  const estimatedBytes = new TextEncoder().encode(JSON.stringify(record)).length;
  const used = state.history.reduce((total, run) => total + (run.recordBytes || 0), 0);
  if (used + estimatedBytes > HISTORY_BUDGET) {
    announce("Conversion is complete, but the 50 MB history budget is full. Export available history; older records were not deleted.", true);
    return;
  }
  record.recordBytes = estimatedBytes;
  state.history.unshift(record);
  if (preferences.persistentHistory) {
    try {
      const db = await getDatabase();
      const tx = db.transaction("runs", "readwrite");
      await tx.store.add({ ...record, saved: true });
      await tx.done;
      record.saved = true;
    } catch {
      announce("Converted successfully, but this run’s history could not be saved to the device. It remains in this session; export it before closing.", true);
    }
  }
  renderHistory();
}

function renderHistory() {
  byId("history-count").textContent = state.history.length;
  byId("history-location").textContent = preferences.persistentHistory ? "This browser" : "This session";
  byId("download-history").disabled = !state.history.length;
  const list = byId("history-list");
  list.replaceChildren();
  if (!state.history.length) {
    const empty = document.createElement("p"); empty.className = "muted"; empty.textContent = "No conversions yet."; list.append(empty); return;
  }
  for (const run of state.history) {
    const row = document.createElement("div"); row.className = "history-row";
    const description = document.createElement("div"), name = document.createElement("strong"), details = document.createElement("span");
    name.textContent = run.filename;
    details.textContent = `${new Date(run.createdAt).toLocaleString()} · ${run.convertedOccurrences} replacements · ${run.saved ? "saved on device" : "session only"}`;
    description.append(name, details);
    const actions = document.createElement("div"); actions.className = "row-actions";
    for (const [label, handler] of [
      ["Restore before", () => restoreHistory(run)],
      ["Export record", () => downloadBytes(new TextEncoder().encode(JSON.stringify(run, null, 2)), `Pillowcase-run-${run.createdAt.replace(/[:.]/g, "-")}.json`, "application/json")],
    ]) {
      const button = document.createElement("button"); button.className = "button subtle"; button.textContent = label; button.addEventListener("click", handler); actions.append(button);
    }
    row.append(description, actions); list.append(row);
  }
}

function mayReplaceDocument() {
  return !state.document.text.trim() || confirm("Replace the current document? Export any unsaved text first. History records will remain intact.");
}

function restoreHistory(run) {
  if (!mayReplaceDocument()) return;
  startDocument({ text: run.originalText, encoding: run.encoding, bom: run.bom, newline: run.newline }, run.filename);
  announce("The before snapshot is restored in the editor. No original file was changed.");
  editor.focus();
}

async function convert({ fromAgent = false } = {}) {
  if (state.loading) throw new Error("Wait for file import to finish.");
  const source = { ...state.document };
  let result;
  try { result = transform(source.text); }
  catch (error) { announce(error.message, true); return { error: error.message }; }
  state.result = result; state.stale = false;
  if (result.changed) {
    state.lastChange = source;
    state.document.text = result.convertedText;
  }
  renderDocument(); renderResult();
  let message;
  if (result.changed) message = `${result.beforeOccurrences.length.toLocaleString()} ${result.beforeOccurrences.length === 1 ? "link" : "links"} replaced. ${result.uniqueConvertedLinks.length.toLocaleString()} unique download ${result.uniqueConvertedLinks.length === 1 ? "link" : "links"} ready. Notes and repeats are preserved.`;
  else if (result.alreadyConvertedLinks.length) message = "These Pillowcase links are already in download format. No changes or extra history were needed.";
  else message = source.text.trim() ? "No supported Pillowcase links found. Your document is unchanged. Check the supported format or review Other URLs." : "Paste a document or choose a text file to begin.";
  if (result.unsupportedLinks.length) message += ` ${result.unsupportedLinks.length} other ${result.unsupportedLinks.length === 1 ? "URL was" : "URLs were"} left unchanged.`;
  announce(message, !result.changed && !!source.text.trim() && !result.alreadyConvertedLinks.length);
  const copyPromise = !fromAgent && preferences.autoCopy && result.uniqueConvertedLinks.length
    ? copyText(result.uniqueConvertedLinks.join("\n"), "Download links") : Promise.resolve();
  if (result.changed) {
    try { await recordChange(result, source, "Convert"); }
    catch { announce("Converted successfully, but the history record could not be completed. Export the document before closing.", true); }
  }
  await copyPromise;
  return resultSummary();
}

function resultSummary() {
  const r = state.result;
  return r ? {
    stale: state.stale, changed: r.changed, convertedOccurrences: r.beforeOccurrences.length,
    uniqueLinkCount: r.uniqueConvertedLinks.length, otherUrlCount: r.unsupportedLinks.length,
    alreadyConvertedCount: r.alreadyConvertedLinks.length,
    downloadLinksPreview: r.uniqueConvertedLinks.slice(0, 25),
    previewTruncated: r.uniqueConvertedLinks.length > 25,
    documentPreview: r.convertedText.slice(0, 2000),
  } : { ready: false };
}

byId("convert").addEventListener("click", () => void convert());
editor.addEventListener("keydown", event => { if ((event.metaKey || event.ctrlKey) && event.key === "Enter") { event.preventDefault(); void convert(); } });
byId("copy").addEventListener("click", () => { if (!state.stale) void copyText(viewText(), state.view === "links" ? "Download links" : state.view === "document" ? "Document" : "Other URLs"); });
byId("select-output").addEventListener("click", () => { output.focus(); output.select(); announce("Text selected. Press ⌘C / Ctrl+C to copy."); });
byId("export").addEventListener("click", () => {
  if (state.stale || !state.result) return;
  if (state.view === "document") downloadBytes(encodeDocument(state.result.convertedText, state.document.encoding, state.document.bom), documentFilename(state.document.name));
  else downloadBytes(new TextEncoder().encode(viewText()), state.view === "links" ? "Pillowcase-Converted-Links.txt" : "Pillowcase-Other-URLs.txt");
  announce("Export requested. Check your browser’s download or save prompt.");
});

byId("choose-file").addEventListener("click", () => byId("file-picker").click());
byId("file-picker").addEventListener("change", async () => {
  const picker = byId("file-picker"), file = picker.files[0];
  picker.value = "";
  if (!file || !mayReplaceDocument()) return;
  if (file.size > MAX_BYTES) { announce("Choose a text file smaller than 10 MB. The current document was not changed.", true); return; }
  const operation = ++state.operation;
  state.loading = true; byId("convert").disabled = true;
  try {
    const doc = decodeDocument(await file.arrayBuffer());
    if (operation !== state.operation) { announce("Import cancelled because the document changed while the file was loading.", true); return; }
    startDocument(doc, file.name);
    announce(`${file.name} opened locally. Its original file will not be overwritten.`);
  } catch (error) { announce(error.message || "This file could not be opened. The current document was not changed.", true); }
  finally { state.loading = false; byId("convert").disabled = false; }
});

byId("load-example").addEventListener("click", () => {
  if (!mayReplaceDocument()) return;
  startDocument({
    text: "# My link notes\n\n[Song](https://pillows.su/f/0123456789abcdef0123456789abcdef)\nSame song: https://pillows.su/f/0123456789abcdef0123456789abcdef\n\nKeep this note.\nReference: https://example.com/guide",
    encoding: "utf-8", bom: false, newline: "LF",
  }, "Example document");
  editor.focus();
});
byId("new-document").addEventListener("click", () => {
  if (!mayReplaceDocument()) return;
  startDocument({ text: "", encoding: "utf-8", bom: false, newline: "LF" }); editor.focus();
});
byId("restore").addEventListener("click", () => {
  if (!state.lastChange || !mayReplaceDocument()) return;
  const snapshot = { ...state.lastChange }, previous = { ...state.document };
  startDocument(snapshot, snapshot.name);
  announce("The last before snapshot is restored. No original file was changed.");
  if (previous.text !== snapshot.text) {
    void recordChange({ originalText: previous.text, convertedText: snapshot.text, beforeOccurrences: [], afterOccurrences: [], uniqueConvertedLinks: [], unsupportedLinks: [], alreadyConvertedLinks: [] }, previous, "Restore").catch(() => announce("Snapshot restored, but its history record could not be completed.", true));
  }
  editor.focus();
});

byId("settings-open").addEventListener("click", () => byId("settings-dialog").showModal());
byId("theme").addEventListener("change", event => { preferences.theme = event.target.value; applyTheme(); savePreferences(); });
byId("auto-copy").addEventListener("change", event => { preferences.autoCopy = event.target.checked; savePreferences(); });
byId("persistent-history").addEventListener("change", async event => {
  preferences.persistentHistory = event.target.checked; savePreferences(); renderHistory();
  if (preferences.persistentHistory) await loadSavedHistory();
});
byId("download-history").addEventListener("click", () => downloadBytes(new TextEncoder().encode(JSON.stringify({ schemaVersion: 1, exportedAt: new Date().toISOString(), runs: state.history }, null, 2)), "Pillowcase-History.json", "application/json"));

renderDocument(); renderResult(); renderHistory();
if (preferences.persistentHistory) void loadSavedHistory();

// Optional page-scoped agent tools. No inference, backend, or paid API is used.
// Untrusted documents remain strings; tool invocation cannot execute their text.
const modelContext = document.modelContext;
if (modelContext?.registerTool) {
  const lifecycle = new AbortController();
  const tools = [
    {
      name: "convert_pillowcase_document", title: "Convert Pillowcase document",
      description: "Convert a text document in the visible editor without visiting links, downloading files, or copying to the clipboard. Returns counts and bounded previews.",
      inputSchema: { type: "object", properties: { text: { type: "string", maxLength: MAX_BYTES } }, required: ["text"], additionalProperties: false },
      annotations: { readOnlyHint: false, untrustedContentHint: true },
      async execute(input) {
        if (!input || typeof input.text !== "string" || Object.keys(input).some(key => key !== "text") || input.text.length > MAX_BYTES) throw new Error("Provide only a text string within the document limit.");
        startDocument({ text: input.text, encoding: "utf-8", bom: false, newline: detectNewline(input.text) });
        return await convert({ fromAgent: true });
      },
    },
    {
      name: "read_pillowcase_conversion_result", title: "Read conversion result",
      description: "Read counts, stale state, and bounded text previews for the current visible conversion. Does not modify the editor or access files.",
      inputSchema: { type: "object", properties: {}, additionalProperties: false },
      annotations: { readOnlyHint: true, untrustedContentHint: true },
      execute(input) { if (input && Object.keys(input).length) throw new Error("No input fields are accepted."); return resultSummary(); },
    },
  ];
  for (const tool of tools) {
    try { Promise.resolve(modelContext.registerTool(tool, { signal: lifecycle.signal })).catch(() => {}); }
    catch { /* Unsupported/denied registration must not affect the app. */ }
  }
  addEventListener("pagehide", () => lifecycle.abort(), { once: true });
}
