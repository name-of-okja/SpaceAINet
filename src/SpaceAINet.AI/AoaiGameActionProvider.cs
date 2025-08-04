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
        // Load configuration from user secrets
        var builder = new ConfigurationBuilder()
            .AddUserSecrets<AoaiGameActionProvider>();

        _configuration = builder.Build();
        // Initialize the chat client with Azure OpenAI
        var endpoint = new Uri(_configuration["AZURE_OPENAI_ENDPOINT"]);
        var apiKey = _configuration["AZURE_OPENAI_APIKEY"];
        var deploymentName = _configuration["AZURE_OPENAI_MODEL"];

        AzureOpenAIClient azureClient = new(
                        endpoint,
                        new AzureKeyCredential(apiKey));
        _chatClient = azureClient.GetChatClient(deploymentName);
    }

    public async Task<GameActionResult> AnalyzeFrameAsync(byte[] frame1, byte[] frame2, string lastAction)
    {
        try
        {
            _lastAction = lastAction;

            // Convert frames to base64 for analysis
            var frame1Base64 = Convert.ToBase64String(frame1);
            var frame2Base64 = Convert.ToBase64String(frame2);

            // Create the prompt for AI analysis
            var prompt = CreateGameAnalysisPrompt(frame1Base64, frame2Base64, lastAction);

            // Call the AI service
            var messages = new ChatMessage[]
            {
                new SystemChatMessage("You are an AI playing a Space Invaders-style game. Analyze the game state and respond with the best action."),
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

    private string CreateGameAnalysisPrompt(string frame1Base64, string frame2Base64, string lastAction)
    {
        return $@"
You are an AI playing a Space Invaders-style game called Space.AI.NET().

Game Rules:
- You control a player character 'A' at the bottom of the screen
- Enemies are represented by patterns like '><', 'oo', and '/O\'
- Player bullets are '^' and enemy bullets are 'v'
- You can move left, right, shoot, or wait
- Goal: Destroy all enemies while avoiding enemy bullets

Current game state:
- Previous frame: {frame1Base64}
- Current frame: {frame2Base64}
- Last action taken: {lastAction}

Analyze the game state and determine the best next action.

Respond with a JSON object in this exact format:
{{
    ""action"": ""[MoveLeft|MoveRight|Shoot|Wait]"",
    ""reasoning"": ""Brief explanation of why this action was chosen"",
    ""confidence"": 0.85
}}

Consider:
1. Enemy positions and movement patterns
2. Incoming enemy bullets to avoid
3. Optimal shooting opportunities
4. Player position relative to threats
5. Previous action effectiveness

Choose the action that maximizes survival and enemy destruction.";
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
}
