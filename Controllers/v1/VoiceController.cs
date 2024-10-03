using Microsoft.AspNetCore.Mvc;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Text;
using SynthNetVoice.Data.Helpers;

namespace SynthNetVoice.Controllers.v1
{

    /// <summary>
    /// Manages <see cref="VoiceInfo"/> resources.
    /// </summary>
    [Route("voice")]
    [SupportedOSPlatform("windows")]
    public class VoiceController : BaseController
    {
        /// <summary>
        /// 
        /// </summary>
        public StringBuilder RecognitionResultBuilder { get; set; }

        /// <summary>
        /// VoiceController ctor
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="config"></param>
        public VoiceController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
            RecognitionResultBuilder = new StringBuilder();    
            LocalRecognizer.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(SpeechRecognizedHandler);
            LocalRecognizer.SpeechDetected += new EventHandler<SpeechDetectedEventArgs>(SpeechDetectedHandler);
            LocalRecognizer.RecognizerUpdateReached += new EventHandler<RecognizerUpdateReachedEventArgs>(RecognizerUpdateReached);
            if (LocalRecognizer.State != RecognizerState.Listening)
            {
                LocalRecognizer.EmulateRecognize("Start listening");
            }

            BaseControllerHelpers.IsCompleted = false;
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
        /// Set the voice to use with given voice info name.
        /// </summary>
        /// <param name="name"><see cref="VoiceInfo.Name"/> as a search string</param>        
        [HttpPost]
        [Route("synthesizer/info")]
        public async Task<IActionResult?> SetVoiceAsync(
            [FromQuery] string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest(name);
            }

            var voiceFound = await GetInstalledVoiceInfo(name);
            if (voiceFound != null)
            {
                LocalSynthesizer.SelectVoice(voiceFound.VoiceInfo.Name);
                BaseControllerHelpers.SelectedVoiceInfoName = voiceFound.VoiceInfo.Name;
            }

            if (!string.IsNullOrEmpty(BaseControllerHelpers.SelectedVoiceInfoName))
            {
               LocalSynthesizer.SpeakAsync($"Ok! You have selected me, {BaseControllerHelpers.SelectedVoiceInfoName} to be the active voice.");
                return Ok(name);
            } 
            else
            {
                LocalSynthesizer.SpeakAsync($"Oh no! I could not set the given voice. There was a problem!");
                return Problem(name);
            }
        }

        /// <summary>
        /// Get voice info for given voice name.
        /// </summary>
        /// <param name="name"><see cref="VoiceInfo.Name"/> as a search string</param>
        /// <returns><see cref="VoiceInfo"/></returns>
        [HttpGet]
        [Route("synthesizer/info")]
        public async Task<InstalledVoice?> GetVoiceAsync(
             [FromQuery] string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return default;
            }           
            var result = await GetInstalledVoiceInfo(name);
            return result;
        }

        /// <summary>
        /// Get currently active voice recognizer info.
        /// </summary>
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
        public async Task<Grammar?> CreateGrammarFromFileAsync(
            [FromQuery] string filePath,
            [FromQuery] string name)
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
                BaseControllerHelpers.IsCompleted = false;
            }
            else
            {
                BaseControllerHelpers.IsCompleted = true;
            }
            RecognitionResultBuilder.AppendLine($"{nameof(BaseControllerHelpers.IsCompleted)}: {BaseControllerHelpers.IsCompleted}, Result: {RecognitionResultBuilder}");
         
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
                BaseControllerHelpers.LocalTextFromAudioFile = CreateAudioFile(audioFile);
                SpeechUI.SendTextFeedback(e.Result, $"Audio file created:\t{BaseControllerHelpers.LocalTextFromAudioFile}", false);
            }
        }

        private string CreateAudioFile(
            RecognizedAudio audioFile)
        {
            BaseControllerHelpers.LocalTextFromAudioFile = Path.Combine(BaseControllerHelpers.LocalAudioFolder, $"{nameof(BaseControllerHelpers.LocalTextFromAudioFile)}{(new Random()).Next()}.wav");
            FileStream waveStream = new FileStream(BaseControllerHelpers.LocalTextFromAudioFile, FileMode.Create);
            audioFile.WriteToWaveStream(waveStream);
            waveStream.Flush();
            waveStream.Close();
            return BaseControllerHelpers.LocalTextFromAudioFile;
        }

    }

}
