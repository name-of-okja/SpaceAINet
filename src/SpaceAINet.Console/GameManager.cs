namespace SpaceAINet.Console;

public class GameManager
{
    private RenderState _currentRenderState;
    private RenderState _previousRenderState;
    private Player _player;
    private List<Enemy> _enemies;
    private List<Bullet> _bullets;
    private ScreenshotService _screenshotService;

    private int _gameAreaLeft = 1;
    private int _gameAreaRight;
    private int _gameAreaTop = 3;
    private int _gameAreaBottom;
    private int _score = 0;
    private DateTime _gameStartTime;
    private int _gameSpeed;
    private bool _gameRunning = true;
    private bool _movingRight = true;
    private DateTime _lastEnemyMove;
    private DateTime _lastEnemyShoot;
    private Random _random = new Random();

    public GameManager(int gameSpeed = 1)
    {
        _gameSpeed = gameSpeed;
        _gameStartTime = DateTime.Now;

        // Set UTF-8 encoding for Unicode box-drawing characters
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;
        System.Console.CursorVisible = false;

        int width = System.Console.WindowWidth;
        int height = System.Console.WindowHeight;

        _gameAreaRight = width - 2;
        _gameAreaBottom = height - 2;

        _currentRenderState = new RenderState(width, height);
        _previousRenderState = new RenderState(width, height);

        _screenshotService = new ScreenshotService();

        InitializeGame();

        _lastEnemyMove = DateTime.Now;
        _lastEnemyShoot = DateTime.Now;
    }

    private void InitializeGame()
    {
        // Initialize player at bottom center
        int playerX = (_gameAreaLeft + _gameAreaRight) / 2;
        int playerY = _gameAreaBottom - 2;
        _player = new Player(playerX, playerY);

        // Initialize enemies
        _enemies = new List<Enemy>();
        _bullets = new List<Bullet>();

        // Top row (5 enemies)
        int startX = _gameAreaLeft + 5;
        int spacing = 6;

        for (int i = 0; i < 5; i++)
        {
            EnemyType type = i switch
            {
                0 => EnemyType.TopRow1,
                1 => EnemyType.TopRow2,
                2 => EnemyType.TopRow3,
                3 => EnemyType.TopRow4,
                4 => EnemyType.TopRow5,
                _ => EnemyType.TopRow1
            };

            _enemies.Add(new Enemy(startX + i * spacing, _gameAreaTop + 2, type));
        }

        // Bottom row (3 enemies)
        startX = _gameAreaLeft + 8;
        spacing = 8;
        for (int i = 0; i < 3; i++)
        {
            _enemies.Add(new Enemy(startX + i * spacing, _gameAreaTop + 4, EnemyType.BottomRow));
        }
    }

    public void RunGameLoop()
    {
        while (_gameRunning)
        {
            HandleInput();
            UpdateGame();
            Render();

            // Game speed control
            int delay = _gameSpeed switch
            {
                1 => 100, // Slow
                2 => 75,  // Medium
                3 => 50,  // Fast
                _ => 100
            };

            Thread.Sleep(delay);
        }
    }

    private void HandleInput()
    {
        if (System.Console.KeyAvailable)
        {
            ConsoleKeyInfo keyInfo = System.Console.ReadKey(true);

            switch (keyInfo.Key)
            {
                case ConsoleKey.LeftArrow:
                    _player.MoveLeft(_gameAreaLeft);
                    break;

                case ConsoleKey.RightArrow:
                    _player.MoveRight(_gameAreaRight);
                    break;

                case ConsoleKey.Spacebar:
                    if (_player.CanShoot())
                    {
                        _bullets.Add(new Bullet(_player.X, _player.Y - 1, true));
                        _player.Shoot();
                    }
                    break;

                case ConsoleKey.S:
                    CaptureScreenshot();
                    break;

                case ConsoleKey.Q:
                    _gameRunning = false;
                    break;
            }
        }
    }

