namespace SpaceAINet.AI;

public class GameActionResult
{
    public GameAction Action { get; set; } = GameAction.None;
    public string Reasoning { get; set; } = string.Empty;
    public float Confidence { get; set; } = 0.0f;
}

public enum GameAction
{
    None,
    MoveLeft,
    MoveRight,
    Shoot,
    Wait
}
