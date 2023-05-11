using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Runtime.Versioning;
using static System.Net.Mime.MediaTypeNames;
using System.Speech.AudioFormat;
using System.Text;
using OpenAI_API;
using System.Diagnostics;

namespace SynthNetVoice.Controllers
{
    [ApiController]
    [Route("api")]
    [SupportedOSPlatform("windows")]
    public class SpeechController : ControllerBase
    {
        private readonly IConfiguration LocalConfig;
        private readonly ILogger<SpeechController> LocalLogger;

        private readonly OpenAIAPI OpenAIAPI;
        private readonly PromptBuilder prompt;
        private readonly SpeechSynthesizer synthesizer;
        private readonly APIAuthentication APIAuthentication;

        public SpeechController(ILogger<SpeechController> logger, IConfiguration config)
        {
            LocalConfig = config;
            LocalLogger = logger;

            APIAuthentication = new APIAuthentication(LocalConfig.GetValue<string>("OPENAI_API_KEY"), LocalConfig.GetValue<string>("OPENAI_ORGANIZATION"));
            prompt = new PromptBuilder();
            synthesizer = new SpeechSynthesizer();
            OpenAIAPI = new OpenAIAPI(APIAuthentication);

#if DEBUG
            LocalLogger.LogDebug($"{nameof(Created)}", nameof(SpeechController));
            Debug.Write($"{nameof(APIAuthentication.OpenAIOrganization)}", APIAuthentication.OpenAIOrganization);
            Debug.Write($"{nameof(APIAuthentication.ApiKey)}", APIAuthentication.ApiKey);
         
#endif

        }

        /// <summary>
        /// Get installed Microsoft voices.
        /// </summary>
        /// <param name="spoken">Defaults to false. If true, will also speak out the response.</param>
        /// <param name="gpt">Defaults to false, using locally installed voice. When true, will use Conversation AI.</param>
        /// <param name="verbose">Defaults to false. If true, gives additional voice info.</param>
        [HttpGet]
        [Route("voices/installed")]
        public string InstalledVoices(bool spoken = false, bool gpt = false, bool verbose = false)
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

                if (verbose)
                {
                    AdditionalVoiceInfo(infoBuilder, info);
                }

                infoBuilder.AppendLine();
            }

            if (spoken)
            {
                Prompt(string.Empty, infoBuilder.ToString(), gpt);
            }
            return infoBuilder.ToString();
        }

        private static void AdditionalVoiceInfo(StringBuilder infoBuilder, VoiceInfo info)
        {
            string AdditionalInfo = "";
            foreach (string key in info.AdditionalInfo.Keys)
            {
                AdditionalInfo += String.Format("  {0}: {1}\n", key, info.AdditionalInfo[key]);
            }

            infoBuilder.AppendLine(" Additional Info - " + AdditionalInfo);
        }

        /// <summary>
        /// The following operation will have your synthezised voice speak out the given text prompt, for a given Fallout 4 NPC.
        /// </summary>
        /// <param name="npc">Name of Fallout 4 NPC. Required when using gpt.</param>
        /// <param name="question">Your question for this NPC when using gpt, else the text you want to hear spoken.</param>
        /// <param name="gpt">Defaults to false, using locally installed voice. When true, will use Conversation AI.</param>
        [HttpGet]
        [Route("prompt")]
        public async void Prompt(string npc = "", string question = "", bool gpt = false)
        {
            var chatBuilder = new StringBuilder();

            if (string.IsNullOrEmpty(question))
            {
                chatBuilder.AppendLine($"I have nothing to say!");
                prompt.AppendText(chatBuilder.ToString());
                synthesizer.Speak(prompt);
            }
            else
            {
                if (!string.IsNullOrEmpty(npc) && gpt)
                {
                    /*
                      --request POST \
                      --url https://api.openai.com/v1/audio/transcriptions \
                      --header 'Authorization: Bearer TOKEN' \
                      --header 'Content-Type: multipart/form-data' \
                      --form file=@/path/to/file/openai.mp3 \
                      --form model=whisper-1
                    */
                    var chat = OpenAIAPI.Chat.CreateConversation();

                    // give instruction as System
                    string npcInstructions = System.IO.File.ReadAllText(Path.Combine("Models", "FalloutNpcInstructions.txt")).Replace("__NPC__", npc);
                    chat.AppendSystemMessage(npcInstructions);

                    // give a few examples as user and assistant
                    chat.AppendUserInput("Who are you?");
                    chat.AppendExampleChatbotOutput($"I am {npc}");
                    chat.AppendUserInput("What world do you live in?");
                    chat.AppendExampleChatbotOutput("I live in the apocalyptic post-nuclear game world of Fallout.");

                    // now let's ask it a question
                    chat.AppendUserInput(question);

                    // and get the response
                    // the entire chat history is available in chat.Messages
                    await chat.StreamResponseFromChatbotAsync(res =>
                    {
                        chatBuilder.Append(res);
                    });

                    prompt.AppendText(chatBuilder.ToString());
                    synthesizer.Speak(prompt);
                }
                else
                {
                    chatBuilder.AppendLine(question);
                    prompt.AppendText(chatBuilder.ToString());
                    synthesizer.Speak(prompt);
                }
            }
        }

    }

}
