using Microsoft.Extensions.Configuration;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;

namespace SpaceAINet.AI;

public class AoaiGameActionProvider
{
    private readonly ChatClient _chatClient;
    private readonly IConfiguration _configuration;
    private string _lastAction = "None";
    private bool _gameInitialized = false;

    public AoaiGameActionProvider()
    {
        try
        {
            // Load configuration from user secrets
            var builder = new ConfigurationBuilder()
                .AddUserSecrets<AoaiGameActionProvider>();

            _configuration = builder.Build();

            // Initialize the chat client with Azure OpenAI
            var endpoint = _configuration["AZURE_OPENAI_ENDPOINT"];
            var apiKey = _configuration["AZURE_OPENAI_APIKEY"];
            var deploymentName = _configuration["AZURE_OPENAI_MODEL"];

            // Validate configuration
            if (string.IsNullOrEmpty(endpoint))
                throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not configured in user secrets");
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("AZURE_OPENAI_APIKEY is not configured in user secrets");
            if (string.IsNullOrEmpty(deploymentName))
                throw new InvalidOperationException("AZURE_OPENAI_MODEL is not configured in user secrets");

            AzureOpenAIClient azureClient = new(
                            new Uri(endpoint),
                            new AzureKeyCredential(apiKey));
            _chatClient = azureClient.GetChatClient(deploymentName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize Azure OpenAI client: {ex.Message}", ex);
        }
    }

    public async Task<GameActionResult> AnalyzeFrameAsync(byte[] frame1, byte[] frame2, string lastAction)
    {
        try
        {
            _lastAction = lastAction;

            // Convert frame data to a more readable format for AI analysis
            var currentGameState = ExtractGameStateFromFrame(frame2);
            var previousGameState = frame1 != null ? ExtractGameStateFromFrame(frame1) : "";

            // Analyze changes between frames
            var stateChange = AnalyzeStateChange(previousGameState, currentGameState);

            // Create the prompt for AI analysis - different for first time vs ongoing
            var prompt = _gameInitialized ?
                CreateOngoingGamePrompt(currentGameState, lastAction, stateChange) :
                CreateInitialGamePrompt(currentGameState, lastAction);

            // Mark game as initialized after first call
            if (!_gameInitialized)
            {
                _gameInitialized = true;
            }

            // Call the AI service with appropriate system message
            var systemMessage = _gameInitialized ?
                "적을 추적하고 공격하세요. JSON으로만 응답: {\"action\": \"MoveLeft|MoveRight|Shoot\", \"reasoning\": \"간단한 이유\"}." :
                "적을 추적하고 공격하세요. JSON으로만 응답: {\"action\": \"MoveLeft|MoveRight|Shoot\", \"reasoning\": \"간단한 이유\"}.";

            var messages = new ChatMessage[]
            {
                new SystemChatMessage(systemMessage),
                new UserChatMessage(prompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages);

            var responseText = response.Value.Content[0].Text ?? "";

            // Parse the response
            return ParseAIResponse(responseText);
        }
        catch (Exception)
        {
            // 간단한 대체 동작
            var fallbackActions = new[] { GameAction.MoveLeft, GameAction.MoveRight, GameAction.Shoot };
            var randomAction = fallbackActions[DateTime.Now.Millisecond % fallbackActions.Length];

            return new GameActionResult
            {
                Action = randomAction,
                Reasoning = "대체 동작: 적 추적",
                Confidence = 0.7f
            };
        }
    }

    private string AnalyzeStateChange(string previousState, string currentState)
    {
        if (string.IsNullOrEmpty(previousState))
            return "Game started";

        var changes = new List<string>();

        // Analyze enemy movement
        var prevEnemyCount = CountEnemies(previousState);
        var currEnemyCount = CountEnemies(currentState);

        if (currEnemyCount < prevEnemyCount)
        {
            changes.Add($"Enemy destroyed! ({prevEnemyCount}→{currEnemyCount})");
        }
        else if (currEnemyCount > prevEnemyCount)
        {
            changes.Add("New enemies appeared!");
        }

        // Analyze bullet activity
        var prevBullets = CountBullets(previousState);
        var currBullets = CountBullets(currentState);

        if (currBullets.playerBullets > prevBullets.playerBullets)
        {
            changes.Add("You fired!");
        }

        if (currBullets.enemyBullets > prevBullets.enemyBullets)
        {
            changes.Add("Enemies fired!");
        }

        // Analyze player position change
        var prevPlayerPos = FindPlayerPosition(previousState);
        var currPlayerPos = FindPlayerPosition(currentState);

        if (prevPlayerPos != currPlayerPos)
        {
            if (currPlayerPos > prevPlayerPos)
                changes.Add("Moved right");
            else
                changes.Add("Moved left");
        }

        return changes.Count > 0 ? string.Join(", ", changes) : "No significant changes";
    }

    private int CountEnemies(string gameState)
    {
        return gameState.Count(c => c == '>' || c == '<' || c == 'o' || c == '/' || c == '\\' || c == 'O');
    }

    private (int playerBullets, int enemyBullets) CountBullets(string gameState)
    {
        var playerBullets = gameState.Count(c => c == '^');
        var enemyBullets = gameState.Count(c => c == 'v');
        return (playerBullets, enemyBullets);
    }

    private int FindPlayerPosition(string gameState)
    {
        return gameState.IndexOf('A');
    }

    private string ExtractGameStateFromFrame(byte[] frameData)
    {
        try
        {
            var gameState = new StringBuilder();

            // Extract meaningful game elements from the frame
            // Frame format: character byte + color byte pairs
            for (int i = 0; i < frameData.Length - 1; i += 2)
            {
                char character = (char)frameData[i];

                // Only include meaningful game characters
                if (character == 'A' || character == '^' || character == 'v' ||
                    character == '>' || character == '<' || character == 'o' ||
                    character == '/' || character == '\\' || character == 'O')
                {
                    gameState.Append(character);
                }
                else if (character == ' ' && gameState.Length > 0 && gameState[gameState.Length - 1] != ' ')
                {
                    gameState.Append(' ');
                }
            }

            return gameState.ToString().Trim();
        }
        catch
        {
            return "Unable to parse game state";
        }
    }

    private string CreateInitialGamePrompt(string gameState, string lastAction)
    {
        return $@"간단한 규칙:
1. 적 유닛 근처로 이동
2. 적 유닛 근처에서 공격

적이 왼쪽에 많으면 MoveLeft, 오른쪽에 많으면 MoveRight, 위치가 좋으면 Shoot

현재 상황: {gameState}
마지막 행동: {lastAction}

JSON: {{""action"": ""MoveLeft"", ""reasoning"": ""적 추적""}}";
    }

    private string CreateOngoingGamePrompt(string gameState, string lastAction, string stateChange)
    {
        return $@"규칙:
1. 적 유닛 근처로 이동
2. 적 유닛 근처에서 공격

적이 왼쪽에 많으면 MoveLeft, 오른쪽에 많으면 MoveRight, 위치가 좋으면 Shoot

현재: {gameState}
마지막 행동: {lastAction}
변화: {stateChange}

JSON: {{""action"": ""MoveLeft"", ""reasoning"": ""적 추적""}}";
    }

    private GameActionResult ParseAIResponse(string response)
    {
        try
        {
            // Clean the LLM response to extract JSON
            var cleanedJson = CleanJsonString(response);

            if (string.IsNullOrWhiteSpace(cleanedJson))
            {
                return AnalyzeWithHeuristics(response);
            }

            // Try to deserialize with more permissive settings
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var result = JsonSerializer.Deserialize<JsonElement>(cleanedJson, options);

            // Extract action and reasoning
            var actionStr = "Shoot"; // Default action
            var reasoning = "AI reasoning unavailable";

            if (result.TryGetProperty("action", out var actionElement))
            {
                actionStr = actionElement.GetString() ?? "Shoot";
            }
            else if (result.TryGetProperty("nextaction", out var nextActionElement))
            {
                actionStr = nextActionElement.GetString() ?? "Shoot";
            }

            if (result.TryGetProperty("reasoning", out var reasoningElement))
            {
                reasoning = reasoningElement.GetString() ?? "AI reasoning unavailable";
            }
            else if (result.TryGetProperty("explanation", out var explanationElement))
            {
                reasoning = explanationElement.GetString() ?? "AI reasoning unavailable";
            }

            // Convert action string to GameAction
            var action = actionStr.Trim().ToLower() switch
            {
                "moveleft" or "move_left" or "move left" or "left" => GameAction.MoveLeft,
                "moveright" or "move_right" or "move right" or "right" => GameAction.MoveRight,
                "shoot" or "fire" => GameAction.Shoot,
                "stop" or "wait" => GameAction.Wait,
                _ => GameAction.Shoot // Default to shooting
            };

            return new GameActionResult
            {
                Action = action,
                Reasoning = reasoning,
                Confidence = 0.8f
            };
        }
        catch (JsonException)
        {
            return AnalyzeWithHeuristics(response);
        }
        catch (Exception)
        {
            return AnalyzeWithHeuristics(response);
        }
    }

    private string CleanJsonString(string llmResponse)
    {
        if (string.IsNullOrWhiteSpace(llmResponse))
            return llmResponse;

        // Remove code block markers
        llmResponse = llmResponse.Replace("```json", "").Replace("```", "");

        // First, aggressively clean problematic game patterns that break JSON
        llmResponse = CleanGamePatternsFromResponse(llmResponse);

        var lines = llmResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int start = Array.FindIndex(lines, l => l.TrimStart().StartsWith("{"));
        int end = Array.FindLastIndex(lines, l => l.TrimEnd().EndsWith("}"));

        string json;
        if (start >= 0 && end >= start)
        {
            var jsonLines = lines[start..(end + 1)];
            json = string.Join("\n", jsonLines).Trim();
        }
        else
        {
            json = llmResponse.Trim();
        }

        // Additional cleaning for JSON content
        json = CleanJsonContent(json);

        // Find the last complete JSON brace
        int lastBrace = json.LastIndexOf('}');
        if (lastBrace >= 0 && lastBrace < json.Length - 1)
        {
            json = json.Substring(0, lastBrace + 1);
        }

        // Remove trailing characters after the last brace
        while (json.Length > 0 && json[^1] != '}')
        {
            json = json.Substring(0, json.Length - 1).TrimEnd();
        }

        // Handle cases like "}."
        if (json.EndsWith("}."))
        {
            json = json.Substring(0, json.Length - 1);
        }

        return json;
    }

    private string CleanGamePatternsFromResponse(string response)
    {
        // Replace problematic game patterns that cause JSON parsing issues
        var replacements = new Dictionary<string, string>
        {
            { "'/O\\'", "'enemy'" },
            { "\"/O\\\"", "\"enemy\"" },
            { "'><'", "'enemy'" },
            { "\"><\"", "\"enemy\"" },
            { "'oo'", "'enemy'" },
            { "\"oo\"", "\"enemy\"" },
            { "'/\\'", "'enemy'" },
            { "\"/\\\"", "\"enemy\"" },
            { "'\\O/'", "'enemy'" },
            { "\"\\O/\"", "\"enemy\"" },
            { "\\O\\", "enemy" },
            { "/O\\", "enemy" },
            { "\\", "" }, // Remove standalone backslashes
        };

        foreach (var replacement in replacements)
        {
            response = response.Replace(replacement.Key, replacement.Value);
        }

        return response;
    }

    private string CleanJsonContent(string json)
    {
        // Clean JSON string values to remove problematic characters
        var result = new StringBuilder();
        bool insideString = false;
        bool escapeNext = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];

            if (escapeNext)
            {
                // Skip this character if it's an invalid escape
                escapeNext = false;
                continue;
            }

            if (c == '"' && !escapeNext)
            {
                insideString = !insideString;
                result.Append(c);
            }
            else if (insideString && c == '\\')
            {
                // Check if this is a valid escape sequence
                if (i + 1 < json.Length)
                {
                    char nextChar = json[i + 1];
                    if (nextChar == '"' || nextChar == '\\' || nextChar == '/' ||
                        nextChar == 'b' || nextChar == 'f' || nextChar == 'n' ||
                        nextChar == 'r' || nextChar == 't' || nextChar == 'u')
                    {
                        result.Append(c);
                    }
                    else
                    {
                        // Invalid escape, skip it
                        escapeNext = true;
                    }
                }
                else
                {
                    // Backslash at end, skip it
                }
            }
            else if (insideString)
            {
                // Inside string, only allow safe characters
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '.' || c == '-' ||
                    c == '_' || c == ',' || c == '\'' || c == ':' || c == '!')
                {
                    result.Append(c);
                }
                // Skip other potentially problematic characters
            }
            else
            {
                // Outside string, keep JSON structure characters
                result.Append(c);
            }
        }

