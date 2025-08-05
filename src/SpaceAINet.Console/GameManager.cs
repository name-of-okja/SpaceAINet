namespace SpaceAINet.Console;

using SpaceAINet.AI;

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

    // AI Mode fields
    private bool _aiMode = false;
    private AoaiGameActionProvider? _aiProvider;
    private byte[]? _previousFrame;
    private string _lastAiAction = "None";
    private DateTime _lastAiActionTime = DateTime.Now;
    private int _aiCallCount = 0;

    // Manual input fields for smooth movement
    private DateTime _lastKeyPressTime = DateTime.MinValue;
    private ConsoleKey _lastPressedKey = ConsoleKey.NoName;

    public GameManager(int gameSpeed = 1, bool aiMode = false)
    {
        _gameSpeed = gameSpeed;
        _gameStartTime = DateTime.Now;
        _aiMode = aiMode;

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

        // Initialize AI provider if in AI mode
        if (_aiMode)
        {
            try
            {
                _aiProvider = new AoaiGameActionProvider();
                System.Console.Title = "Space.AI.NET() - AI Mode Initializing...";

                // Test the connection
                System.Console.WriteLine("Testing Azure OpenAI connection...");
                var connectionTest = _aiProvider.TestConnectionAsync().GetAwaiter().GetResult();

                if (connectionTest)
                {
                    System.Console.WriteLine("✓ Azure OpenAI connection successful!");
                    System.Console.Title = "Space.AI.NET() - AI Mode Ready";
                }
                else
                {
                    System.Console.WriteLine("✗ Azure OpenAI connection failed - using fallback heuristics");
                    System.Console.Title = "Space.AI.NET() - AI Mode (Fallback)";
                }

                System.Console.WriteLine("Press any key to start the game...");
                System.Console.ReadKey();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to initialize AI provider: {ex.Message}");
                System.Console.WriteLine("Falling back to manual mode. Press any key to continue...");
                System.Console.ReadKey();
                _aiMode = false;
            }
        }

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

            // Game speed control - more dramatic differences
            int delay = _gameSpeed switch
            {
                1 => 150, // Slow - much slower
                2 => 75,  // Medium - normal
                3 => 25,  // Fast - much faster
                _ => 100
            };

            Thread.Sleep(delay);
        }
    }

    private void HandleInput()
    {
        if (_aiMode)
        {
            HandleAIInput();
        }
        else
        {
            HandleManualInput();
        }
    }

    private async void HandleAIInput()
    {
        if (_aiProvider == null) return;

        // Only process AI input at regular intervals to avoid overwhelming the API
        if (DateTime.Now - _lastAiActionTime < TimeSpan.FromMilliseconds(500))
            return;

        try
        {
            // Capture current frame
            var currentFrame = CaptureFrameAsBytes();

            if (_previousFrame != null)
            {
                // Get AI decision
                var actionResult = await _aiProvider.AnalyzeFrameAsync(_previousFrame, currentFrame, _lastAiAction);

                // Increment AI call counter
                _aiCallCount++;

                // Execute the AI action
                ExecuteAIAction(actionResult);

                _lastAiAction = actionResult.Action.ToString();
                _lastAiActionTime = DateTime.Now;

                // Display AI reasoning in console title
                System.Console.Title = $"Space.AI.NET() - AI: {actionResult.Action} ({actionResult.Confidence:P0}) - {actionResult.Reasoning}";
            }

            _previousFrame = currentFrame;
        }
        catch (Exception ex)
        {
            // Log error to a more visible place and continue the game
            var errorMsg = $"AI Error: {ex.Message}";
            System.Console.Title = errorMsg;

            // Also try to log to console for debugging
            try
            {
                var originalPos = System.Console.GetCursorPosition();
                System.Console.SetCursorPosition(0, 0);
                System.Console.WriteLine($"DEBUG: {errorMsg}");
                System.Console.SetCursorPosition(originalPos.Left, originalPos.Top);
            }
            catch { /* Ignore positioning errors */ }
        }
    }

    private void ExecuteAIAction(GameActionResult actionResult)
    {
        switch (actionResult.Action)
        {
            case GameAction.MoveLeft:
                _player.MoveLeft(_gameAreaLeft);
                break;
            case GameAction.MoveRight:
                _player.MoveRight(_gameAreaRight);
                break;
            case GameAction.Shoot:
                if (_player.CanShoot())
                {
                    _bullets.Add(new Bullet(_player.X, _player.Y - 1, true));
                    _player.Shoot();
                }
                break;
            case GameAction.Wait:
            case GameAction.None:
            default:
                // Do nothing
                break;
        }
    }

    private byte[] CaptureFrameAsBytes()
    {
        // Convert the current render state to a byte array representation
        var frame = new List<byte>();

        for (int y = 0; y < _currentRenderState.Height; y++)
        {
            for (int x = 0; x < _currentRenderState.Width; x++)
            {
                var character = _currentRenderState.GetChar(x, y);
                var color = _currentRenderState.GetColor(x, y);

                // Simple encoding: character byte + color byte
                frame.Add((byte)character);
                frame.Add((byte)color);
            }
        }

        return frame.ToArray();
    }

    private void HandleManualInput()
    {
        if (System.Console.KeyAvailable)
        {
            ConsoleKeyInfo keyInfo = System.Console.ReadKey(true);
            DateTime now = DateTime.Now;

            // Input throttling for smooth movement
            var inputDelay = _gameSpeed switch
            {
                1 => TimeSpan.FromMilliseconds(120), // Slower input response for slow game
                2 => TimeSpan.FromMilliseconds(80),  // Normal input response
                3 => TimeSpan.FromMilliseconds(50),  // Faster input response for fast game
                _ => TimeSpan.FromMilliseconds(80)
            };

            // Check if enough time has passed since last key press for movement keys
            bool canProcessMovement = now - _lastKeyPressTime >= inputDelay;
            bool isMovementKey = keyInfo.Key == ConsoleKey.LeftArrow || keyInfo.Key == ConsoleKey.RightArrow;

            // Always allow non-movement keys (shoot, screenshot, quit) without delay
            // For movement keys, check the delay
            if (!isMovementKey || canProcessMovement)
            {
                switch (keyInfo.Key)
                {
                    case ConsoleKey.LeftArrow:
                        _player.MoveLeft(_gameAreaLeft);
                        _lastKeyPressTime = now;
                        _lastPressedKey = keyInfo.Key;
                        break;

                    case ConsoleKey.RightArrow:
                        _player.MoveRight(_gameAreaRight);
                        _lastKeyPressTime = now;
                        _lastPressedKey = keyInfo.Key;
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

        // Enemy move delay adjusted by game speed
        var enemyMoveDelay = _gameSpeed switch
        {
            1 => TimeSpan.FromMilliseconds(800), // Slow - enemies move slower
            2 => TimeSpan.FromMilliseconds(500), // Medium - normal speed
            3 => TimeSpan.FromMilliseconds(250), // Fast - enemies move faster
            _ => TimeSpan.FromMilliseconds(500)
        };

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

        // Enemy shoot delay adjusted by game speed
        var shootDelay = _gameSpeed switch
        {
            1 => TimeSpan.FromSeconds(3.0), // Slow - enemies shoot less frequently
            2 => TimeSpan.FromSeconds(2.0), // Medium - normal frequency
            3 => TimeSpan.FromSeconds(1.0), // Fast - enemies shoot more frequently
            _ => TimeSpan.FromSeconds(2.0)
        };

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

        // Add speed and mode information
        string speedText = _gameSpeed switch
        {
            1 => "Slow",
            2 => "Medium",
            3 => "Fast",
            _ => "Unknown"
        };

        string modeText = _aiMode ? $"AI (Calls: {_aiCallCount})" : "Manual";

        string ui = $"Score: {_score:D4}   Time: {elapsedSeconds:D2}s   Bullets: {_player.CurrentBullets}/{_player.MaxBullets}   Speed: {speedText}   Mode: {modeText}";

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
            "Press R to Restart or Q to Quit..."
        };

        int startY = (windowHeight - messages.Length) / 2;

        for (int i = 0; i < messages.Length; i++)
        {
            int x = (windowWidth - messages[i].Length) / 2;
            System.Console.SetCursorPosition(x, startY + i);
            System.Console.WriteLine(messages[i]);
        }

        System.Console.ResetColor();

        // Keep reading until R or Q is pressed
        ConsoleKeyInfo keyInfo;
        do
        {
            keyInfo = System.Console.ReadKey(true);
        } while (keyInfo.Key != ConsoleKey.R && keyInfo.Key != ConsoleKey.Q);

        if (keyInfo.Key == ConsoleKey.Q)
        {
            _gameRunning = false;
            return false; // Exit the game
        }

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
            "Press R to Restart or Q to Quit..."
        };

        int startY = (windowHeight - messages.Length) / 2;

        for (int i = 0; i < messages.Length; i++)
        {
            int x = (windowWidth - messages[i].Length) / 2;
            System.Console.SetCursorPosition(x, startY + i);
            System.Console.WriteLine(messages[i]);
        }

        System.Console.ResetColor();

        // Keep reading until R or Q is pressed
        ConsoleKeyInfo keyInfo;
        do
        {
            keyInfo = System.Console.ReadKey(true);
        } while (keyInfo.Key != ConsoleKey.R && keyInfo.Key != ConsoleKey.Q);

        if (keyInfo.Key == ConsoleKey.Q)
        {
            _gameRunning = false;
            return false; // Exit the game
        }

        return keyInfo.Key == ConsoleKey.R;
    }
}
