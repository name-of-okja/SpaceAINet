namespace SpaceAINet.Console;

public class RenderState
{
    public char[,] CharBuffer { get; }
    public ConsoleColor[,] ColorBuffer { get; }
    public int Width { get; }
    public int Height { get; }

    public RenderState(int width, int height)
    {
        Width = width;
        Height = height;
        CharBuffer = new char[height, width];
        ColorBuffer = new ConsoleColor[height, width];

        // Initialize with empty spaces
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                CharBuffer[y, x] = ' ';
                ColorBuffer[y, x] = ConsoleColor.Black;
            }
        }
    }

    public void SetPixel(int x, int y, char character, ConsoleColor color)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            CharBuffer[y, x] = character;
            ColorBuffer[y, x] = color;
        }
    }

    public char GetChar(int x, int y)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
            return CharBuffer[y, x];
        return ' ';
    }

    public ConsoleColor GetColor(int x, int y)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
            return ColorBuffer[y, x];
        return ConsoleColor.Black;
    }

    public void Clear()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                CharBuffer[y, x] = ' ';
                ColorBuffer[y, x] = ConsoleColor.Black;
            }
        }
    }
}
