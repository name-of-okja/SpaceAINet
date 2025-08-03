using System.Drawing;
using System.Drawing.Imaging;

namespace SpaceAINet.Console;

public class ScreenshotService
{
    private readonly string _screenshotFolder;
    private int _screenshotCounter = 0;

    public ScreenshotService()
    {
        _screenshotFolder = Path.Combine(Directory.GetCurrentDirectory(), "screenshoots");
        InitializeFolder();
    }

    private void InitializeFolder()
    {
        // Clear and create screenshot folder
        if (Directory.Exists(_screenshotFolder))
        {
            Directory.Delete(_screenshotFolder, true);
        }
        Directory.CreateDirectory(_screenshotFolder);
    }

    public void CaptureScreenshot(RenderState renderState, int score, int timeSeconds, int currentBullets, int maxBullets)
    {
        try
        {
            _screenshotCounter++;
            string filename = $"screenshot_{_screenshotCounter:D4}.png";
            string fullPath = Path.Combine(_screenshotFolder, filename);

            // Create bitmap with monospace font rendering
            int charWidth = 8;
            int charHeight = 16;
            int width = renderState.Width * charWidth;
            int height = renderState.Height * charHeight;

            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);

            // Set background
            graphics.Clear(System.Drawing.Color.Black);

            // Use monospace font
            using var font = new Font("Consolas", 10, FontStyle.Regular);

            for (int y = 0; y < renderState.Height; y++)
            {
                for (int x = 0; x < renderState.Width; x++)
                {
                    char character = renderState.GetChar(x, y);
                    ConsoleColor consoleColor = renderState.GetColor(x, y);

                    if (character != ' ')
                    {
                        System.Drawing.Color drawColor = ConvertConsoleColor(consoleColor);
                        using var brush = new SolidBrush(drawColor);

                        graphics.DrawString(
                            character.ToString(),
                            font,
                            brush,
                            x * charWidth,
                            y * charHeight
                        );
                    }
                }
            }

            bitmap.Save(fullPath, ImageFormat.Png);
            System.Console.Title = $"Space.AI.NET() - Screenshot saved: {filename}";
        }
        catch (Exception ex)
        {
            System.Console.Title = $"Space.AI.NET() - Screenshot failed: {ex.Message}";
        }
    }

    private System.Drawing.Color ConvertConsoleColor(ConsoleColor consoleColor)
    {
        return consoleColor switch
        {
            ConsoleColor.Black => System.Drawing.Color.Black,
            ConsoleColor.DarkBlue => System.Drawing.Color.DarkBlue,
            ConsoleColor.DarkGreen => System.Drawing.Color.DarkGreen,
            ConsoleColor.DarkCyan => System.Drawing.Color.DarkCyan,
            ConsoleColor.DarkRed => System.Drawing.Color.DarkRed,
            ConsoleColor.DarkMagenta => System.Drawing.Color.DarkMagenta,
            ConsoleColor.DarkYellow => System.Drawing.Color.Olive,
            ConsoleColor.Gray => System.Drawing.Color.Gray,
            ConsoleColor.DarkGray => System.Drawing.Color.DimGray,
            ConsoleColor.Blue => System.Drawing.Color.Blue,
            ConsoleColor.Green => System.Drawing.Color.Green,
            ConsoleColor.Cyan => System.Drawing.Color.Cyan,
            ConsoleColor.Red => System.Drawing.Color.Red,
            ConsoleColor.Magenta => System.Drawing.Color.Magenta,
            ConsoleColor.Yellow => System.Drawing.Color.Yellow,
            ConsoleColor.White => System.Drawing.Color.White,
            _ => System.Drawing.Color.White
        };
    }
}
