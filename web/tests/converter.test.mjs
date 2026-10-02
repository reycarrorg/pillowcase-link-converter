import test from "node:test";
import assert from "node:assert/strict";
import { transform, decodeDocument, encodeDocument, applyEditorNewlines, documentFilename, MAX_BYTES } from "../src/converter.mjs";

const id = "0123456789abcdef0123456789abcdef";
const share = `https://pillows.su/f/${id}`;
const api = `https://api.pillows.su/api/download/${id}`;

test("plain transformation retains the current repository's contract", () => {
  const r = transform(share);
  assert.equal(r.convertedText, api); assert.equal(r.changed, true);
  assert.deepEqual(r.uniqueConvertedLinks, [api]);
});
test("HTTP, www, uppercase scheme/host/ID, Markdown, CRLF and prose", () => {
  const original = `Note [song](HTTP://WWW.PILLOWS.SU/F/${id.toUpperCase()}).\r\nCafé 🎵\r\nKeep this.`;
  assert.equal(transform(original).convertedText, `Note [song](${api}).\r\nCafé 🎵\r\nKeep this.`);
});
test("duplicate occurrences remain; newly converted copy list deduplicates", () => {
  const r = transform(`${share}\n${share}`);
  assert.equal(r.beforeOccurrences.length, 2); assert.equal(r.uniqueConvertedLinks.length, 1);
  assert.equal(r.duplicateCount, 1); assert.equal(r.convertedText, `${api}\n${api}`);
});
test("existing API and other URLs are classified but not changed", () => {
  const r = transform(`${share}\n${api}\nhttps://example.com/file`);
  assert.equal(r.alreadyConvertedLinks.length, 1);
  assert.deepEqual(r.unsupportedLinks, ["https://example.com/file"]);
  assert.deepEqual(r.uniqueConvertedLinks, [api]);
});
test("repeat transformation is idempotent; API-only input creates no change", () => {
  const r = transform(transform(share).convertedText);
  assert.equal(r.changed, false); assert.equal(r.beforeOccurrences.length, 0);
  assert.equal(r.uniqueConvertedLinks.length, 0);
});
test("queries, fragments and trailing paths are preserved in document, not copy list", () => {
  for (const suffix of ["?download=1", "#note", "/extra?x=2#note"]) {
    const r = transform(share + suffix);
    assert.equal(r.convertedText, api + suffix); assert.deepEqual(r.uniqueConvertedLinks, [api]);
  }
});
test("sentence punctuation is not silently skipped", () => {
  for (const punctuation of [".", ",", ";", "!", "?"]) {
    const r = transform(`Song: ${share}${punctuation}`);
    assert.equal(r.convertedText, `Song: ${api}${punctuation}`); assert.equal(r.beforeOccurrences.length, 1);
  }
});
test("a supported URL inside another URL stays inside that unrelated URL unchanged", () => {
  const outer = `https://example.com/redirect?url=${share}`;
  const r = transform(outer); assert.equal(r.convertedText, outer);
  assert.equal(r.beforeOccurrences.length, 0); assert.deepEqual(r.unsupportedLinks, [outer]);
});
test("narrow allowlist rejects lookalikes, credentials, explicit ports and wrong IDs", () => {
  for (const url of [
    `https://pillows.su.evil.example/f/${id}`, `https://evil.example/pillows.su/f/${id}`,
    `https://name@pillows.su/f/${id}`, `https://pillows.su:443/f/${id}`,
    `https://pillows.su/f/${id.slice(1)}`, `https://pillows.su/f/${id}a`,
    `https://pillows.su/f/${id.slice(1)}z`,
  ]) assert.equal(transform(url).convertedText, url);
});
test("no fuzzy domains or emails are recognized as HTTP URLs", () => {
  const r = transform(`pillows.su/f/${id} person@example.com ftp://example.com/file`);
  assert.equal(r.urlCount, 0); assert.equal(r.changed, false);
});
test("other URL list deduplicates case-insensitively", () => {
  const r = transform("https://example.com/file\nHTTPS://EXAMPLE.COM/FILE");
  assert.deepEqual(r.unsupportedLinks, ["https://example.com/file"]);
});
test("empty and whitespace input is an unchanged document", () => {
  for (const text of ["", "  \r\n"]) {
    const r = transform(text); assert.equal(r.convertedText, text); assert.equal(r.changed, false);
  }
});
test("HTML-like document text remains literal content", () => {
  const original = `<script>alert(1)</script>\n<img src=x onerror=alert(1)>\n${share}`;
  assert.equal(transform(original).convertedText, original.replace(share, api));
});
for (const encoding of ["utf-8", "utf-16le", "utf-16be", "utf-32le", "utf-32be"]) {
  test(`${encoding} BOM and CRLF round trip preserves bytes apart from converted URL`, () => {
    const text = `Café 🎵\r\n${share}\r\n`;
    const bytes = encodeDocument(text, encoding, true), read = decodeDocument(bytes);
    assert.equal(read.text, text); assert.equal(read.encoding, encoding); assert.equal(read.bom, true); assert.equal(read.newline, "CRLF");
    assert.deepEqual(encodeDocument(read.text, read.encoding, read.bom), bytes);
    assert.equal(decodeDocument(encodeDocument(transform(read.text).convertedText, encoding, true)).text, `Café 🎵\r\n${api}\r\n`);
  });
}
test("UTF-8 without BOM stays without BOM", () => {
  const text = "Notes\n" + share, bytes = encodeDocument(text);
  const read = decodeDocument(bytes); assert.equal(read.bom, false); assert.equal(read.text, text);
});
test("malformed Unicode is rejected, not silently replaced", () => {
  for (const bytes of [new Uint8Array([0xff]), new Uint8Array([0xff, 0xfe, 0x01]), new Uint8Array([0xff, 0xfe, 0, 0, 0x00, 0xd8, 0, 0])]) assert.throws(() => decodeDocument(bytes));
});
test("editor normalization respects the imported convention", () => {
  assert.equal(applyEditorNewlines("One\nTwo\n", "CRLF"), "One\r\nTwo\r\n");
  assert.equal(applyEditorNewlines("One\rTwo", "LF"), "One\nTwo");
});
test("large 10,000-link batch preserves occurrences and creates one copy link", () => {
  const started = performance.now(), r = transform(Array(10000).fill(share).join("\n"));
  assert.equal(r.beforeOccurrences.length, 10000); assert.equal(r.uniqueConvertedLinks.length, 1);
  assert.ok(performance.now() - started < 5000, "conversion should not take five seconds for this fixture");
});
test("size limits fail explicitly; export names cannot create path traversal", () => {
  assert.throws(() => transform("a".repeat(MAX_BYTES + 1)), RangeError);
  assert.equal(documentFilename("a/b.txt"), "a_b-converted.txt");
});
