namespace SpaceAINet.Console;

public static class StartScreen
{
    public static void Show()
    {
        System.Console.Clear();

        int windowWidth = System.Console.WindowWidth;
        int windowHeight = System.Console.WindowHeight;

        // Display title centered
        string title = "Space.AI.NET()";
        int titleX = (windowWidth - title.Length) / 2;
        System.Console.SetCursorPosition(titleX, 2);
        System.Console.ForegroundColor = ConsoleColor.Cyan;
        System.Console.WriteLine(title);

        // Display subtitle centered
        string subtitle = "Built with .NET + AI for galactic defense";
        int subtitleX = (windowWidth - subtitle.Length) / 2;
        System.Console.SetCursorPosition(subtitleX, 3);
        System.Console.ForegroundColor = ConsoleColor.Gray;
        System.Console.WriteLine(subtitle);

        // Instructions and speed options - left aligned
        System.Console.SetCursorPosition(4, 6);
        System.Console.ForegroundColor = ConsoleColor.White;
        System.Console.WriteLine("How to Play:");

        System.Console.SetCursorPosition(4, 7);
        System.Console.WriteLine("←   Move Left");

        System.Console.SetCursorPosition(4, 8);
        System.Console.WriteLine("→   Move Right");

        System.Console.SetCursorPosition(4, 9);
        System.Console.WriteLine("SPACE   Shoot");

        System.Console.SetCursorPosition(4, 10);
        System.Console.WriteLine("S   Take Screenshot");

        System.Console.SetCursorPosition(4, 11);
        System.Console.WriteLine("Q   Quit");

        System.Console.SetCursorPosition(4, 12);
        System.Console.WriteLine("R   Restart (after game ends)");

        System.Console.SetCursorPosition(4, 14);
        System.Console.WriteLine("Select Game Speed:");

        System.Console.SetCursorPosition(4, 15);
        System.Console.WriteLine("[1] Slow (default)");

        System.Console.SetCursorPosition(4, 16);
        System.Console.WriteLine("[2] Medium");

        System.Console.SetCursorPosition(4, 17);
        System.Console.WriteLine("[3] Fast");

        System.Console.SetCursorPosition(4, 18);
        System.Console.WriteLine("Press ENTER for default");

        System.Console.ResetColor();
    }
}