        return result.ToString();
    }

    private GameActionResult AnalyzeWithHeuristics(string response)
    {
        // 간단한 휴리스틱 전략
        var lowerResponse = response.ToLower();

        // 이동 키워드 검색
        if (lowerResponse.Contains("moveleft") || lowerResponse.Contains("move left") ||
            (lowerResponse.Contains("left") && lowerResponse.Contains("적")))
        {
            return new GameActionResult
            {
                Action = GameAction.MoveLeft,
                Reasoning = "왼쪽으로 적 추적",
                Confidence = 0.9f
            };
        }

        if (lowerResponse.Contains("moveright") || lowerResponse.Contains("move right") ||
            (lowerResponse.Contains("right") && lowerResponse.Contains("적")))
        {
            return new GameActionResult
            {
                Action = GameAction.MoveRight,
                Reasoning = "오른쪽으로 적 추적",
                Confidence = 0.9f
            };
        }

        // 기본적으로 공격
        return new GameActionResult
        {
            Action = GameAction.Shoot,
            Reasoning = "적 공격",
            Confidence = 0.6f
        };
    }

    /// <summary>
    /// Tests if the Azure OpenAI connection is working properly
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var messages = new ChatMessage[]
            {
                new SystemChatMessage("You are a test assistant."),
                new UserChatMessage("Respond with exactly: TEST_OK")
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            var content = response.Value.Content[0].Text ?? "";
            return content.Contains("TEST_OK");
        }
        catch
        {
            return false;
        }
    }
}
