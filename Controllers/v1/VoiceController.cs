using Microsoft.AspNetCore.Mvc;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Numerics;
using System.Text;
using OpenAI_API.Moderation;
using System;
using System.Net;

namespace SynthNetVoice.Controllers.v1
{
    /// <summary>
    /// Manages <see cref="VoiceInfo"/> resources.
    /// </summary>
    [Route("voice")]
    [SupportedOSPlatform("windows")]
    public class VoiceController : BaseController
    {
        public StringBuilder RecognitionResultBuilder { get; set; }

        public VoiceController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
            RecognitionResultBuilder = new StringBuilder();    
            LocalRecognizer.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(SpeechRecognizedHandler);
            LocalRecognizer.SpeechDetected += new EventHandler<SpeechDetectedEventArgs>(SpeechDetectedHandler);
            LocalRecognizer.RecognizerUpdateReached += new EventHandler<RecognizerUpdateReachedEventArgs>(RecognizerUpdateReached);
            if (LocalRecognizer.State != RecognizerState.Listening)
            {
                LocalRecognizer.EmulateRecognizeAsync("Start listening");
            }
            IsCompleted = false;
            LocalRecognizer.RequestRecognizerUpdate();
        }

        /// <summary>
        /// Get a list of installed voices.
        /// </summary>
        [HttpGet]
        [Route("synthesizer/list")]
        public async Task<List<InstalledVoice>> InstalledVoicesAsync()
        {
            var task = new TaskFactory().StartNew(() =>
            {
                return LocalSynthesizer.GetInstalledVoices().ToList();
            });
            var list = await task;
            return list;
        }

        /// <summary>
        /// Set the voice to use wiith given id, which can be (part of) an installed voice's display name.
        /// </summary>
        /// <param name="id"><see cref="VoiceInfo.Id"/> as a search string</param>        
        [HttpPost]
        [Route("synthesizer/info")]
        public async Task<IActionResult?> SetVoiceAsync(
            [FromQuery]string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(id);
            }
            string validated = id.Trim().ToLower();
            var task = new TaskFactory().StartNew(() =>
            {
                 LocalSynthesizer.SelectVoice(id);
                SelectedVoiceId = id;
                return true;
            });
            
