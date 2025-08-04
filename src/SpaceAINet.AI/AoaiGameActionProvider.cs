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
                new SystemChatMessage("You are a Space Invaders expert! CRITICAL: Always move TOWARD enemies, never away from them! Analyze the game state carefully and chase the enemies to destroy them!"),
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
        return $@"You are a SPACE INVADERS EXPERT! Your ONLY mission: DESTROY ALL ENEMIES!

CRITICAL RULES:
1. ANALYZE WHERE ENEMIES ARE located in the game state
2. MOVE TOWARD enemies to get closer to them
3. SHOOT constantly while moving toward enemies
4. NEVER move away from enemies - always chase them!

GAME STATE ANALYSIS:
{gameState}
Last action: {lastAction}

GAME ELEMENTS:
- 'A' = YOUR SHIP (this is you!)
- '><' = ENEMY SHIP (destroy this!)
- 'oo' = ENEMY SHIP (destroy this!)  
- '/O\\' = ENEMY SHIP (destroy this!)
- 'v' = Enemy bullet (ignore these)
- '^' = Your bullet

MOVEMENT STRATEGY:
- Look at the game state above
- Find where the enemy ships ('><', 'oo', '/O\\') are positioned
- If enemies appear to the LEFT of your ship 'A': Choose ""MoveLeft""
- If enemies appear to the RIGHT of your ship 'A': Choose ""MoveRight""  
- If enemies are directly above or below: Choose ""Shoot""
- ALWAYS move TOWARD the enemies, NEVER away from them!

DECISION PROCESS:
1. Locate your ship 'A' in the game state
2. Locate enemy ships ('><', 'oo', '/O\\') in the game state  
3. Determine which direction has MORE enemies
4. Move in that direction to get closer to enemies
5. Shoot while moving toward them

EXAMPLES:
- If game state shows enemies on left side: ""MoveLeft"" to chase them
- If game state shows enemies on right side: ""MoveRight"" to chase them
- Always explain which enemies you're chasing and why

Be aggressive! Chase the enemies! Never retreat!

Output format: JSON with ""action"" and ""reasoning"" fields.
Actions: MoveLeft, MoveRight, Shoot";
    }

    private GameActionResult ParseAIResponse(string response)
    {
        try
        {
            // Clean the LLM response to extract JSON
            var cleanedJson = CleanJsonString(response);

            if (string.IsNullOrWhiteSpace(cleanedJson))
            {
                Console.WriteLine("No valid JSON found in response");
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
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON parsing error: {ex.Message}");
            Console.WriteLine($"Response content: {response.Substring(0, Math.Min(200, response.Length))}...");
            return AnalyzeWithHeuristics(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General parsing error: {ex.Message}");
            return AnalyzeWithHeuristics(response);
        }
    }

    private GameActionResult ExtractActionFromText(string text)
    {
        // Try to extract action directly from text when JSON parsing fails
        var lowerText = text.ToLower();

        if (lowerText.Contains("moveleft") || lowerText.Contains("move left"))
        {
            return new GameActionResult
            {
                Action = GameAction.MoveLeft,
                Reasoning = "Extracted from text: move left",
                Confidence = 0.6f
            };
        }

        if (lowerText.Contains("moveright") || lowerText.Contains("move right"))
        {
            return new GameActionResult
            {
                Action = GameAction.MoveRight,
                Reasoning = "Extracted from text: move right",
                Confidence = 0.6f
            };
        }

        if (lowerText.Contains("shoot") || lowerText.Contains("fire"))
        {
            return new GameActionResult
            {
                Action = GameAction.Shoot,
                Reasoning = "Extracted from text: shoot",
                Confidence = 0.6f
            };
        }

        // Default mobile action
        return new GameActionResult
        {
            Action = GameAction.Shoot,
            Reasoning = "Fallback: aggressive shooting",
            Confidence = 0.5f
        };
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
        // Aggressive heuristics - prioritize movement toward enemies
        var lowerResponse = response.ToLower();

        // Look for movement keywords
        if (lowerResponse.Contains("left") || lowerResponse.Contains("moveleft") ||
            lowerResponse.Contains("chase") && lowerResponse.Contains("left"))
        {
            return new GameActionResult
            {
                Action = GameAction.MoveLeft,
                Reasoning = "Heuristic: Moving left to chase enemies",
                Confidence = 0.8f
            };
        }

        if (lowerResponse.Contains("right") || lowerResponse.Contains("moveright") ||
            lowerResponse.Contains("chase") && lowerResponse.Contains("right"))
        {
            return new GameActionResult
            {
                Action = GameAction.MoveRight,
                Reasoning = "Heuristic: Moving right to chase enemies",
                Confidence = 0.8f
            };
        }

        // Default to aggressive shooting
        return new GameActionResult
        {
            Action = GameAction.Shoot,
            Reasoning = "Heuristic: Aggressive shooting at enemies",
            Confidence = 0.7f
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
