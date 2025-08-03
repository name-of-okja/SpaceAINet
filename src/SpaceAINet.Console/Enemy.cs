namespace SpaceAINet.Console;

public class Enemy
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Symbol { get; }
    public ConsoleColor Color { get; }
    public bool IsAlive { get; set; } = true;
    public EnemyType Type { get; }

    public Enemy(int x, int y, EnemyType type)
    {
        X = x;
        Y = y;
        Type = type;

        switch (type)
        {
            case EnemyType.TopRow1:
            case EnemyType.TopRow3:
            case EnemyType.TopRow5:
                Symbol = "><";
                Color = ConsoleColor.Red;
                break;
            case EnemyType.TopRow2:
            case EnemyType.TopRow4:
                Symbol = "oo";
                Color = ConsoleColor.Red;
                break;
            case EnemyType.BottomRow:
                Symbol = "/O\\";
                Color = ConsoleColor.DarkYellow;
                break;
        }
    }

    public void Move(int deltaX, int deltaY)
    {
        X += deltaX;
        Y += deltaY;
    }

    public void Render(RenderState renderState)
    {
        if (!IsAlive) return;

        for (int i = 0; i < Symbol.Length; i++)
        {
            renderState.SetPixel(X + i, Y, Symbol[i], Color);
        }
    }

    public int Width => Symbol.Length;
}

public enum EnemyType
{
    TopRow1,
    TopRow2,
    TopRow3,
    TopRow4,
    TopRow5,
    BottomRow
}
