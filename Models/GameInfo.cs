namespace SynthNetVoice.Models
{
    /// <summary>
    /// The game or other application that calls the Voice API.
    /// </summary>
    public class GameInfo
    {
        public object? RequestId { get; set; }

        public string? Prompt { get; set; }

        public GameInfo() { }

    }
}