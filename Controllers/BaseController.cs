using System.Speech.Recognition;
using Microsoft.AspNetCore.Mvc;
using SynthNetVoice.Controllers.v1;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using SynthNetVoice.Data.Enums;
using SynthNetVoice.Data.Models;
using OpenAI;
using SynthNetVoice.Data.Helpers;
namespace SynthNetVoice.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    [Route("api")]
    [ApiController]
    [SupportedOSPlatform("windows")]
    public class BaseController : ControllerBase
    {
        /// <summary>
        /// The local configuration file
        /// </summary>
        public readonly IConfiguration LocalConfiguration;
        /// <summary>
        /// A local logger instance for player actions
        /// </summary>
        public readonly ILogger<PlayerController> LocalLogger;
        /// <summary>
        /// Entry to sdk for OpenAI api access
        /// </summary>
        public readonly OpenAIClient LocalOpenAIClient;
        /// <summary>
        /// System.Speech.Synthesis.PromptBuilder
        /// </summary>
        public readonly PromptBuilder LocalPrompt;
        /// <summary>
        /// System.Speech.Synthesis.SpeechSynthesizer
        /// </summary>
        public readonly SpeechSynthesizer LocalSynthesizer;
        /// <summary>
        /// System.Speech.Recognition.SpeechRecognizer
        /// </summary>
        public readonly SpeechRecognizer LocalRecognizer;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="config"></param>
        public BaseController(ILogger<PlayerController> logger, IConfiguration config)
        {
            LocalConfiguration = config;
            LocalLogger = logger;
            BaseControllerHelpers.LocalTextFromAudioFile = string.Empty;
            BaseControllerHelpers.SelectedVoiceInfoName = string.Empty;
            BaseControllerHelpers.LocalConversationFile = string.Empty;
            BaseControllerHelpers.IsCompleted = false;

            LocalPrompt ??= new PromptBuilder();
            LocalSynthesizer ??= new SpeechSynthesizer();            
            LocalRecognizer ??= new SpeechRecognizer();
            LocalOpenAIClient ??= new(LocalConfiguration.GetValue<string>("OPENAI_API_KEY"));
        }

        /// <summary>
        /// Log conversation text with an NPC to a given local file.
        /// </summary>
        /// <param name="script">conversation text logged</param>
        /// <returns>Log conversation</returns>
        [Route("log")]
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        public string LogConversation(
            Transcription script)
        {
            string title = $"{nameof(LocalPrompt)}_{BaseControllerHelpers.LocalNpcName}_{BaseControllerHelpers.LocalGameName}";
            BaseControllerHelpers.LocalConversationFile = Path.Combine(BaseControllerHelpers.LocalConversationFolder, $"{title}_{DateTime.Now:yyyyddMM}.html");
            string template;
            if (!System.IO.File.Exists(BaseControllerHelpers.LocalConversationFile))
            {
                template = System.IO.File.ReadAllText(BaseControllerHelpers.ConversationTemplateFile);
                template = template.Replace(BaseControllerHelpers.ConversationTemplateTitleParam, title);
            }
            else
            {
                template = System.IO.File.ReadAllText(BaseControllerHelpers.LocalConversationFile);
            }

            template = template.Replace(
                BaseControllerHelpers.ConversationTemplateTextParam,
                script.TextFilePath).Replace(BaseControllerHelpers.ConversationTemplateAppendParam, BaseControllerHelpers.ConversationTemplateAppend);
            System.IO.File.WriteAllText(BaseControllerHelpers.LocalConversationFile, template);
            return BaseControllerHelpers.LocalConversationFile;
        }

        /// <summary>
        /// Find installed voice info for given voice name.
        /// </summary>
        /// <param name="name"><see cref="VoiceInfo.Name"/> as a search string</param>
        /// <returns><see cref="VoiceInfo"/></returns>
        [Route("installed/voice/info")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<InstalledVoice?> GetInstalledVoiceInfo(
             [FromQuery] string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return default;
            }
            string validated = name.Trim().ToLower();
            var task = new TaskFactory().StartNew(() =>
            {
                var list = LocalSynthesizer.GetInstalledVoices().ToList();
                var voice = list.Find(iv => iv.VoiceInfo.Id.ToLower().Contains(validated));
                return voice;
            });
            var result = await task;
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="script"></param>
        [Route("tts")]
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        public void TextToSpeech(Transcription script)
        {
            if (script != null && !string.IsNullOrEmpty(script.TextFilePath) && !string.IsNullOrEmpty(script.AudioFileName))
            {
                LocalPrompt.AppendText(script.TextFilePath);
                LocalSynthesizer.Speak(LocalPrompt);

                script.SoundPlayer = new System.Media.SoundPlayer(script.AudioFileName);
                script.SoundPlayer.Play();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="script"></param>
        [Route("stt")]
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        public void SpeechToText(Transcription script)
        {
            SetAudio(script);
            var spfi = new SpeechAudioFormatInfo(22050, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
            LocalSynthesizer.SetOutputToWaveFile(script.AudioFileName, spfi);               
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameName"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        [Route("stt/audio/script")]
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<Transcription> NewScript(string gameName, string text)
        {
            var task = new TaskFactory().StartNew( () =>
            {
                if (string.IsNullOrEmpty(BaseControllerHelpers.SelectedVoiceInfoName))
                {
                    var voice = LocalSynthesizer.GetInstalledVoices().FirstOrDefault(v => v.Enabled == true);
                    if (voice != null)
                    {
                        BaseControllerHelpers.SelectedVoiceInfoName = voice.VoiceInfo.Name;
                    }                  
                }
                LocalSynthesizer.SelectVoice(BaseControllerHelpers.SelectedVoiceInfoName);
                string voicename = BaseControllerHelpers.SelectedVoiceInfoName.ToLower().Replace(" ", "_");
                GameNames game = Enum.Parse<GameNames>(gameName);
                string audioPath = game switch
                {
                    GameNames.Fallout4 => $"f4_{voicename}",
                    GameNames.GreenHell => $"gh_{voicename}",
                    GameNames.None => $"other_{voicename}",
                    _ => $"x_{voicename}",
                };
                Transcription script = new()
                {
                    TextFilePath = text,
                    AudioFilePath = Path.Combine(BaseControllerHelpers.LocalAudioFolder, audioPath, "wavs")
                };

                return script;
            });

            var output = await task;
            return output;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="script"></param>
        [Route("stt/audio/script/path")]
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAudio(Transcription script)
        {
            string metacontent = string.Empty;
            string metafilename = string.Empty;
            if (script != null && !string.IsNullOrEmpty(script.AudioFilePath) && !Directory.Exists(script.AudioFilePath))
            {
                Directory.CreateDirectory(script.AudioFilePath);
            }
            if (script != null && !string.IsNullOrEmpty(script.TextFilePath) && !string.IsNullOrEmpty(script.AudioFilePath))
            {
                int wavs = Directory.GetFiles(script.AudioFilePath).Length;
                string tempFileName = $"{wavs}".PadLeft(6, '0');
                tempFileName = $"{tempFileName}.wav";
                script.AudioFileName = Path.Combine(script.AudioFilePath, tempFileName);
                string metafilepath = script.AudioFilePath.Replace("wavs", string.Empty);
                metafilename = Path.Combine(metafilepath, $"metadata.csv");
                if (System.IO.File.Exists(metafilename))
                {
                    metacontent = System.IO.File.ReadAllText(metafilename);
                    if (string.IsNullOrEmpty(metacontent))
                    {
                        metacontent = $"{tempFileName}|{script.TextFilePath.Trim()}|{script.TextFilePath.Trim()}";
                    }
                    else
                    {
                        metacontent = $"{metacontent}\n{tempFileName}|{script.TextFilePath.Trim()}|{script.TextFilePath.Trim()}";
                    }
                 
                }               
            }
            System.IO.File.WriteAllText(metafilename, metacontent);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameName"></param>
        /// <param name="npcName"></param>
        /// <returns></returns>
        [Route("validate")]
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        public bool ValidateDefaultParameters(string? gameName, string? npcName)
        {
            if (string.IsNullOrEmpty(gameName) || string.IsNullOrEmpty(npcName))
            {
                return false;
            }

            BaseControllerHelpers.LocalNpcName = npcName.Trim();
            BaseControllerHelpers.LocalGameName = Enum.Parse<GameNames>(gameName);
            if (BaseControllerHelpers.LocalGameName == GameNames.None || string.IsNullOrEmpty(BaseControllerHelpers.LocalNpcName))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

}
