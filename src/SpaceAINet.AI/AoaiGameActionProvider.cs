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
            var gameState = ExtractGameStateFromFrame(frame2);

            // Create the prompt for AI analysis
            var prompt = CreateGameAnalysisPrompt(gameState, lastAction);

            // Call the AI service
            var messages = new ChatMessage[]
            {
                new SystemChatMessage("You are a MOBILE WARRIOR in Space Invaders! Hunt enemies by moving constantly. Never stay still - move, shoot, move, shoot! Chase your targets!"),
                new UserChatMessage(prompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages);

            // Parse the response
            return ParseAIResponse(response.Value.Content[0].Text ?? "");
        }
        catch (Exception ex)
        {
            // Return a safe default action in case of error
            return new GameActionResult
            {
                Action = GameAction.Wait,
                Reasoning = $"Error occurred: {ex.Message}",
                Confidence = 0.1f
            };
        }
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

    private string CreateGameAnalysisPrompt(string gameState, string lastAction)
    {
        return $@"You are a HUNTER in Space Invaders! SEEK AND DESTROY!

GAME STATE: {gameState}
LAST ACTION: {lastAction}

YOUR MISSION: Hunt down enemies by moving and shooting!

ELEMENTS:
- 'A' = You (the hunter)
- '><', 'oo', '/O\' = Enemy targets to hunt
- 'v' = Enemy bullets (dodge these!)
- '^' = Your bullets

HUNTING STRATEGY:
1. MOVE toward enemies to get better shots
2. SHOOT while moving for maximum damage  
3. CHANGE POSITIONS frequently - don't stay still!
4. Only dodge 'v' bullets when they're directly above you
5. After dodging, immediately MOVE to a new attack position

TACTICAL PRIORITIES:
- If enemies are on the LEFT: Move LEFT and shoot
- If enemies are on the RIGHT: Move RIGHT and shoot  
- If enemies are spread out: Move to center and shoot
- If last action was Shoot: MOVE to new position
- If last action was Move: SHOOT from new position
- Never stay in the same spot - keep hunting!

RESPOND WITH JSON:
{{
    ""action"": ""MoveLeft"" | ""MoveRight"" | ""Shoot"",
    ""reasoning"": ""Hunting strategy explanation"",
    ""confidence"": 0.85
}}

BE A MOBILE WARRIOR - Move and shoot, don't camp!";
    }

    private GameActionResult ParseAIResponse(string response)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);

                // Clean up common JSON issues
                jsonString = CleanJsonString(jsonString);

                // Additional validation - ensure we have a valid JSON structure
                if (!jsonString.Contains("action") || !jsonString.Contains("reasoning"))
                {
                    Console.WriteLine("JSON missing required fields, using heuristics");
                    return AnalyzeWithHeuristics(response);
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };

                var result = JsonSerializer.Deserialize<JsonElement>(jsonString, options);

                var actionStr = result.GetProperty("action").GetString() ?? "Shoot";
                var reasoning = result.GetProperty("reasoning").GetString() ?? "AI reasoning unavailable";
                var confidence = result.TryGetProperty("confidence", out var confElement) ?
                    confElement.GetSingle() : 0.7f;

                // Validate action string
                var action = actionStr.Trim().ToLower() switch
                {
                    "moveleft" or "move left" or "left" => GameAction.MoveLeft,
                    "moveright" or "move right" or "right" => GameAction.MoveRight,
                    "shoot" or "fire" => GameAction.Shoot,
                    "wait" => GameAction.Wait,
                    _ => GameAction.Shoot // Default to aggressive action
                };

                return new GameActionResult
                {
                    Action = action,
                    Reasoning = reasoning,
                    Confidence = Math.Max(0.0f, Math.Min(1.0f, confidence))
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing AI response: {ex.Message}");
            Console.WriteLine($"Response content: {response.Substring(0, Math.Min(200, response.Length))}...");
        }

        // Fallback to simple heuristics if JSON parsing fails
        return AnalyzeWithHeuristics(response);
    }
    private string CleanJsonString(string jsonString)
    {
        // First, remove code block markers if present
        jsonString = jsonString.Replace("```json", "").Replace("```", "");

        // Remove common problematic characters and fix JSON issues
        jsonString = jsonString.Replace("'", "'"); // Replace smart quotes
        jsonString = jsonString.Replace("'", "'"); // Replace smart quotes
        jsonString = jsonString.Replace("\u201C", "\""); // Replace smart double quotes
        jsonString = jsonString.Replace("\u201D", "\""); // Replace smart double quotes

        // Remove box drawing characters and other unicode symbols
        var boxDrawingChars = new[] { '─', '│', '┌', '┐', '└', '┘', '├', '┤', '┬', '┴', '┼',
                                     '═', '║', '╔', '╗', '╚', '╝', '╠', '╣', '╦', '╩', '╬',
                                     '▀', '▄', '█', '░', '▒', '▓', '◄', '►', '▲', '▼' };

        foreach (var boxChar in boxDrawingChars)
        {
            jsonString = jsonString.Replace(boxChar, ' ');
        }

        // Pre-process problematic escape sequences in JSON strings
        // Handle common enemy patterns that cause JSON issues
        jsonString = jsonString.Replace("/O\\", "/O"); // Remove problematic backslash from enemy pattern
        jsonString = jsonString.Replace("'><'", "enemies"); // Simplify enemy pattern
        jsonString = jsonString.Replace("'/O\\'", "enemies"); // Simplify enemy pattern  
        jsonString = jsonString.Replace("'oo'", "enemies"); // Simplify enemy pattern

        // Clean up problematic characters more aggressively
        var cleanedChars = new List<char>();
        bool insideString = false;
        char prevChar = ' ';

        for (int i = 0; i < jsonString.Length; i++)
        {
            char c = jsonString[i];

            // Track if we're inside a JSON string
            if (c == '"' && prevChar != '\\')
            {
                insideString = !insideString;
                cleanedChars.Add(c);
            }
            // Keep basic JSON structure characters
            else if (!insideString && (c == '{' || c == '}' || c == '[' || c == ']' || c == ',' || c == ':'))
            {
                cleanedChars.Add(c);
            }
            // Inside strings, handle characters more carefully
            else if (insideString)
            {
                // Skip problematic backslash sequences
                if (c == '\\')
                {
                    // Look ahead to see what follows the backslash
                    if (i + 1 < jsonString.Length)
                    {
                        char nextChar = jsonString[i + 1];
                        // Only keep valid JSON escape sequences
                        if (nextChar == '"' || nextChar == '\\' || nextChar == '/' ||
                            nextChar == 'b' || nextChar == 'f' || nextChar == 'n' ||
                            nextChar == 'r' || nextChar == 't' || nextChar == 'u')
                        {
                            cleanedChars.Add(c);
                        }
                        else
                        {
                            // Skip invalid backslash, continue to next character
                            continue;
                        }
                    }
                    else
                    {
                        // Backslash at end of string, skip it
                        continue;
                    }
                }
                // Keep other printable characters
                else if (c >= 32 && c <= 126)
                {
                    cleanedChars.Add(c);
                }
                else if (c == ' ')
                {
                    cleanedChars.Add(c);
                }
            }
            // Outside strings, keep alphanumeric and basic whitespace
            else if (!insideString && (char.IsLetterOrDigit(c) || c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '.' || c == '-'))
            {
                cleanedChars.Add(c == '\n' || c == '\r' || c == '\t' ? ' ' : c);
            }

            prevChar = c;
        }

        var cleaned = new string(cleanedChars.ToArray()).Trim();

        // Remove multiple spaces
        while (cleaned.Contains("  "))
        {
            cleaned = cleaned.Replace("  ", " ");
        }

        return cleaned;
    }
    private GameActionResult AnalyzeWithHeuristics(string response)
    {
        // Mobile warrior heuristics - mix movement and shooting
        var lowerResponse = response.ToLower();

        // Check for movement commands first
        if (lowerResponse.Contains("left") || lowerResponse.Contains("move left"))
        {
            return new GameActionResult
            {
                Action = GameAction.MoveLeft,
                Reasoning = "Heuristic: Response suggests moving left",
                Confidence = 0.6f
            };
        }

        if (lowerResponse.Contains("right") || lowerResponse.Contains("move right"))
        {
            return new GameActionResult
            {
                Action = GameAction.MoveRight,
                Reasoning = "Heuristic: Response suggests moving right",
                Confidence = 0.6f
            };
        }

        if (lowerResponse.Contains("shoot") || lowerResponse.Contains("fire"))
        {
            return new GameActionResult
            {
                Action = GameAction.Shoot,
                Reasoning = "Heuristic: Response suggests shooting",
                Confidence = 0.7f
            };
        }

        // Default to alternating between movement and shooting for mobile gameplay
        var random = new Random();
        var actions = new[] { GameAction.MoveLeft, GameAction.MoveRight, GameAction.Shoot, GameAction.Shoot };
        var selectedAction = actions[random.Next(actions.Length)];

        return new GameActionResult
        {
            Action = selectedAction,
            Reasoning = $"Heuristic: Mobile warrior default - {selectedAction}",
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
