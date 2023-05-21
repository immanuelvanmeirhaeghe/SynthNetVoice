using System.Speech.Recognition; 
using Microsoft.AspNetCore.Mvc;
using OpenAI_API;
using SynthNetVoice.Controllers.v1;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Speech.Synthesis;

namespace SynthNetVoice.Controllers
{
    [Route("api")]
    [ApiController]
    [SupportedOSPlatform("windows")]
    public class BaseController : ControllerBase
    {
        public readonly IConfiguration LocalConfiguration;
        public readonly ILogger<PlayerController> LocalLogger;

        public readonly OpenAIAPI LocalOpenAIAPI;
        public readonly PromptBuilder LocalPrompt;
        public readonly SpeechSynthesizer LocalSynthesizer;
        public readonly SpeechRecognizer LocalRecognizer;
        public readonly APIAuthentication LocalAPIAuthentication;

        public string AudioToSpeak { get; set; }
        public string LogConversationFile { get; set; }
        public string SelectedVoiceId { get; set; }
        public bool IsCompleted { get; set; }

        public BaseController(ILogger<PlayerController> logger, IConfiguration config)
        {
            LocalConfiguration = config;
            LocalLogger = logger;
            AudioToSpeak = string.Empty;
            SelectedVoiceId = string.Empty;
            LogConversationFile = string.Empty;
            LocalAPIAuthentication = new APIAuthentication(LocalConfiguration.GetValue<string>("OPENAI_API_KEY"), LocalConfiguration.GetValue<string>("OPENAI_ORGANIZATION"));
            LocalPrompt = new PromptBuilder();
            LocalSynthesizer = new SpeechSynthesizer();
            IsCompleted = false;
            LocalRecognizer ??= new SpeechRecognizer();
            LocalOpenAIAPI = new OpenAIAPI(LocalAPIAuthentication);
        }

        /// <summary>
        /// Log conversation text with an NPC to a given local file.
        /// </summary>
        /// <param name="fileName">given local file</param>
        /// <param name="text">conversation text logged</param>
        /// <returns>Log conversation</returns>
        [Route("log")]
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi =true)]
        public string LogConversation(string fileName, string text)
        {
            LogConversationFile = Path.Combine("D:\\Workspaces\\VSTS\\SynthNetVoice.Data\\Fallout4Data\\Logs\\", $"{fileName}_{DateTime.Now:hhmmss}.txt");
            System.IO.File.WriteAllText(LogConversationFile, text);
            return LogConversationFile;
        }

    }
}