    private void UpdateGame()
    {
        // Update bullets
        var bulletsToRemove = new List<Bullet>();

        foreach (var bullet in _bullets)
        {
            bullet.Update();

            // Check if bullet is out of bounds
            if (bullet.Y < _gameAreaTop || bullet.Y > _gameAreaBottom)
            {
                bulletsToRemove.Add(bullet);
                if (bullet.IsPlayerBullet)
                    _player.BulletDestroyed();
            }
            else
            {
                // Check collisions with enemies (player bullets)
                if (bullet.IsPlayerBullet)
                {
                    foreach (var enemy in _enemies.Where(e => e.IsAlive))
                    {
                        if (bullet.CheckCollision(enemy))
                        {
                            enemy.IsAlive = false;
                            bulletsToRemove.Add(bullet);
                            _player.BulletDestroyed();
                            _score += 10;
                            break;
                        }
                    }
                }
                // Check collision with player (enemy bullets)
                else
                {
                    if (bullet.CheckCollision(_player))
                    {
                        if (ShowGameOverScreen())
                        {
                            RestartGame();
                            return; // Exit to restart game loop
                        }
                        else
                        {
                            _gameRunning = false;
                            return; // Exit immediately to avoid further processing
                        }
                    }
                }
            }
        }

        // Remove bullets
        foreach (var bullet in bulletsToRemove)
        {
            _bullets.Remove(bullet);
        }

        // Move enemies
        UpdateEnemies();

        // Enemy shooting
        UpdateEnemyShooting();

        // Check win condition
        if (_enemies.All(e => !e.IsAlive))
        {
            if (ShowWinScreen())
            {
                RestartGame();
            }
            else
            {
                _gameRunning = false;
            }
        }
    }

    private void UpdateEnemies()
    {
        var now = DateTime.Now;
        var enemyMoveDelay = TimeSpan.FromMilliseconds(500);

        if (now - _lastEnemyMove >= enemyMoveDelay)
        {
            bool hitEdge = false;

            // Check if any enemy hits the edge
            foreach (var enemy in _enemies.Where(e => e.IsAlive))
            {
                if (_movingRight && enemy.X + enemy.Width >= _gameAreaRight - 1)
                {
                    hitEdge = true;
                    break;
                }
                else if (!_movingRight && enemy.X <= _gameAreaLeft + 1)
                {
                    hitEdge = true;
                    break;
                }
            }

            // Move enemies
            foreach (var enemy in _enemies.Where(e => e.IsAlive))
            {
                if (hitEdge)
                {
                    enemy.Move(0, 1); // Move down
                }
                else
                {
                    enemy.Move(_movingRight ? 1 : -1, 0); // Move horizontally
                }
            }

            // Change direction if hit edge
            if (hitEdge)
            {
                _movingRight = !_movingRight;
            }

            _lastEnemyMove = now;
        }
    }

    private void UpdateEnemyShooting()
    {
        var now = DateTime.Now;
        var shootDelay = TimeSpan.FromSeconds(2);

        if (now - _lastEnemyShoot >= shootDelay)
        {
            // Only one enemy can shoot at a time
            var aliveEnemies = _enemies.Where(e => e.IsAlive).ToList();
            if (aliveEnemies.Count > 0)
            {
                var shootingEnemy = aliveEnemies[_random.Next(aliveEnemies.Count)];
                _bullets.Add(new Bullet(shootingEnemy.X + shootingEnemy.Width / 2, shootingEnemy.Y + 1, false));
            }

            _lastEnemyShoot = now;
        }
    }

    private void Render()
    {
        // Clear current render state
        _currentRenderState.Clear();

        // Draw border
        DrawBorder();

        // Draw UI
        DrawUI();

        // Render game objects
        _player.Render(_currentRenderState);

        foreach (var enemy in _enemies)
        {
            enemy.Render(_currentRenderState);
        }

        foreach (var bullet in _bullets)
        {
            bullet.Render(_currentRenderState);
        }

        // Double-buffered rendering: only update changed characters
        for (int y = 0; y < _currentRenderState.Height; y++)
        {
            for (int x = 0; x < _currentRenderState.Width; x++)
            {
                char currentChar = _currentRenderState.GetChar(x, y);
                char previousChar = _previousRenderState.GetChar(x, y);
                ConsoleColor currentColor = _currentRenderState.GetColor(x, y);
                ConsoleColor previousColor = _previousRenderState.GetColor(x, y);

                if (currentChar != previousChar || currentColor != previousColor)
                {
                    System.Console.SetCursorPosition(x, y);
                    System.Console.ForegroundColor = currentColor;
                    System.Console.Write(currentChar);
                }
            }
        }

        // Swap buffers
        var temp = _previousRenderState;
        _previousRenderState = _currentRenderState;
        _currentRenderState = temp;
    }

