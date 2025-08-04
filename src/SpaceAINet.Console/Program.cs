using SpaceAINet.Console;

// Set UTF-8 output encoding for box-drawing characters
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.CursorVisible = false;

// Show start screen
StartScreen.Show();

// Read user input for speed selection - use safe position
int inputLine = Math.Min(24, Console.WindowHeight - 2);
Console.SetCursorPosition(4, inputLine);
Console.Write("Enter your choice: ");

int gameSpeed = 1; // Default to slow
bool aiMode = false; // Default to manual

while (true)
{
    ConsoleKeyInfo keyInfo = Console.ReadKey(true);

    if (keyInfo.Key == ConsoleKey.Enter)
    {
        // Use defaults (slow, manual)
        break;
    }
    else if (keyInfo.KeyChar == '1')
    {
        gameSpeed = 1; // Slow
        break;
    }
    else if (keyInfo.KeyChar == '2')
    {
        gameSpeed = 2; // Medium
        break;
    }
    else if (keyInfo.KeyChar == '3')
    {
        gameSpeed = 3; // Fast
        break;
    }
    else if (keyInfo.KeyChar == 'A' || keyInfo.KeyChar == 'a')
    {
        aiMode = true;
        break;
    }
    else if (keyInfo.KeyChar == 'M' || keyInfo.KeyChar == 'm')
    {
        aiMode = false;
        break;
    }
}

// Clear console and start game
Console.Clear();

// Run the game
var gameManager = new GameManager(gameSpeed, aiMode);
gameManager.RunGameLoop();

Console.ResetColor();
Console.Clear();
Console.WriteLine("Thanks for playing Space.AI.NET()!");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
