using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Runtime.Versioning;
using static System.Net.Mime.MediaTypeNames;
using System.Speech.AudioFormat;
using System.Text;
using OpenAI_API;
using System.Diagnostics;
using SynthNetVoice.Data.Instructions;

namespace SynthNetVoice.Controllers.v1
{
    [Route("player")]
    [SupportedOSPlatform("windows")]
    public class PlayerController : BaseController
    {
        public PlayerController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
        }

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
