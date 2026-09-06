using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PillowcaseLinkConverter
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var window = new MainWindow(args);
            int captureIndex = Array.IndexOf(args, "--capture");
            if (captureIndex >= 0 && captureIndex + 1 < args.Length)
            {
                window.Loaded += delegate
                {
                    if (Array.IndexOf(args, "--dark") >= 0) window.SetDemoTheme("Dark");
                    else if (Array.IndexOf(args, "--light") >= 0) window.SetDemoTheme("Light");
                    window.PrepareDemo(Array.IndexOf(args, "--converted") >= 0);
                    window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate
                    {
                        string path = Path.GetFullPath(args[captureIndex + 1]);
                        try
                        {
                            window.UpdateLayout();
                            var size = new Size(Math.Max(1, window.ActualWidth), Math.Max(1, window.ActualHeight));
                            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                            bitmap.Render(window);
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            using (var stream = File.Create(path)) encoder.Save(stream);
                        }
                        catch (Exception ex) { File.WriteAllText(path + ".error.txt", ex.ToString()); }
                        finally { window.Close(); app.Shutdown(); }
                    }));
                };
            }
            app.Run(window);
        }
    }
}
