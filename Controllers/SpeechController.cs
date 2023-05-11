using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Runtime.Versioning;
using static System.Net.Mime.MediaTypeNames;
using System.Speech.AudioFormat;
using System.Text;

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

        [HttpGet]
        [Route("api/voices/installed")]
        public string InstalledVoices()
        {
            // Output information about all of the installed voices.
            var infoBuilder = new StringBuilder();

            infoBuilder.AppendLine("Installed voices -");
            foreach (InstalledVoice voice in synthesizer.GetInstalledVoices())
            {
                VoiceInfo info = voice.VoiceInfo;
                string AudioFormats = "";
                foreach (SpeechAudioFormatInfo fmt in info.SupportedAudioFormats)
                {
                    AudioFormats += String.Format("{0}\n",
                    fmt.EncodingFormat.ToString());
                }

                infoBuilder.AppendLine(" Name:          " + info.Name);
                infoBuilder.AppendLine(" Culture:       " + info.Culture);
                infoBuilder.AppendLine(" Age:           " + info.Age);
                infoBuilder.AppendLine(" Gender:        " + info.Gender);
                infoBuilder.AppendLine(" Description:   " + info.Description);
                infoBuilder.AppendLine(" ID:            " + info.Id);
                infoBuilder.AppendLine(" Enabled:       " + voice.Enabled);
                if (info.SupportedAudioFormats.Count != 0)
                {
                    infoBuilder.AppendLine(" Audio formats: " + AudioFormats);
                }
                else
                {
                    infoBuilder.AppendLine(" No supported audio formats found");
                }

                string AdditionalInfo = "";
                foreach (string key in info.AdditionalInfo.Keys)
                {
                    AdditionalInfo += String.Format("  {0}: {1}\n", key, info.AdditionalInfo[key]);
                }

                infoBuilder.AppendLine(" Additional Info - " + AdditionalInfo);
                infoBuilder.AppendLine();
            }

            return infoBuilder.ToString();
        }


    }

}
