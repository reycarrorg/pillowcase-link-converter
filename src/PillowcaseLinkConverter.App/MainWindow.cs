using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace PillowcaseLinkConverter
{
    public sealed class MainWindow : Window
    {
        readonly TextBox editor = new TextBox();
        readonly TextBlock documentLabel = new TextBlock();
        readonly TextBlock status = new TextBlock();
        readonly Border resultCard = new Border();
        readonly TextBlock resultSummary = new TextBlock();
        readonly ProgressBar progress = new ProgressBar();
        readonly Button convertButton;
        readonly Menu mainMenu;
        readonly Border headerBorder;
        readonly Border footerBorder;
        AppSettings settings;
        string currentPath;
        string savedText = "";
        Encoding currentEncoding = new UTF8Encoding(false);
        bool currentBom;
        bool suppressDirty;
        string lastHistory;
        ConversionResult lastResult;

        public MainWindow(string[] args)
        {
            Title = "Pillowcase Link Converter";
            Width = 1120; Height = 760; MinWidth = 760; MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 252));
            FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
            var loaded = SettingsService.Load(SettingsService.DefaultPath);
            settings = loaded.Settings;

            var root = new DockPanel(); Content = root;
            mainMenu = BuildMenu(); root.Children.Add(mainMenu);
            DockPanel.SetDock(root.Children[root.Children.Count - 1], Dock.Top);

            headerBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(25, 35, 57)), Padding = new Thickness(26, 18, 26, 16) };
            var headerGrid = new Grid(); headerGrid.ColumnDefinitions.Add(new ColumnDefinition()); headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock { Text = "Pillowcase Link Converter", Foreground = Brushes.White, FontSize = 25, FontWeight = FontWeights.SemiBold });
            titleStack.Children.Add(new TextBlock { Text = "Turn share-page links into JDownloader-ready API links — locally and safely.", Foreground = new SolidColorBrush(Color.FromRgb(195, 207, 229)), Margin = new Thickness(0, 4, 0, 0) });
            headerGrid.Children.Add(titleStack);
            convertButton = Button("Convert links", "Ctrl+Enter", Convert, true); convertButton.Padding = new Thickness(22, 11, 22, 11); Grid.SetColumn(convertButton, 1); headerGrid.Children.Add(convertButton);
            headerBorder.Child = headerGrid; DockPanel.SetDock(headerBorder, Dock.Top); root.Children.Add(headerBorder);

            footerBorder = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(221, 226, 235)), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(22, 9, 22, 9) };
            status.Text = "Ready — paste links, type, or open a text file."; status.Foreground = new SolidColorBrush(Color.FromRgb(67, 77, 96)); footerBorder.Child = status; DockPanel.SetDock(footerBorder, Dock.Bottom); root.Children.Add(footerBorder);

            var body = new Grid { Margin = new Thickness(24) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 10) }; toolbar.ColumnDefinitions.Add(new ColumnDefinition()); toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            documentLabel.FontWeight = FontWeights.SemiBold; documentLabel.FontSize = 16; toolbar.Children.Add(documentLabel);
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(Button("New", "Ctrl+N", delegate { NewDocument(); }, false)); actions.Children.Add(Button("Open…", "Ctrl+O", Open, false)); actions.Children.Add(Button("Save", "Ctrl+S", Save, false));
            Grid.SetColumn(actions, 1); toolbar.Children.Add(actions); body.Children.Add(toolbar);

            editor.AcceptsReturn = true; editor.AcceptsTab = true; editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            editor.FontFamily = new FontFamily("Consolas"); editor.FontSize = 15; editor.Padding = new Thickness(14); editor.Background = Brushes.White; editor.BorderBrush = new SolidColorBrush(Color.FromRgb(184, 194, 210)); editor.BorderThickness = new Thickness(1);
            editor.TextWrapping = TextWrapping.NoWrap; editor.TextChanged += delegate { if (!suppressDirty) UpdateTitle(); };
            Grid.SetRow(editor, 1); body.Children.Add(editor);

            resultCard.Background = Brushes.White; resultCard.BorderBrush = new SolidColorBrush(Color.FromRgb(205, 214, 228)); resultCard.BorderThickness = new Thickness(1); resultCard.CornerRadius = new CornerRadius(8); resultCard.Padding = new Thickness(16); resultCard.Margin = new Thickness(0, 14, 0, 0); resultCard.Visibility = Visibility.Collapsed;
            var resultGrid = new Grid(); resultGrid.ColumnDefinitions.Add(new ColumnDefinition()); resultGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            resultSummary.TextWrapping = TextWrapping.Wrap; resultGrid.Children.Add(resultSummary);
            var resultButtons = new StackPanel { Orientation = Orientation.Horizontal };
            resultButtons.Children.Add(Button("Copy links", null, CopyLinks, false)); resultButtons.Children.Add(Button("Open history", null, delegate { OpenPath(settings.HistoryDirectory); }, false)); resultButtons.Children.Add(Button("Restore before", null, Restore, false));
            Grid.SetColumn(resultButtons, 1); resultGrid.Children.Add(resultButtons); resultCard.Child = resultGrid; Grid.SetRow(resultCard, 2); body.Children.Add(resultCard);
            progress.Height = 3; progress.IsIndeterminate = true; progress.Visibility = Visibility.Collapsed; Grid.SetRow(progress, 2); body.Children.Add(progress);
            root.Children.Add(body);

            AllowDrop = true; DragOver += delegate(object s, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            Drop += delegate(object s, DragEventArgs e) { var files = e.Data.GetData(DataFormats.FileDrop) as string[]; if (files != null && files.Length > 0) OpenFile(files[0]); };
            Closing += delegate(object s, System.ComponentModel.CancelEventArgs e) { if (!ConfirmDiscard()) e.Cancel = true; };
            InputBindings.Add(new KeyBinding(new RelayCommand(delegate { NewDocument(); }), Key.N, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(Open), Key.O, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(Save), Key.S, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(SaveAs), Key.S, ModifierKeys.Control | ModifierKeys.Shift));
            InputBindings.Add(new KeyBinding(new RelayCommand(Convert), Key.Enter, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(ShowHelp), Key.F1, ModifierKeys.None));
            NewDocument(false);
            ApplyTheme();
            Loaded += delegate { if (!String.IsNullOrEmpty(loaded.Warning)) MessageBox.Show(this, loaded.Warning, "Settings recovered", MessageBoxButton.OK, MessageBoxImage.Warning); if (!settings.WelcomeSeen && Array.IndexOf(args, "--capture") < 0) ShowWelcome(); if (args.Length > 0 && File.Exists(args[0])) OpenFile(args[0]); };
        }

        public void PrepareDemo(bool converted)
        {
            currentPath = null;
            string id = "0123456789abcdef0123456789abcdef";
            string before = "Weekend archive\r\n\r\n[Opening set](https://pillows.su/f/" + id + ")\r\nhttps://example.com/reference-stays-unchanged\r\n\r\nPaste a mixed list, then convert it without losing your notes.";
            var demoResult = ConversionEngine.Transform(before);
            SetText(converted ? demoResult.ConvertedText : before);
            savedText = editor.Text; UpdateTitle();
            resultSummary.Text = converted ? "Converted 1 occurrence into 1 unique download link. Left 1 other URL untouched. Before and after snapshots were saved in History." : "Ready for a safe preview: matching links will change in place; notes and unsupported URLs stay untouched.";
            resultCard.Visibility = Visibility.Visible;
            status.Text = converted ? "Conversion complete. Copy the link list and paste it into JDownloader LinkGrabber." : "Demo text is fictional. Processing stays on this computer.";
        }

        public void SetDemoTheme(string value) { settings.Theme = value; ApplyTheme(); }

        Menu BuildMenu()
        {
            var menu = new Menu();
            var file = new MenuItem { Header = "_File" }; Add(file, "_New", NewDocument); Add(file, "_Open…", Open); Add(file, "_Save", Save); Add(file, "Save _As…", SaveAs); file.Items.Add(new Separator()); Add(file, "E_xit", Close); menu.Items.Add(file);
            var tools = new MenuItem { Header = "_Tools" }; Add(tools, "_Convert links", Convert); Add(tools, "_Settings…", ShowSettings); Add(tools, "Open _history folder", delegate { OpenPath(settings.HistoryDirectory); }); menu.Items.Add(tools);
            var help = new MenuItem { Header = "_Help" }; Add(help, "_How to use", ShowHelp); Add(help, "_Welcome tour", ShowWelcome); Add(help, "_About", delegate { MessageBox.Show(this, "Pillowcase Link Converter 2.0\n\nLocal-only. No accounts, telemetry, cookies, or network requests.\nIt converts text; JDownloader handles downloads.", "About"); }); menu.Items.Add(help);
            return menu;
        }

        void Add(MenuItem parent, string label, Action action) { var item = new MenuItem { Header = label }; item.Click += delegate { action(); }; parent.Items.Add(item); }
        Button Button(string text, string tip, Action action, bool primary)
        {
            var b = new Button { Content = text, Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(13, 7, 13, 7), MinWidth = 78, ToolTip = tip };
            if (primary) { b.Background = new SolidColorBrush(Color.FromRgb(80, 107, 255)); b.Foreground = Brushes.White; b.BorderBrush = b.Background; }
            b.Click += delegate { action(); }; return b;
        }

        void NewDocument() { NewDocument(true); }
        void NewDocument(bool ask)
        {
            if (ask && !ConfirmDiscard()) return;
            currentPath = null; currentEncoding = new UTF8Encoding(false); currentBom = false; SetText(""); savedText = ""; lastHistory = null; lastResult = null; resultCard.Visibility = Visibility.Collapsed; status.Text = "New document — paste or type links here."; UpdateTitle(); editor.Focus();
        }
        bool ConfirmDiscard()
        {
            if (editor.Text == savedText) return true;
            var result = MessageBox.Show(this, "Save your changes before continuing?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) return false;
            if (result == MessageBoxResult.Yes) return SaveCore(false);
            return true;
        }
        void Open()
        {
            if (!ConfirmDiscard()) return;
            var dialog = new OpenFileDialog { Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) == true) OpenFile(dialog.FileName);
        }
        void OpenFile(string path)
        {
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException("The selected file was not found.", path);
                var doc = TextDocumentIO.Read(path); currentPath = Path.GetFullPath(path); currentEncoding = doc.Encoding; currentBom = doc.HasBom; SetText(doc.Text); savedText = doc.Text; resultCard.Visibility = Visibility.Collapsed; lastHistory = null; UpdateTitle(); status.Text = "Opened " + Path.GetFileName(path) + " — encoding and line endings will be preserved.";
            }
            catch (Exception ex) { Error("Could not open that file", ex); }
        }
        void Save() { SaveCore(false); }
        void SaveAs() { SaveCore(true); }
        bool SaveCore(bool forceDialog)
        {
            string path = currentPath;
            if (forceDialog || String.IsNullOrEmpty(path))
            {
                var dialog = new SaveFileDialog { Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*", FileName = String.IsNullOrEmpty(path) ? "Pillowcase Links.txt" : Path.GetFileName(path) };
                if (dialog.ShowDialog(this) != true) return false; path = dialog.FileName;
            }
            try { TextDocumentIO.AtomicWrite(path, editor.Text, currentEncoding, currentBom); currentPath = Path.GetFullPath(path); savedText = editor.Text; UpdateTitle(); status.Text = "Saved safely to " + currentPath; return true; }
            catch (Exception ex) { Error("The document could not be saved. Your editor text is still here.", ex); return false; }
        }
        void Convert()
        {
            progress.Visibility = Visibility.Visible; resultCard.Visibility = Visibility.Collapsed; convertButton.IsEnabled = false;
            try
            {
                var result = ConversionEngine.Transform(editor.Text);
                if (!result.Changed)
                {
                    lastResult = result; resultSummary.Text = "Nothing needed changing. " + result.AlreadyConvertedLinks.Count + " API link(s) were already ready; " + result.UnsupportedLinks.Count + " other URL(s) were left untouched."; resultCard.Visibility = Visibility.Visible; status.Text = "No matching Pillowcase share-page links found — no file or history was changed."; return;
                }
                if (!String.IsNullOrEmpty(currentPath) && settings.WarnBeforeUpdatingFile)
                {
                    if (MessageBox.Show(this, "This will create a complete before/after history snapshot, then safely replace the matching links in:\n\n" + currentPath + "\n\nContinue?", "Update opened text file", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) { status.Text = "Conversion cancelled — nothing was changed."; return; }
                }
                var doc = new TextDocument { Text = editor.Text, Encoding = currentEncoding, HasBom = currentBom, Newline = DetectNewline(editor.Text) };
                var draft = HistoryManager.Stage(settings.HistoryDirectory, String.IsNullOrEmpty(currentPath) ? "Untitled.txt" : Path.GetFileName(currentPath), currentPath, doc, result, "Convert");
                try
                {
                    if (!String.IsNullOrEmpty(currentPath)) TextDocumentIO.AtomicWrite(currentPath, result.ConvertedText, currentEncoding, currentBom);
                    lastHistory = HistoryManager.Complete(draft);
                }
                catch
                {
                    if (!String.IsNullOrEmpty(currentPath)) TextDocumentIO.AtomicWrite(currentPath, result.OriginalText, currentEncoding, currentBom);
                    HistoryManager.Abandon(draft); throw;
                }
                SetText(result.ConvertedText); if (!String.IsNullOrEmpty(currentPath)) savedText = result.ConvertedText; lastResult = result; UpdateTitle();
                string copied = ""; if (settings.CopyAfterConversion) { try { Clipboard.SetText(String.Join(Environment.NewLine, result.UniqueConvertedLinks)); copied = " Copied the unique converted links to your clipboard."; } catch { copied = " Clipboard copy was unavailable; use Copy links."; } }
                resultSummary.Text = "Converted " + result.BeforeOccurrences.Count + " occurrence(s) into " + result.UniqueConvertedLinks.Count + " unique download link(s). Left " + result.UnsupportedLinks.Count + " other URL(s) untouched." + copied;
                resultCard.Visibility = Visibility.Visible; status.Text = "Conversion complete. Before and after snapshots are saved in history.";
            }
            catch (Exception ex) { Error("Conversion could not be completed. The app kept the original document whenever possible.", ex); }
            finally { progress.Visibility = Visibility.Collapsed; convertButton.IsEnabled = true; }
        }
        void CopyLinks()
        {
            if (lastResult == null || lastResult.UniqueConvertedLinks.Count == 0) { status.Text = "There are no newly converted links to copy."; return; }
            try { Clipboard.SetText(String.Join(Environment.NewLine, lastResult.UniqueConvertedLinks)); status.Text = "Unique converted links copied. Paste them into JDownloader LinkGrabber."; } catch (Exception ex) { Error("Clipboard copy failed", ex); }
        }
        void Restore()
        {
            if (String.IsNullOrEmpty(lastHistory)) { status.Text = "There is no conversion from this session to restore."; return; }
            string originalPath = Path.Combine(lastHistory, "Original Document.txt");
            try
            {
                string original = File.ReadAllText(originalPath, new UTF8Encoding(false));
                if (MessageBox.Show(this, "Restore the editor to the before-conversion snapshot? A new Restore history record will be created.", "Restore before conversion", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                var restore = new ConversionResult { OriginalText = editor.Text, ConvertedText = original, BeforeOccurrences = new System.Collections.Generic.List<string>(), AfterOccurrences = new System.Collections.Generic.List<string>(), UniqueConvertedLinks = new System.Collections.Generic.List<string>(), UnsupportedLinks = new System.Collections.Generic.List<string>(), AlreadyConvertedLinks = new System.Collections.Generic.List<string>() };
                var doc = new TextDocument { Text = editor.Text, Encoding = currentEncoding, HasBom = currentBom, Newline = DetectNewline(editor.Text) };
                var draft = HistoryManager.Stage(settings.HistoryDirectory, String.IsNullOrEmpty(currentPath) ? "Untitled.txt" : Path.GetFileName(currentPath), currentPath, doc, restore, "Restore");
                if (!String.IsNullOrEmpty(currentPath)) TextDocumentIO.AtomicWrite(currentPath, original, currentEncoding, currentBom);
                lastHistory = HistoryManager.Complete(draft); SetText(original); if (!String.IsNullOrEmpty(currentPath)) savedText = original; UpdateTitle(); status.Text = "Restored the before-conversion text. The restore is recorded in history.";
            }
            catch (Exception ex) { Error("The snapshot could not be restored", ex); }
        }
        void ShowSettings()
        {
            var dialog = new Window { Owner = this, Title = "Settings", Width = 620, Height = 390, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Brushes.White };
            var stack = new StackPanel { Margin = new Thickness(26) }; dialog.Content = stack;
            stack.Children.Add(new TextBlock { Text = "Settings", FontSize = 23, FontWeight = FontWeights.SemiBold });
            stack.Children.Add(new TextBlock { Text = "History folder", Margin = new Thickness(0, 20, 0, 5), FontWeight = FontWeights.SemiBold });
            var path = new TextBox { Text = settings.HistoryDirectory, Padding = new Thickness(8) }; stack.Children.Add(path);
            var browse = Button("Browse…", null, delegate { using (var f = new Forms.FolderBrowserDialog { SelectedPath = path.Text, Description = "Choose a history folder" }) if (f.ShowDialog() == Forms.DialogResult.OK) path.Text = f.SelectedPath; }, false); browse.HorizontalAlignment = HorizontalAlignment.Left; browse.Margin = new Thickness(0, 7, 0, 10); stack.Children.Add(browse);
            var copy = new CheckBox { Content = "Copy unique converted links to the clipboard after conversion", IsChecked = settings.CopyAfterConversion, Margin = new Thickness(0, 8, 0, 5) };
            var warn = new CheckBox { Content = "Ask before updating an opened text file", IsChecked = settings.WarnBeforeUpdatingFile, Margin = new Thickness(0, 5, 0, 14) }; stack.Children.Add(copy); stack.Children.Add(warn);
            var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 14) };
            themeRow.Children.Add(new TextBlock { Text = "Appearance", Width = 110, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold });
            var theme = new ComboBox { Width = 180, Padding = new Thickness(6) }; theme.Items.Add("System"); theme.Items.Add("Light"); theme.Items.Add("Dark"); theme.SelectedItem = settings.Theme; themeRow.Children.Add(theme); stack.Children.Add(themeRow);
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; var cancel = Button("Cancel", null, dialog.Close, false); var save = Button("Save settings", null, delegate { if (String.IsNullOrWhiteSpace(path.Text)) { MessageBox.Show(dialog, "Choose a history folder."); return; } settings.HistoryDirectory = Path.GetFullPath(path.Text); settings.CopyAfterConversion = copy.IsChecked == true; settings.WarnBeforeUpdatingFile = warn.IsChecked == true; settings.Theme = theme.SelectedItem == null ? "System" : theme.SelectedItem.ToString(); SettingsService.Save(settings, SettingsService.DefaultPath); ApplyTheme(); dialog.DialogResult = true; }, true); row.Children.Add(cancel); row.Children.Add(save); stack.Children.Add(row); ApplyThemeTo(dialog); dialog.ShowDialog();
        }
        void ShowWelcome()
        {
            var dialog = InfoWindow("Welcome", "A safer, clearer way to prepare Pillowcase links", "1. Paste links here or open a .txt file.\n\n2. Select Convert links. The matching URLs change in place, while notes, Markdown, duplicates, and line order stay intact.\n\n3. Paste the copied API links into JDownloader LinkGrabber.\n\nEvery changed conversion stores before/after snapshots in History. This app does not download anything or send your text online."); dialog.Closed += delegate { settings.WelcomeSeen = true; try { SettingsService.Save(settings, SettingsService.DefaultPath); } catch { } }; dialog.ShowDialog();
        }
        void ShowHelp() { InfoWindow("How to use", "From a link list to JDownloader", "Paste or open text containing links like:\nhttps://pillows.su/f/0123456789abcdef0123456789abcdef\n\nChoose Convert links (Ctrl+Enter). The editor becomes:\nhttps://api.pillows.su/api/download/0123456789abcdef0123456789abcdef\n\nUse Copy links, then paste into JDownloader's LinkGrabber. Unsupported websites remain unchanged and are listed in the conversion's History folder.\n\nShortcuts: Ctrl+N New • Ctrl+O Open • Ctrl+S Save • Ctrl+Shift+S Save As • Ctrl+Enter Convert • F1 Help").ShowDialog(); }
        Window InfoWindow(string title, string heading, string copy)
        {
            var window = new Window { Owner = this, Title = title, Width = 650, Height = 490, MinWidth = 540, MinHeight = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brushes.White };
            var grid = new Grid { Margin = new Thickness(34) }; grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = heading, FontSize = 25, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap }); stack.Children.Add(new TextBlock { Text = copy, FontSize = 15, LineHeight = 24, Margin = new Thickness(0, 22, 0, 0), TextWrapping = TextWrapping.Wrap }); grid.Children.Add(stack);
            var close = Button("Got it", null, window.Close, true); close.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetRow(close, 1); grid.Children.Add(close); window.Content = grid; ApplyThemeTo(window); return window;
        }

        bool IsDark()
        {
            if (String.Equals(settings.Theme, "Dark", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch { return false; }
        }
        void ApplyTheme() { ApplyThemeTo(this); headerBorder.Background = new SolidColorBrush(IsDark() ? Color.FromRgb(13, 20, 36) : Color.FromRgb(25, 35, 57)); convertButton.Background = new SolidColorBrush(Color.FromRgb(80, 107, 255)); convertButton.Foreground = Brushes.White; }
        void ApplyThemeTo(Window window)
        {
            bool dark = IsDark(); Brush canvas = new SolidColorBrush(dark ? Color.FromRgb(15, 20, 31) : Color.FromRgb(247, 249, 252)); Brush surface = new SolidColorBrush(dark ? Color.FromRgb(28, 36, 51) : Colors.White); Brush field = new SolidColorBrush(dark ? Color.FromRgb(20, 27, 40) : Colors.White); Brush text = new SolidColorBrush(dark ? Color.FromRgb(232, 237, 247) : Color.FromRgb(25, 31, 43)); Brush muted = new SolidColorBrush(dark ? Color.FromRgb(170, 183, 207) : Color.FromRgb(67, 77, 96)); Brush border = new SolidColorBrush(dark ? Color.FromRgb(62, 75, 99) : Color.FromRgb(205, 214, 228));
            window.Background = canvas; PaintTree(window.Content as DependencyObject, dark, surface, field, text, muted, border);
            if (window == this) { Background = canvas; editor.Background = field; editor.Foreground = text; editor.BorderBrush = border; resultCard.Background = surface; resultCard.BorderBrush = border; footerBorder.Background = surface; footerBorder.BorderBrush = border; status.Foreground = muted; documentLabel.Foreground = text; mainMenu.Background = surface; mainMenu.Foreground = text; }
        }
        void PaintTree(DependencyObject node, bool dark, Brush surface, Brush field, Brush text, Brush muted, Brush border)
        {
            if (node == null) return;
            var tb = node as TextBlock; if (tb != null && !IsUnderHeader(tb)) tb.Foreground = text;
            var box = node as TextBox; if (box != null) { box.Background = field; box.Foreground = text; box.BorderBrush = border; }
            var combo = node as ComboBox; if (combo != null) { combo.Background = field; combo.Foreground = text; }
            var check = node as CheckBox; if (check != null) check.Foreground = text;
            var button = node as Button; if (button != null && button != convertButton && button.Foreground != Brushes.White) { button.Background = surface; button.Foreground = text; button.BorderBrush = border; }
            int count = VisualTreeHelper.GetChildrenCount(node); for (int i = 0; i < count; i++) PaintTree(VisualTreeHelper.GetChild(node, i), dark, surface, field, text, muted, border);
        }
        bool IsUnderHeader(DependencyObject node) { while (node != null) { if (node == headerBorder) return true; node = VisualTreeHelper.GetParent(node); } return false; }
        void SetText(string text) { suppressDirty = true; editor.Text = text ?? ""; suppressDirty = false; }
        void UpdateTitle() { bool dirty = editor.Text != savedText; string name = String.IsNullOrEmpty(currentPath) ? "Untitled" : Path.GetFileName(currentPath); documentLabel.Text = name + (dirty ? "  •  Unsaved changes" : ""); Title = (dirty ? "*" : "") + name + " — Pillowcase Link Converter"; }
        static string DetectNewline(string text) { return text.Contains("\r\n") ? "CRLF" : text.Contains("\n") ? "LF" : text.Contains("\r") ? "CR" : "None"; }
        void OpenPath(string path) { try { Directory.CreateDirectory(path); Process.Start("explorer.exe", "\"" + path + "\""); } catch (Exception ex) { Error("Could not open that folder", ex); } }
        void Error(string message, Exception ex) { status.Text = message; MessageBox.Show(this, message + "\n\n" + ex.Message, "Pillowcase Link Converter", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    public sealed class RelayCommand : ICommand
    {
        readonly Action action; public RelayCommand(Action value) { action = value; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { action(); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
