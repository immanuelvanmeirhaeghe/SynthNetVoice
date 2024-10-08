﻿using Microsoft.AspNetCore.Mvc;
using SynthNetVoice.Data.Enums;
using SynthNetVoice.Data.Helpers;
using SynthNetVoice.Data.Models;
using System.Media;
using System.Runtime.Versioning;
using System.Speech.Recognition;
using System.Text;

namespace SynthNetVoice.Controllers.v1
{
    /// <summary>
    /// Manages audio actions.
    /// </summary>
    [Route("audio")]
    [ApiController]
    [SupportedOSPlatform("windows")]
    public class AudioController : BaseController
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="config"></param>
        public AudioController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
        }

        /// <summary>
        /// Transcribe audio into whatever language the audio is in.
        /// </summary>
        /// <param name="audioFileInfo">Path to input audio file</param>
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
            Transcription? responseToPrompt = default;
            Transcription? script = default; 
            var task = new TaskFactory().StartNew( () =>
            {
                if (audioFileInfo != null && !string.IsNullOrEmpty(audioFileInfo.FilePath))
                {
                    string[] fileParts = audioFileInfo.FilePath.Split("\\");
                    BaseControllerHelpers.LocalAudioFile = fileParts[fileParts.Length - 1];
                    BaseControllerHelpers.LocalAudioFileTitle = BaseControllerHelpers.LocalAudioFile.Split(".")[0];
                    string LocalAudioFilePath = audioFileInfo.FilePath.Replace(BaseControllerHelpers.LocalAudioFile, string.Empty) ;
                    BaseControllerHelpers.LocalTextFromAudioFile = Path.Combine(LocalAudioFilePath, $"{BaseControllerHelpers.LocalAudioFileTitle}.txt");
                    string[] localParts = BaseControllerHelpers.LocalAudioFile.Split("_");
                    if (localParts != null && localParts.Length == 3)
                    {
                        BaseControllerHelpers.LocalNpcName = localParts[1];
                        BaseControllerHelpers.LocalGameName = Enum.Parse<GameNames>(localParts[2]);
                    }
                    SpeechRecognitionEngine LocalRecognitionEngine = new SpeechRecognitionEngine
                    {
                        BabbleTimeout = new TimeSpan(int.MaxValue),
                        InitialSilenceTimeout = new TimeSpan(int.MaxValue),
                        EndSilenceTimeout = new TimeSpan(100000000),
                        EndSilenceTimeoutAmbiguous = new TimeSpan(100000000)
                    };
                    Grammar grm = new DictationGrammar();
                    LocalRecognitionEngine.LoadGrammar(grm);
                    LocalRecognitionEngine.SetInputToWaveFile(audioFileInfo.FilePath);
                    RecognitionResult result =  LocalRecognitionEngine.Recognize();
                    if (result != null)
                    {
                        System.IO.File.WriteAllText(BaseControllerHelpers.LocalTextFromAudioFile, result.Text);
                        LocalPrompt.AppendText(result.Text);
                        LocalSynthesizer.Speak(LocalPrompt);

                        script = new Transcription
                        {
                            TextFilePath = BaseControllerHelpers.LocalTextFromAudioFile,
                            SoundPlayer = new SoundPlayer(audioFileInfo.FilePath)
                        };
                        BaseControllerHelpers.LocalTextFromAudioFile = LogConversation(script);
                    }
                }           
                return script;
            });

            responseToPrompt = await task;          
            return Ok(responseToPrompt);
        }

        /// <summary>
        ///  Creates audio file from given text input file.
        /// </summary>
        /// <param name="script">The <see cref="Transcription"/> info</param>
        [Route("create")]
        [HttpPost]
        public new async Task<IActionResult> TextToSpeech(
            [FromBody] Transcription script)
        {
            if (script == null || ( script != null && string.IsNullOrEmpty(script.TextFilePath) ) )
            {
                return BadRequest(script);
            }
            
            var task = new TaskFactory().StartNew(() =>
            {
                if (script != null
                 && !string.IsNullOrEmpty(script.TextFilePath) 
                 && !string.IsNullOrEmpty(script.AudioFilePath)
                 && !string.IsNullOrEmpty(script.AudioFileName))
                {
                    string audioFilepath = Path.Combine(script.AudioFilePath, script.AudioFileName);
                    LocalSynthesizer.SetOutputToWaveFile(audioFilepath);
                    string textFilePath = script.TextFilePath;
                    string content = System.IO.File.ReadAllText(textFilePath);
                    LocalPrompt.AppendText(content);
                    LocalSynthesizer.Speak(LocalPrompt);

                    script.SoundPlayer = new SoundPlayer(audioFilepath);
                    //script.SoundPlayer.Play();
                }                  
            });

            await task;
           
            return Ok(script);
        }

    }

}
