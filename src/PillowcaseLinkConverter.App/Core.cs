using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PillowcaseLinkConverter
{
    public sealed class ConversionResult
    {
        public string OriginalText { get; set; }
        public string ConvertedText { get; set; }
        public List<string> BeforeOccurrences { get; set; }
        public List<string> AfterOccurrences { get; set; }
        public List<string> UniqueConvertedLinks { get; set; }
        public List<string> UnsupportedLinks { get; set; }
        public List<string> AlreadyConvertedLinks { get; set; }
        public bool Changed { get { return !String.Equals(OriginalText, ConvertedText, StringComparison.Ordinal); } }
    }

    public static class ConversionEngine
    {
        private const string LandingPattern = "https?://(?:www\\.)?pillows\\.su/f/(?<id>[0-9a-fA-F]{32})(?=$|[\\s\\]\\)\\}<>\\\"'/?#])";
        private const string ApiPattern = @"^https?://api\.pillows\.su/api/download/[0-9a-fA-F]{32}(?:[/?#].*)?$";
        private const string LandingWholePattern = @"^https?://(?:www\.)?pillows\.su/f/[0-9a-fA-F]{32}(?:[/?#].*)?$";
        private static readonly Regex LandingRegex = new Regex(LandingPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AnyUrlRegex = new Regex("https?://[^\\s\\]\\)\\}<>\\\"']+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ApiRegex = new Regex("https?://api\\.pillows\\.su/api/download/[0-9a-fA-F]{32}(?=$|[\\s\\]\\)\\}<>\\\"'/?#])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ConversionResult Transform(string text)
        {
            text = text ?? String.Empty;
            var before = new List<string>();
            var after = new List<string>();

            string transformed = LandingRegex.Replace(text, delegate(Match match)
            {
                string replacement = "https://api.pillows.su/api/download/" + match.Groups["id"].Value.ToLowerInvariant();
                before.Add(match.Value);
                after.Add(replacement);
                return replacement;
            });

            var allUrls = AnyUrlRegex.Matches(text).Cast<Match>()
                .Select(m => TrimUrlPunctuation(m.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var already = ApiRegex.Matches(text).Cast<Match>()
                .Select(m => TrimUrlPunctuation(m.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unsupported = allUrls
                .Where(url => !Regex.IsMatch(url, LandingWholePattern, RegexOptions.IgnoreCase))
                .Where(url => !Regex.IsMatch(url, ApiPattern, RegexOptions.IgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ConversionResult
            {
                OriginalText = text,
                ConvertedText = transformed,
                BeforeOccurrences = before,
                AfterOccurrences = after,
                UniqueConvertedLinks = after.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                UnsupportedLinks = unsupported,
                AlreadyConvertedLinks = already
            };
        }

        private static string TrimUrlPunctuation(string value)
        {
            return value.TrimEnd('.', ',', ';', ':', '!', '?');
        }
    }

    public sealed class TextDocument
    {
        public string Text { get; set; }
        public Encoding Encoding { get; set; }
        public bool HasBom { get; set; }
        public string Newline { get; set; }
    }

    public static class TextDocumentIO
    {
        public static TextDocument Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Encoding encoding;
            int offset;
            bool hasBom;
            DetectEncoding(bytes, out encoding, out offset, out hasBom);
            string text = encoding.GetString(bytes, offset, bytes.Length - offset);
            return new TextDocument
            {
                Text = text,
                Encoding = encoding,
                HasBom = hasBom,
                Newline = DetectNewline(text)
            };
        }

        public static void AtomicWrite(string path, string text, Encoding encoding, bool emitBom)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A destination path is required.", "path");
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (String.IsNullOrEmpty(directory)) throw new IOException("The destination directory could not be determined.");
            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(directory, ".plc-" + Guid.NewGuid().ToString("N") + ".tmp");
            string rollbackPath = Path.Combine(directory, ".plc-" + Guid.NewGuid().ToString("N") + ".rollback");
            bool targetExisted = File.Exists(fullPath);
            try
            {
                WriteBytesFlushed(temporaryPath, text ?? String.Empty, encoding ?? new UTF8Encoding(false), emitBom);
                string expected = ComputeHash(Encode(text ?? String.Empty, encoding ?? new UTF8Encoding(false), emitBom));
                string actual = ComputeFileHash(temporaryPath);
                if (!String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("The temporary file failed verification.");

                if (targetExisted)
                {
                    File.Replace(temporaryPath, fullPath, rollbackPath, true);
                    if (File.Exists(rollbackPath)) File.Delete(rollbackPath);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            catch
            {
                if (targetExisted && !File.Exists(fullPath) && File.Exists(rollbackPath))
                    File.Move(rollbackPath, fullPath);
                throw;
            }
            finally
            {
                TryDelete(temporaryPath);
                if (File.Exists(fullPath)) TryDelete(rollbackPath);
            }
        }

        public static string ComputeHash(string text, Encoding encoding, bool emitBom)
        {
            return ComputeHash(Encode(text ?? String.Empty, encoding ?? new UTF8Encoding(false), emitBom));
        }

        public static string ComputeFileHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return ToHex(sha.ComputeHash(stream));
        }

        private static void DetectEncoding(byte[] bytes, out Encoding encoding, out int offset, out bool hasBom)
        {
            hasBom = true;
            if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            { encoding = new UTF32Encoding(true, true); offset = 4; return; }
            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            { encoding = new UTF32Encoding(false, true); offset = 4; return; }
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            { encoding = new UTF8Encoding(true); offset = 3; return; }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            { encoding = new UnicodeEncoding(false, true); offset = 2; return; }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            { encoding = new UnicodeEncoding(true, true); offset = 2; return; }

            hasBom = false;
            offset = 0;
            try
            {
                var strictUtf8 = new UTF8Encoding(false, true);
                strictUtf8.GetString(bytes);
                encoding = new UTF8Encoding(false);
            }
            catch (DecoderFallbackException)
            {
                encoding = Encoding.Default;
            }
        }

        private static string DetectNewline(string text)
        {
            int crlf = text.IndexOf("\r\n", StringComparison.Ordinal);
            if (crlf >= 0) return "CRLF";
            if (text.IndexOf('\n') >= 0) return "LF";
            if (text.IndexOf('\r') >= 0) return "CR";
            return "None";
        }

        private static byte[] Encode(string text, Encoding encoding, bool emitBom)
        {
            byte[] body = encoding.GetBytes(text);
            byte[] preamble = emitBom ? encoding.GetPreamble() : new byte[0];
            if (preamble.Length == 0) return body;
            byte[] combined = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, combined, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, combined, preamble.Length, body.Length);
            return combined;
        }

        private static void WriteBytesFlushed(string path, string text, Encoding encoding, bool emitBom)
        {
            byte[] bytes = Encode(text, encoding, emitBom);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static string ComputeHash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return ToHex(sha.ComputeHash(bytes));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public string HistoryDirectory { get; set; }
        [DataMember] public bool CopyAfterConversion { get; set; }
        [DataMember] public bool WarnBeforeUpdatingFile { get; set; }
        [DataMember] public bool WelcomeSeen { get; set; }
        [DataMember] public string Theme { get; set; }

        public static AppSettings Defaults()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new AppSettings
            {
                HistoryDirectory = Path.Combine(local, "Pillowcase Link Converter", "History"),
                CopyAfterConversion = true,
                WarnBeforeUpdatingFile = true,
                WelcomeSeen = false,
                Theme = "System"
            };
        }
    }

    public sealed class SettingsLoadResult
    {
        public AppSettings Settings { get; set; }
        public string Warning { get; set; }
    }

    public static class SettingsService
    {
        public static string DefaultPath
        {
            get
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, "Pillowcase Link Converter", "settings.json");
            }
        }

        public static SettingsLoadResult Load(string path)
        {
            var defaults = AppSettings.Defaults();
            if (!File.Exists(path)) return new SettingsLoadResult { Settings = defaults };
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    var settings = (AppSettings)serializer.ReadObject(stream);
                    if (settings == null || String.IsNullOrWhiteSpace(settings.HistoryDirectory)) throw new SerializationException("Settings were incomplete.");
                    if (String.IsNullOrWhiteSpace(settings.Theme)) settings.Theme = "System";
                    return new SettingsLoadResult { Settings = settings };
                }
            }
            catch (Exception ex)
            {
                string preserved = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                try { File.Copy(path, preserved, false); } catch { preserved = null; }
                return new SettingsLoadResult
                {
                    Settings = defaults,
                    Warning = "Settings could not be read, so defaults were loaded." + (preserved == null ? String.Empty : " The original file was preserved at " + preserved + ".") + "\n\n" + ex.Message
                };
            }
        }

        public static void Save(AppSettings settings, string path)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temp = Path.Combine(directory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    serializer.WriteObject(stream, settings);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, null, true); else File.Move(temp, path);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }
    }

    [DataContract]
    public sealed class HistoryMetadata
    {
        [DataMember] public int SchemaVersion { get; set; }
        [DataMember] public string State { get; set; }
        [DataMember] public string Action { get; set; }
        [DataMember] public string CreatedUtc { get; set; }
        [DataMember] public string DocumentName { get; set; }
        [DataMember] public string SourcePath { get; set; }
        [DataMember] public string Encoding { get; set; }
        [DataMember] public bool HasBom { get; set; }
        [DataMember] public string Newline { get; set; }
        [DataMember] public int OccurrenceCount { get; set; }
        [DataMember] public int UniqueConvertedCount { get; set; }
        [DataMember] public int UnsupportedCount { get; set; }
        [DataMember] public int AlreadyConvertedCount { get; set; }
        [DataMember] public string BeforeSha256 { get; set; }
        [DataMember] public string AfterSha256 { get; set; }
    }

    public sealed class HistoryDraft
    {
        public string PendingDirectory { get; set; }
        public string FinalDirectory { get; set; }
        public HistoryMetadata Metadata { get; set; }
    }

    public static class HistoryManager
    {
        public static HistoryDraft Stage(string historyRoot, string documentName, string sourcePath, TextDocument document, ConversionResult result, string action)
        {
            if (String.IsNullOrWhiteSpace(historyRoot)) throw new ArgumentException("Choose a history folder in Settings.");
            DateTime now = DateTime.Now;
            string day = Path.Combine(historyRoot, now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(day);
            string id = Guid.NewGuid().ToString("N");
            string safeName = MakeSafeName(documentName);
            string baseName = now.ToString("HH-mm-ss-fff") + "_" + safeName + "_" + id.Substring(0, 8);
            string pending = Path.Combine(day, ".pending-" + id);
            string final = Path.Combine(day, baseName);
            Directory.CreateDirectory(pending);

            var metadata = new HistoryMetadata
            {
                SchemaVersion = 1,
                State = "Pending",
                Action = action,
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                DocumentName = documentName,
                SourcePath = sourcePath,
                Encoding = document.Encoding.WebName,
                HasBom = document.HasBom,
                Newline = document.Newline,
                OccurrenceCount = result.BeforeOccurrences.Count,
                UniqueConvertedCount = result.UniqueConvertedLinks.Count,
                UnsupportedCount = result.UnsupportedLinks.Count,
                AlreadyConvertedCount = result.AlreadyConvertedLinks.Count,
                BeforeSha256 = TextDocumentIO.ComputeHash(result.OriginalText, document.Encoding, document.HasBom),
                AfterSha256 = TextDocumentIO.ComputeHash(result.ConvertedText, document.Encoding, document.HasBom)
            };

            try
            {
                WriteHistoryText(Path.Combine(pending, "Original Document.txt"), result.OriginalText);
                WriteHistoryText(Path.Combine(pending, "Converted Document.txt"), result.ConvertedText);
                WriteHistoryText(Path.Combine(pending, "Before Links.txt"), JoinLines(result.BeforeOccurrences));
                WriteHistoryText(Path.Combine(pending, "After Links.txt"), JoinLines(result.AfterOccurrences));
                WriteHistoryText(Path.Combine(pending, "Converted Links.txt"), JoinLines(result.UniqueConvertedLinks));
                WriteHistoryText(Path.Combine(pending, "Unsupported Links.txt"), JoinLines(result.UnsupportedLinks));
                WriteMetadata(Path.Combine(pending, "metadata.json"), metadata);
                VerifyHistoryFile(Path.Combine(pending, "Original Document.txt"));
                VerifyHistoryFile(Path.Combine(pending, "Converted Document.txt"));
                return new HistoryDraft { PendingDirectory = pending, FinalDirectory = final, Metadata = metadata };
            }
            catch
            {
                TryDeleteDirectory(pending);
                throw;
            }
        }

        public static string Complete(HistoryDraft draft)
        {
            draft.Metadata.State = "Completed";
            WriteMetadata(Path.Combine(draft.PendingDirectory, "metadata.json"), draft.Metadata);
            Directory.Move(draft.PendingDirectory, draft.FinalDirectory);
            return draft.FinalDirectory;
        }

        public static void Abandon(HistoryDraft draft)
        {
            if (draft != null) TryDeleteDirectory(draft.PendingDirectory);
        }

        private static void WriteHistoryText(string path, string text)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text ?? String.Empty);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static void WriteMetadata(string path, HistoryMetadata metadata)
        {
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var serializer = new DataContractJsonSerializer(typeof(HistoryMetadata));
                serializer.WriteObject(stream, metadata);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, null, true); else File.Move(temp, path);
        }

        private static void VerifyHistoryFile(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { if (stream.Length < 0) throw new IOException("History verification failed."); }
        }

        private static string JoinLines(IEnumerable<string> values)
        {
            return String.Join(Environment.NewLine, values.ToArray());
        }

        private static string MakeSafeName(string value)
        {
            string name = String.IsNullOrWhiteSpace(value) ? "Untitled" : Path.GetFileNameWithoutExtension(value);
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            name = Regex.Replace(name, @"\s+", " ").Trim();
            if (name.Length > 40) name = name.Substring(0, 40).Trim();
            return String.IsNullOrEmpty(name) ? "Untitled" : name;
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }
    }
}
