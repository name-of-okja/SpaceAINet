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
                new SystemChatMessage("You are an expert Space Invaders player. Your goal: survive and win. Prioritize dodging bullets over shooting. Respond with precise JSON only."),
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
        return $@"You are playing Space Invaders. You must survive and destroy all enemies.

GAME LAYOUT:
- Player 'A': You (bottom of screen)
- Enemies: '><', 'oo', '/O\' patterns (top area)
- Player bullets: '^' (moving up)
- Enemy bullets: 'v' (moving down toward you)
- Borders: Box-drawing characters

CRITICAL PRIORITIES (in order):
1. DODGE enemy bullets 'v' immediately - survival is #1 priority
2. SHOOT enemies when you have clear shots
3. POSITION yourself for optimal shooting angles
4. AVOID moving into bullet paths

CURRENT GAME STATE:
{gameState}

Last Action: {lastAction}

DECISION MAKING:
- If enemy bullet 'v' is above you: MOVE away immediately
- If no immediate threats: SHOOT at enemies
- If enemies are moving toward your position: REPOSITION
- If you just shot: MOVE to avoid return fire

Respond ONLY with valid JSON:
{{
    ""action"": ""MoveLeft"" | ""MoveRight"" | ""Shoot"" | ""Wait"",
    ""reasoning"": ""1-2 sentence tactical explanation"",
    ""confidence"": 0.75
}}

THINK: What's the immediate threat? What's the best counter-action?";
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
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<JsonElement>(jsonString, options);

                var actionStr = result.GetProperty("action").GetString() ?? "Wait";
                var reasoning = result.GetProperty("reasoning").GetString() ?? "No reasoning provided";
                var confidence = result.TryGetProperty("confidence", out var confElement) ?
                    confElement.GetSingle() : 0.5f;

                var action = actionStr switch
                {
                    "MoveLeft" => GameAction.MoveLeft,
                    "MoveRight" => GameAction.MoveRight,
                    "Shoot" => GameAction.Shoot,
                    "Wait" => GameAction.Wait,
                    _ => GameAction.Wait
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
        }

        // Fallback to simple heuristics if JSON parsing fails
        return AnalyzeWithHeuristics(response);
    }

    private GameActionResult AnalyzeWithHeuristics(string response)
    {
        // Simple keyword-based analysis as fallback
        var lowerResponse = response.ToLower();

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

        return new GameActionResult
        {
            Action = GameAction.Wait,
            Reasoning = "Heuristic: Default to waiting",
            Confidence = 0.3f
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
