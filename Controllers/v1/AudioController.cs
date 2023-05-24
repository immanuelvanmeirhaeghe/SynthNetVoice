using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI_API.Moderation;
using SynthNetVoice.Data.Enums;
using SynthNetVoice.Data.Models;
using System.Media;
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
        [Route("transcriptions")]
        [HttpPost]
        public async Task<IActionResult> Transcriptions(
            [FromBody] AudioFileInfo audioFileInfo
            )
        {
            if (audioFileInfo == null || (audioFileInfo != null && string.IsNullOrEmpty(audioFileInfo.FilePath)))
            {
                return BadRequest(new { audioFileInfo });
            }

            var promptBuilder = new StringBuilder();
            string responseToPrompt = string.Empty;

            var task = new TaskFactory().StartNew(() =>
            {
                if (audioFileInfo != null && !string.IsNullOrEmpty(audioFileInfo.FilePath))
                {
                    LocalSynthesizer.SetOutputToWaveFile(audioFileInfo.FilePath);
                    LocalPrompt.AppendAudio(audioFileInfo.FilePath);

                    string[] fileParts = audioFileInfo.FilePath.Split("\\");
                    LocalAudioFile = fileParts[fileParts.Length - 1];
                    string[] localParts = LocalAudioFile.Split("_");
                    LocalNpcName = localParts[1];
                    LocalGameName = Enum.Parse<GameNames>(localParts[2]);

                    SpeechRecognitionEngine recognizer = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US"));
                    recognizer.SetInputToWaveFile(audioFileInfo.FilePath);
                    RecognitionResult result = recognizer.Recognize();
                    RecognizedAudio audioFile = result.GetAudioForWordRange(result.Words[0], result.Words[^1]);
                    MemoryStream audioStream = new MemoryStream();
                    audioFile.WriteToWaveStream(audioStream);
                    if (audioStream != null)
                    {
                        FileStream waveStream = new FileStream(LocalTextFromAudioFile, FileMode.Create);
                        audioFile.WriteToWaveStream(waveStream);
                        waveStream.Flush();
                        waveStream.Close();
                    }                    
                }               
                return LocalTextFromAudioFile;
            });

            responseToPrompt = await task;
            Transcription script = new Transcription
            {
                Text = LocalTextFromAudioFile
            };
            LocalTextFromAudioFile = LogConversation(script);
            return Ok(script);
        }

        /// <summary>
        /// Creates audio file from given text.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        [Route("create")]
        [HttpPost]    
        public async Task<IActionResult> SpeechToText(             
            [FromBody] Transcription script,
            [FromQuery] string gameName = "Fallout4",
            [FromQuery] string npcName = "MamaMurphy")
        {
            if (!ValidateDefaultParameters(gameName, npcName))
            {
                return BadRequest(new { gameName, npcName });
            }

            if (script == null || ( script != null && string.IsNullOrEmpty(script.Text) ) )
            {
                return BadRequest(script);
            }
            
            var task = new TaskFactory().StartNew(() =>
            {
                if (script != null && !string.IsNullOrEmpty(script.AudioFilePath) && !string.IsNullOrEmpty(script.AudioFileName))
                {
                    string path = Path.Combine(script.AudioFilePath, script.AudioFileName);
                    LocalSynthesizer.SetOutputToWaveFile(path);
                    LocalPrompt.AppendText(script.Text);
                }                  
            });

            await task;
           
            return Ok(script);
        }

    }

}