    private void DrawBorder()
    {
        // Top border
        _currentRenderState.SetPixel(_gameAreaLeft, _gameAreaTop, '┌', ConsoleColor.White);
        for (int x = _gameAreaLeft + 1; x < _gameAreaRight; x++)
        {
            _currentRenderState.SetPixel(x, _gameAreaTop, '─', ConsoleColor.White);
        }
        _currentRenderState.SetPixel(_gameAreaRight, _gameAreaTop, '┐', ConsoleColor.White);

        // Bottom border
        _currentRenderState.SetPixel(_gameAreaLeft, _gameAreaBottom, '└', ConsoleColor.White);
        for (int x = _gameAreaLeft + 1; x < _gameAreaRight; x++)
        {
            _currentRenderState.SetPixel(x, _gameAreaBottom, '─', ConsoleColor.White);
        }
        _currentRenderState.SetPixel(_gameAreaRight, _gameAreaBottom, '┘', ConsoleColor.White);

        // Side borders
        for (int y = _gameAreaTop + 1; y < _gameAreaBottom; y++)
        {
            _currentRenderState.SetPixel(_gameAreaLeft, y, '│', ConsoleColor.White);
            _currentRenderState.SetPixel(_gameAreaRight, y, '│', ConsoleColor.White);
        }
    }

    private void DrawUI()
    {
        int elapsedSeconds = (int)(DateTime.Now - _gameStartTime).TotalSeconds;
        string ui = $"Score: {_score:D4}   Time: {elapsedSeconds:D2}s   Bullets: {_player.CurrentBullets}/{_player.MaxBullets}";

        // Draw UI inside the border
        int uiY = _gameAreaTop + 1;
        int uiX = _gameAreaLeft + 2;

        for (int i = 0; i < ui.Length && uiX + i < _gameAreaRight; i++)
        {
            _currentRenderState.SetPixel(uiX + i, uiY, ui[i], ConsoleColor.White);
        }
    }

    public RenderState GetRenderState()
    {
        return _currentRenderState;
    }

    private void CaptureScreenshot()
    {
        int elapsedSeconds = (int)(DateTime.Now - _gameStartTime).TotalSeconds;
        _screenshotService.CaptureScreenshot(_currentRenderState, _score, elapsedSeconds, _player.CurrentBullets, _player.MaxBullets);
    }

    private void RestartGame()
    {
        // Reset game state
        _score = 0;
        _gameStartTime = DateTime.Now;
        _movingRight = true;
        _lastEnemyMove = DateTime.Now;
        _lastEnemyShoot = DateTime.Now;

        // Clear render states
        _currentRenderState.Clear();
        _previousRenderState.Clear();

        // Reinitialize game objects
        InitializeGame();
    }

    private bool ShowWinScreen()
    {
        System.Console.Clear();
        System.Console.ForegroundColor = ConsoleColor.Green;

        int windowWidth = System.Console.WindowWidth;
        int windowHeight = System.Console.WindowHeight;

        // Display victory message centered
        string[] messages = {
            "🎉 VICTORY! 🎉",
            "All enemies destroyed!",
            $"Final Score: {_score}",
            $"Time: {(int)(DateTime.Now - _gameStartTime).TotalSeconds} seconds",
            "",
            "Press R to Restart or any other key to exit..."
        };

        int startY = (windowHeight - messages.Length) / 2;

        for (int i = 0; i < messages.Length; i++)
        {
            int x = (windowWidth - messages[i].Length) / 2;
            System.Console.SetCursorPosition(x, startY + i);
            System.Console.WriteLine(messages[i]);
        }

        System.Console.ResetColor();
        ConsoleKeyInfo keyInfo = System.Console.ReadKey();
        return keyInfo.Key == ConsoleKey.R;
    }

    private bool ShowGameOverScreen()
    {
        System.Console.Clear();
        System.Console.ForegroundColor = ConsoleColor.Red;

        int windowWidth = System.Console.WindowWidth;
        int windowHeight = System.Console.WindowHeight;

        // Display game over message centered
        string[] messages = {
            "💥 GAME OVER 💥",
            "You were hit by an enemy bullet!",
            $"Final Score: {_score}",
            $"Time Survived: {(int)(DateTime.Now - _gameStartTime).TotalSeconds} seconds",
            "",
            "Press R to Restart or any other key to exit..."
        };

        int startY = (windowHeight - messages.Length) / 2;

        for (int i = 0; i < messages.Length; i++)
        {
            int x = (windowWidth - messages[i].Length) / 2;
            System.Console.SetCursorPosition(x, startY + i);
            System.Console.WriteLine(messages[i]);
        }

        System.Console.ResetColor();
        ConsoleKeyInfo keyInfo = System.Console.ReadKey();
        return keyInfo.Key == ConsoleKey.R;
    }
}
