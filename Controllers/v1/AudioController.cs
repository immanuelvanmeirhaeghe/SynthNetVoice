using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SynthNetVoice.Data.Models;
using System.Runtime.Versioning;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;

namespace SynthNetVoice.Controllers.v1
{
    [Route("audio")]
    [ApiController]
    [SupportedOSPlatform("windows")]
    public class AudioController : BaseController
    {
        public AudioController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
        }

        /// <summary>
        /// Transcribe audio into whatever language the audio is in.
        /// </summary>
        /// <param name="audioFileInfo"></param>
        /// <param name="model"></param>
        /// <param name="response_format"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("transcriptions")]
        public async Task<Transcription> Transcriptions(
            [FromBody] AudioFileInfo audioFileInfo
            )
        {
            var promptBuilder = new StringBuilder();
            string responseToPrompt = string.Empty;

            var task = new TaskFactory().StartNew(() =>
            {
                if (audioFileInfo == null)
                {
                    promptBuilder.AppendLine($"I have nothing to say!");
                }
                else
                {
                    LocalPrompt.AppendAudio(audioFileInfo.FilePath);
                }
                LocalPrompt.AppendText(AudioToSpeak);
                LocalSynthesizer.Speak(LocalPrompt);

                return AudioToSpeak;

            });

            responseToPrompt = await task;
            //var result = await task;
            //responseToPrompt = await result;
            return new Transcription { Text= responseToPrompt };
        }

    }
}