            bool ok = await task;
            if (ok)
            {
                return Ok(id);
            } else
            {
                return Problem(id);
            }
        }

        /// <summary>
        /// Get voice info for given id, which can be (part of) an installed voice's display name.
        /// </summary>
        /// <param name="id"><see cref="VoiceInfo.Id"/> as a search string</param>
        /// <returns><see cref="VoiceInfo"/></returns>
        [HttpGet]
        [Route("synthesizer/info")]
        public async Task<InstalledVoice?> GetVoiceAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return default;
            }
            string validated = id.Trim().ToLower();
            var task = new TaskFactory().StartNew(() =>
            {
                return LocalSynthesizer.GetInstalledVoices().ToList();
            });
            var result = await task;
            var voice = result.Find(iv => iv.VoiceInfo.Id.ToLower().Contains(validated));
            return voice;
        }

        /// <summary>
        /// Get currently active voice recognizer info.
        /// </summary>
        /// <param name="profile"></param>
        /// <returns><see cref="RecognizerInfo"/></returns>
        [HttpGet]
        [Route("recognizer/info")]
        public async Task<RecognizerInfo?> GetRecognizerAsync()
        {
            var task = new TaskFactory().StartNew(() =>
            {
                return LocalRecognizer.RecognizerInfo;
            });
            var result = await task;
            return result;
        }

        /// <summary>
        /// <para>
        /// Create a grammar to use with the recognizer
        /// from a file that contains a description of a grammar in a supported format.
        /// </para>
        /// <para>Supported formats include the following:
        /// <list type="bullet">
        /// <item>XML-format files that conform to the W3C Speech Recognition Grammar Specification(SRGS) Version 1.0</item>
        /// <item>Grammars that have been compiled to a binary file with a.cfg file extension</item>
        /// </list>        
        /// </para>
        /// </summary>
        /// <param name="filePath">File that contains a description of a grammar in a supported format</param>
        /// <param name="name">Name to set for this grammar file.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("recognizer/grammar/create")]
        public async Task<Grammar?> CreateGrammarFromFileAsync(string filePath, string name)
        {
            var task = new TaskFactory().StartNew(() =>
            {
                Grammar grammar = new Grammar(filePath)
                {
                    Name = name
                };
                LocalRecognizer.LoadGrammar(grammar);
                return grammar;               
            });
            var result = await task;
            return result;
        }

        /// <summary>
        /// Handle the RecognizerUpdateReached event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecognizerUpdateReached(
            object? sender,
            RecognizerUpdateReachedEventArgs? e)
        {
            // At the update, get the names and enabled status of the currently loaded grammars.  
            RecognitionResultBuilder.AppendLine("Update reached:");           
            string qualifier;
            List<Grammar> grammars = new List<Grammar>(LocalRecognizer.Grammars);
            foreach (Grammar g in grammars)
            {
                qualifier = (g.Enabled) ? "enabled" : "disabled";
                RecognitionResultBuilder.AppendLine($"Grammar {g.Name} is loaded and is {qualifier}.");
                LogConversation(nameof(RecognizerUpdateReached), RecognitionResultBuilder.ToString());
            }
        }

        /// <summary>
        /// Handle the SpeechRecognized event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SpeechRecognizedHandler(
          object? sender, 
          SpeechRecognizedEventArgs e)
        {
            if (e.Result != null)
            {
                RecognitionResultBuilder.AppendLine($"Recognition result = {(e.Result.Text ?? "<no text>")}");
                SemanticValue semantics = e.Result.Semantics;
                RecognitionResult result = e.Result;
                if (semantics == null || (semantics != null && semantics.Any(sm => sm.Value == null)))
                {
                    SpeechUI.SendTextFeedback(e.Result, "Nothing provided", false);
                }
                else
                {
                    SpeakAudioFile(e, result);
                }
            }
            else
            {
                RecognitionResultBuilder.AppendLine("No recognition result");
            }
            LogConversation(nameof(SpeechRecognizedHandler), RecognitionResultBuilder.ToString());
        }

        /// <summary>
        /// Handle the SpeechRecognizeCompleted event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SpeechDetectedHandler(
          object? sender,
          SpeechDetectedEventArgs e)
        {
            if (e == null)
            {
                RecognitionResultBuilder.AppendLine("No result generated.");
                IsCompleted = false;
            }
            else
            {
                IsCompleted = true;
            }
            RecognitionResultBuilder.AppendLine($"{nameof(IsCompleted)}: {IsCompleted}, {nameof(Result)}: {RecognitionResultBuilder}");
            LogConversation(nameof(SpeechDetectedHandler), RecognitionResultBuilder.ToString());
        }

        private void SpeakAudioFile(
             SpeechRecognizedEventArgs e,
             RecognitionResult result)
        {
            RecognizedAudio audioFile = result.GetAudioForWordRange(result.Words[0], result.Words[^1]);
            MemoryStream audioStream = new MemoryStream();
            audioFile.WriteToAudioStream(audioStream);

            if (audioStream != null)
            {
                AudioToSpeak = CreateAudioFile(audioFile);
                SpeechUI.SendTextFeedback(e.Result, $"Audio file created:\t{AudioToSpeak}", false);
            }
        }

        private string CreateAudioFile(
            RecognizedAudio audioFile)
        {
            AudioToSpeak = Path.Combine($"D:\\Workspaces\\VSTS\\SynthNetVoice.Data\\Fallout4Data\\Audio\\", $"{nameof(AudioToSpeak)}{(new Random()).Next()}.wav");
            FileStream waveStream = new FileStream(AudioToSpeak, FileMode.Create);
            audioFile.WriteToWaveStream(waveStream);
            waveStream.Flush();
            waveStream.Close();
            return AudioToSpeak;
        }

    }
}
