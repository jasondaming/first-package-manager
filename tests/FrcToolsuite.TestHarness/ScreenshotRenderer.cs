using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace FrcToolsuite.TestHarness;

public static class ScreenshotRenderer
{
    public static void RenderToFile(Control view, int width, int height, string outputPath)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = view,
            SizeToContent = SizeToContent.Manual,
            ShowInTaskbar = false,
        };

        window.Show();

        Dispatcher.UIThread.RunJobs();

        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
        view.UpdateLayout();

        Dispatcher.UIThread.RunJobs();

        var pixelSize = new PixelSize(width, height);
        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        bitmap.Render(view);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(outputPath);
        bitmap.Save(stream);

        window.Close();

        Console.WriteLine($"Screenshot saved to {outputPath}");
    }

    public static void RenderWindowToFile(Window window, int width, int height, string outputPath)
    {
        window.Width = width;
        window.Height = height;
        window.SizeToContent = SizeToContent.Manual;
        window.ShowInTaskbar = false;

        window.Show();

        Dispatcher.UIThread.RunJobs();

        window.Measure(new Size(width, height));
        window.Arrange(new Rect(0, 0, width, height));
        window.UpdateLayout();

        Dispatcher.UIThread.RunJobs();

        var pixelSize = new PixelSize(width, height);
        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        bitmap.Render(window);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(outputPath);
        bitmap.Save(stream);

        window.Close();

        Console.WriteLine($"Screenshot saved to {outputPath}");
    }
}
