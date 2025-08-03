namespace SpaceAINet.Console;

public class Bullet
{
    public int X { get; set; }
    public int Y { get; set; }
    public bool IsPlayerBullet { get; }
    public bool IsActive { get; set; } = true;
    public char Symbol => IsPlayerBullet ? '^' : 'v';
    public ConsoleColor Color { get; } = ConsoleColor.White;

    public Bullet(int x, int y, bool isPlayerBullet)
    {
        X = x;
        Y = y;
        IsPlayerBullet = isPlayerBullet;
    }

    public void Update()
    {
        if (IsPlayerBullet)
            Y--; // Move up
        else
            Y++; // Move down
    }

    public void Render(RenderState renderState)
    {
        if (IsActive)
        {
            renderState.SetPixel(X, Y, Symbol, Color);
        }
    }

    public bool CheckCollision(Enemy enemy)
    {
        if (!IsActive || !enemy.IsAlive || !IsPlayerBullet)
            return false;

        // Check if bullet position overlaps with enemy
        for (int i = 0; i < enemy.Width; i++)
        {
            if (X == enemy.X + i && Y == enemy.Y)
                return true;
        }
        return false;
    }

    public bool CheckCollision(Player player)
    {
        if (!IsActive || IsPlayerBullet)
            return false;

        return X == player.X && Y == player.Y;
    }
}
