using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Runtime.Versioning;
using static System.Net.Mime.MediaTypeNames;

namespace SynthNetVoice.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [SupportedOSPlatform("windows")]
    public class SpeechController : ControllerBase
    {
        private readonly PromptBuilder prompt;
        private readonly SpeechSynthesizer synthesizer;
        private readonly ILogger<SpeechController> _logger;

        public SpeechController(ILogger<SpeechController> logger)
        {
            _logger = logger;
            prompt = new PromptBuilder();
            synthesizer = new SpeechSynthesizer();
        }

        /// <summary>
        /// The following operation will have your synthezised voice speak out the given text prompt.
        /// </summary>
        /// <param name="text">text to speak</param>
        [HttpGet]
        [Route("api/prompt")]
        public void Prompt(string text)
        {          
             prompt.AppendText(text);
            synthesizer.Speak(prompt);
        }

    }

}
