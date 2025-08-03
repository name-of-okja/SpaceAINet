namespace SpaceAINet.Console;

public class Player
{
    public int X { get; set; }
    public int Y { get; set; }
    public int MaxBullets { get; } = 3;
    public int CurrentBullets { get; set; } = 0;
    public char Symbol { get; } = 'A';
    public ConsoleColor Color { get; } = ConsoleColor.Cyan;

    public Player(int startX, int startY)
    {
        X = startX;
        Y = startY;
    }

    public void MoveLeft(int gameAreaLeft)
    {
        if (X > gameAreaLeft + 1)
            X--;
    }

    public void MoveRight(int gameAreaRight)
    {
        if (X < gameAreaRight - 1)
            X++;
    }

    public bool CanShoot()
    {
        return CurrentBullets < MaxBullets;
    }

    public void Shoot()
    {
        if (CanShoot())
            CurrentBullets++;
    }

    public void BulletDestroyed()
    {
        if (CurrentBullets > 0)
            CurrentBullets--;
    }

    public void Render(RenderState renderState)
    {
        renderState.SetPixel(X, Y, Symbol, Color);
    }
}
