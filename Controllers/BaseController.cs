using System.Speech.Recognition;
using Microsoft.AspNetCore.Mvc;
using OpenAI_API;
using SynthNetVoice.Controllers.v1;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using SynthNetVoice.Data.Enums;
using System.Text;
using SynthNetVoice.Data.Models;
using System.Windows;
namespace SynthNetVoice.Controllers
{

    [Route("api")]
    [ApiController]
    [SupportedOSPlatform("windows")]
    public class BaseController : ControllerBase
    {
        private const string ConversationTemplateAppend = "<div style=\"height:auto; width:auto;\">{{__TEXT__}}</div>{{__APPEND__}}";
        private const string ConversationTemplateFile = "D:\\Workspaces\\VSTS\\SynthNetVoice.Data\\Logs\\log.html";
        private const string ConversationTemplateTitleParam = "{{__TITLE__}}";
        private const string ConversationTemplateTextParam = "{{__TEXT__}}";
        private const string ConversationTemplateAppendParam = "{{__APPEND__}}";

        public const string LocalConversationFolder = "D:\\Workspaces\\VSTS\\SynthNetVoice.Data\\Logs\\";
        public const string LocalAudioFolder = $"D:\\Workspaces\\VSTS\\SynthNetVoice.Data\\Voices\\";

        public readonly IConfiguration LocalConfiguration;
        public readonly ILogger<PlayerController> LocalLogger;

        public readonly OpenAIAPI LocalOpenAIAPI;
        public readonly PromptBuilder LocalPrompt;
        public readonly SpeechSynthesizer LocalSynthesizer;
        public readonly SpeechRecognizer LocalRecognizer;
        public readonly APIAuthentication LocalAPIAuthentication;

        public static string LocalTextFromAudioFile { get; set; } = string.Empty;
        public static string LocalAudioFile { get; set; } = string.Empty;
        public static string LocalAudioFileTitle { get; set; } = string.Empty;
        public static string LocalConversationFile { get; set; } = string.Empty;
        public static string SelectedVoiceInfoName { get; set; } = string.Empty;
        public static bool IsCompleted { get; set; } = false;
        /// <summary>
        /// Name of the NPC.
        /// </summary>
        public static string LocalNpcName { get; set; } = "MamaMurphy";
        /// <summary>
        /// Game of the NPC.
        /// </summary>
        public static GameNames LocalGameName { get; set; } = GameNames.Fallout4;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="config"></param>
        public BaseController(ILogger<PlayerController> logger, IConfiguration config)
        {
            LocalConfiguration = config;
            LocalLogger = logger;
            LocalTextFromAudioFile = string.Empty;
            SelectedVoiceInfoName = string.Empty;
            LocalConversationFile = string.Empty;
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
        /// <param name="script">conversation text logged</param>
        /// <returns>Log conversation</returns>
        [Route("log")]
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        public string LogConversation(
            Transcription script)
        {
            string title = $"Prompt_{LocalNpcName}_{LocalGameName}";
            LocalConversationFile = Path.Combine(LocalConversationFolder, $"{title}_{DateTime.Now:yyyyddMM}.html");
            string template;
            if (!System.IO.File.Exists(LocalConversationFile))
            {
                template = System.IO.File.ReadAllText(ConversationTemplateFile);
                template = template.Replace(ConversationTemplateTitleParam, title);
            }
            else
            {
                template = System.IO.File.ReadAllText(LocalConversationFile);
            }

            template = template.Replace(ConversationTemplateTextParam, script.Text).Replace(ConversationTemplateAppendParam, ConversationTemplateAppend);
            System.IO.File.WriteAllText(LocalConversationFile, template);
            return LocalConversationFile;
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
            if (script != null && !string.IsNullOrEmpty(script.Text) && !string.IsNullOrEmpty(script.AudioFileName))
            {
                LocalPrompt.AppendText(script.Text);
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
                if (string.IsNullOrEmpty(SelectedVoiceInfoName))
                {
                    var voice = LocalSynthesizer.GetInstalledVoices().FirstOrDefault(v => v.Enabled == true);
                    if (voice != null)
                    {
                        SelectedVoiceInfoName = voice.VoiceInfo.Name;
                    }                  
                }
                LocalSynthesizer.SelectVoice(SelectedVoiceInfoName);
                string voicename = SelectedVoiceInfoName.ToLower().Replace(" ", "_");
                GameNames game = Enum.Parse<GameNames>(gameName);
                string audioPath = game switch
                {
                    GameNames.Fallout4 => $"f4_{voicename}",
                    GameNames.GreenHell => $"gh_{voicename}",
                    GameNames.None => $"other_{voicename}",
                    _ => $"x_{voicename}",
                };
                Transcription script = new Transcription
                {
                    Text = text,
                    AudioFilePath = Path.Combine(LocalAudioFolder, audioPath, "wavs")
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
            if (script != null && !string.IsNullOrEmpty(script.Text) && !string.IsNullOrEmpty(script.AudioFilePath))
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
                        metacontent = $"{tempFileName}|{script.Text.Trim()}|{script.Text.Trim()}";
                    }
                    else
                    {
                        metacontent = $"{metacontent}\n{tempFileName}|{script.Text.Trim()}|{script.Text.Trim()}";
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
            LocalNpcName = npcName.Trim();
            LocalGameName = Enum.Parse<GameNames>(gameName);
            if (LocalGameName == GameNames.None || string.IsNullOrEmpty(LocalNpcName))
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
