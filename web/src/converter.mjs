// Browser port of REYCARRORG's MIT-licensed PillowcaseLinkConverter Core.cs.
// Conversion is a text operation: no URL here is fetched or made clickable.
import LinkifyIt from "linkify-it";
export const MAX_BYTES = 10 * 1024 * 1024;
const linkifier = new LinkifyIt({}, { fuzzyLink: false, fuzzyEmail: false, fuzzyIP: false })
  .add("ftp:", null).add("mailto:", null).add("//", null);
const SHARE_BASE = /^https?:\/\/(?:www\.)?pillows\.su\/f\/([0-9a-f]{32})(?=$|[/?#])/i;
const API_BASE = /^https?:\/\/api\.pillows\.su\/api\/download\/([0-9a-f]{32})(?=$|[/?#])/i;
const API_ORIGIN = "https://api.pillows.su/api/download/";

export function transform(text) {
  if (typeof text !== "string") throw new TypeError("A text document is required.");
  if (text.length > MAX_BYTES) throw new RangeError("Use a document smaller than 10 MB.");
  const before = [], after = [], unsupported = [], already = [];
  const unique = new Map(), unsupportedSeen = new Set(), alreadySeen = new Set();
  let urlCount = 0;
  const pieces = [];
  let cursor = 0;
  for (const match of linkifier.match(text) || []) {
    if (!/^https?:$/i.test(match.schema)) continue;
    pieces.push(text.slice(cursor, match.index));
    cursor = match.lastIndex;
    const token = match.raw;
    urlCount++;
    const clean = token.replace(/[.,;:!?]+$/, "");
    const punctuation = token.slice(clean.length);
    const share = clean.match(SHARE_BASE);
    if (share) {
      const converted = API_ORIGIN + share[1].toLowerCase();
      before.push(share[0]);
      after.push(converted);
      if (!unique.has(converted)) unique.set(converted, converted);
      // Preserve an existing query, fragment, trailing path, and punctuation.
      pieces.push(converted + clean.slice(share[0].length) + punctuation);
    } else {
      const api = clean.match(API_BASE);
      if (api) {
        const key = clean.toLowerCase();
        if (!alreadySeen.has(key)) { alreadySeen.add(key); already.push(clean); }
      } else {
        const key = clean.toLowerCase();
        if (!unsupportedSeen.has(key)) { unsupportedSeen.add(key); unsupported.push(clean); }
      }
      pieces.push(token);
    }
  }
  pieces.push(text.slice(cursor));
  const convertedText = pieces.join("");
  return {
    originalText: text, convertedText, beforeOccurrences: before,
    afterOccurrences: after, uniqueConvertedLinks: [...unique.values()],
    unsupportedLinks: unsupported, alreadyConvertedLinks: already,
    changed: convertedText !== text, urlCount,
    duplicateCount: after.length - unique.size,
  };
}

export function detectNewline(text) {
  if (text.includes("\r\n")) return "CRLF";
  if (text.includes("\n")) return "LF";
  if (text.includes("\r")) return "CR";
  return "None";
}

export function applyEditorNewlines(text, newline) {
  const normalized = text.replace(/\r\n|\r/g, "\n");
  return newline === "CRLF" ? normalized.replace(/\n/g, "\r\n")
    : newline === "CR" ? normalized.replace(/\n/g, "\r") : normalized;
}

export function decodeDocument(input) {
  const bytes = input instanceof Uint8Array ? input : new Uint8Array(input);
  if (bytes.length > MAX_BYTES) throw new RangeError("Choose a text file smaller than 10 MB.");
  let encoding = "utf-8", offset = 0, bom = false;
  if (bytes.length >= 4 && bytes[0] === 0 && bytes[1] === 0 && bytes[2] === 254 && bytes[3] === 255) {
    encoding = "utf-32be"; offset = 4;
  } else if (bytes.length >= 4 && bytes[0] === 255 && bytes[1] === 254 && bytes[2] === 0 && bytes[3] === 0) {
    encoding = "utf-32le"; offset = 4;
  } else if (bytes.length >= 3 && bytes[0] === 239 && bytes[1] === 187 && bytes[2] === 191) {
    offset = 3;
  } else if (bytes.length >= 2 && bytes[0] === 255 && bytes[1] === 254) {
    encoding = "utf-16le"; offset = 2;
  } else if (bytes.length >= 2 && bytes[0] === 254 && bytes[1] === 255) {
    encoding = "utf-16be"; offset = 2;
  }
  bom = offset !== 0;
  const body = bytes.subarray(offset);
  let text;
  try {
    if (encoding.startsWith("utf-32")) {
      if (body.length % 4) throw new Error("Incomplete UTF-32 sequence.");
      const view = new DataView(body.buffer, body.byteOffset, body.byteLength);
      const parts = [];
      for (let i = 0; i < body.length; i += 4) {
        const point = view.getUint32(i, encoding === "utf-32le");
        if (point > 0x10ffff || point >= 0xd800 && point <= 0xdfff) throw new Error("Invalid Unicode code point.");
        parts.push(String.fromCodePoint(point));
      }
      text = parts.join("");
    } else {
      text = new TextDecoder(encoding, { fatal: true, ignoreBOM: true }).decode(body);
    }
  } catch {
    throw new Error("This file is not valid UTF-8 or BOM-marked UTF-16/UTF-32 text. Save a UTF-8 copy and try again.");
  }
  return { text, encoding, bom, newline: detectNewline(text) };
}

export function encodeDocument(text, encoding = "utf-8", bom = false) {
  let body, prefix;
  if (encoding === "utf-8") {
    body = new TextEncoder().encode(text);
    prefix = new Uint8Array([239, 187, 191]);
  } else if (encoding === "utf-16le" || encoding === "utf-16be") {
    body = new Uint8Array(text.length * 2);
    const view = new DataView(body.buffer);
    for (let i = 0; i < text.length; i++) view.setUint16(i * 2, text.charCodeAt(i), encoding === "utf-16le");
    prefix = new Uint8Array(encoding === "utf-16le" ? [255, 254] : [254, 255]);
  } else if (encoding === "utf-32le" || encoding === "utf-32be") {
    const points = Array.from(text, character => character.codePointAt(0));
    body = new Uint8Array(points.length * 4);
    const view = new DataView(body.buffer);
    for (let i = 0; i < points.length; i++) view.setUint32(i * 4, points[i], encoding === "utf-32le");
    prefix = new Uint8Array(encoding === "utf-32le" ? [255, 254, 0, 0] : [0, 0, 254, 255]);
  } else throw new Error("Unsupported export encoding.");
  if (!bom) return body;
  const bytes = new Uint8Array(prefix.length + body.length);
  bytes.set(prefix); bytes.set(body, prefix.length);
  return bytes;
}

export function documentFilename(name) {
  const base = String(name || "Pillowcase Links.txt").replace(/[\\/:*?"<>|\u0000-\u001f]/g, "_").replace(/\.txt$/i, "");
  return `${base}-converted.txt`;
}
