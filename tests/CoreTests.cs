using System;
using System.IO;
using System.Linq;
using System.Text;
using PillowcaseLinkConverter;

public static class CoreTests
{
    static int failures;
    static void Check(bool value, string name) { if (value) Console.WriteLine("PASS " + name); else { Console.WriteLine("FAIL " + name); failures++; } }
    public static int Main()
    {
        string id = "0123456789abcdef0123456789abcdef";
        string landing = "https://pillows.su/f/" + id;
        string api = "https://api.pillows.su/api/download/" + id;
        var plain = ConversionEngine.Transform(landing); Check(plain.ConvertedText == api, "plain conversion");
        var contextual = ConversionEngine.Transform("Note [song](http://www.pillows.su/f/" + id.ToUpperInvariant() + ").\r\nKeep this.");
        Check(contextual.ConvertedText == "Note [song](" + api + ").\r\nKeep this.", "markdown prose and CRLF preserved");
        var duplicate = ConversionEngine.Transform(landing + "\n" + landing); Check(duplicate.BeforeOccurrences.Count == 2 && duplicate.UniqueConvertedLinks.Count == 1, "duplicates retained and copy list deduplicated");
        var mixed = ConversionEngine.Transform(landing + "\n" + api + "\nhttps://example.com/file"); Check(mixed.AlreadyConvertedLinks.Count == 1 && mixed.UnsupportedLinks.SequenceEqual(new[] { "https://example.com/file" }), "mixed URLs classified");
        Check(!ConversionEngine.Transform(api).Changed, "API input is idempotent");
        Check(!ConversionEngine.Transform("  \r\n").Changed, "empty input is no-op");
        var defaults = AppSettings.Defaults(); Check(defaults.Theme == "System", "system theme is the safe default");
        string root = Path.Combine(Path.GetTempPath(), "plc-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            foreach (var sample in new[] { new { Name="utf8", Encoding=(Encoding)new UTF8Encoding(false), Bom=false }, new { Name="utf8bom", Encoding=(Encoding)new UTF8Encoding(true), Bom=true }, new { Name="utf16", Encoding=(Encoding)new UnicodeEncoding(false,true), Bom=true } })
            {
                string path = Path.Combine(root, sample.Name + ".txt"); string text = "Café 🎵\r\n" + landing;
                TextDocumentIO.AtomicWrite(path, text, sample.Encoding, sample.Bom); var read = TextDocumentIO.Read(path);
                Check(read.Text == text && read.HasBom == sample.Bom && read.Newline == "CRLF", sample.Name + " encoding/BOM/newline round trip");
            }
            string history = Path.Combine(root, "history"); var source = new TextDocument { Text=landing, Encoding=new UTF8Encoding(false), HasBom=false, Newline="None" };
            string one = HistoryManager.Complete(HistoryManager.Stage(history, "Links.txt", null, source, plain, "Convert"));
            string two = HistoryManager.Complete(HistoryManager.Stage(history, "Links.txt", null, source, plain, "Convert"));
            Check(one != two && File.Exists(Path.Combine(one, "Original Document.txt")) && File.Exists(Path.Combine(two, "metadata.json")), "history is append-only and collision-safe");
            string target = Path.Combine(root, "atomic.txt"); TextDocumentIO.AtomicWrite(target, "before", new UTF8Encoding(false), false); TextDocumentIO.AtomicWrite(target, "after", new UTF8Encoding(false), false); Check(File.ReadAllText(target) == "after", "atomic replacement");
            string settingsPath = Path.Combine(root, "settings.json"); defaults.Theme = "Dark"; SettingsService.Save(defaults, settingsPath); Check(SettingsService.Load(settingsPath).Settings.Theme == "Dark", "theme setting persists");
            string huge = String.Join("\n", Enumerable.Repeat(landing, 10000).ToArray()); Check(ConversionEngine.Transform(huge).BeforeOccurrences.Count == 10000, "large batch");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " TEST(S) FAILED"); return failures == 0 ? 0 : 1;
    }
}
