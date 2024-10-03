using Microsoft.AspNetCore.Mvc;
using System.Runtime.Versioning;
using System.Text;

namespace SynthNetVoice.Controllers.v1
{
    /// <summary>
    /// Manages player actions.
    /// </summary>
    [Route("player")]
    [SupportedOSPlatform("windows")]
    public class PlayerController : BaseController
    {
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="config"></param>
        public PlayerController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
        }

        /// <summary>
        /// Prompt for player question, option to transcribe and use AI or not
        /// </summary>
        /// <param name="question"></param>
        /// <param name="scribe"></param>
        /// <param name="gpt"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("prompt")]
        public async Task<string> Prompt(string question = "", bool scribe = true, bool gpt = false)
        {
            var task = new TaskFactory().StartNew(() =>
            {
                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine($"{nameof(Prompt)}: {question}");
                if (string.IsNullOrEmpty(question))
                {
                    LocalPrompt.AppendText($"I have nothing to say!");
                }
                else
                {
                    LocalPrompt.AppendText(question);
                }
                LocalSynthesizer.Speak(LocalPrompt);
                if (scribe)
                {
                    promptBuilder.AppendLine($"{LocalSynthesizer}");
                    LocalLogger.Log(LogLevel.Information, nameof(Prompt), promptBuilder.ToString());
                }
                return promptBuilder.ToString();
            });
            var result = await task;
            return result;
        }

    }

}
