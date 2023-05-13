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
        public readonly APIAuthentication LocalAPIAuthentication;

        public BaseController(ILogger<PlayerController> logger, IConfiguration config)
        {
            LocalConfiguration = config;
            LocalLogger = logger;

            LocalAPIAuthentication = new APIAuthentication(LocalConfiguration.GetValue<string>("OPENAI_API_KEY"), LocalConfiguration.GetValue<string>("OPENAI_ORGANIZATION"));
            LocalPrompt = new PromptBuilder();
            LocalSynthesizer = new SpeechSynthesizer();
            LocalOpenAIAPI = new OpenAIAPI(LocalAPIAuthentication);

#if DEBUG

            LocalLogger.LogDebug($"{nameof(Created)}", nameof(PlayerController));
            Debug.Write($"{nameof(LocalAPIAuthentication.OpenAIOrganization)}", LocalAPIAuthentication.OpenAIOrganization);
            Debug.Write($"{nameof(LocalAPIAuthentication.ApiKey)}", LocalAPIAuthentication.ApiKey);

#endif

        }

       

    }
}
