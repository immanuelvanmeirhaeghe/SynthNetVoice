using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI_API;
using OpenAI_API.Chat;
using SynthNetVoice.Data.Instructions;
using SynthNetVoice.Data.Models;
using System.Runtime.Versioning;
using System.Text;

namespace SynthNetVoice.Controllers.v1
{
    [Route("npc")]
    [SupportedOSPlatform("windows")]
    public class NpcController : BaseController
    {
        public NpcController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
            LocalConversation = LocalOpenAIAPI.Chat.CreateConversation();
            LocalNpcInstructions = string.Empty;
        }

        /// <summary>
        /// Conversation which encapsulates an AI ongoing chat.
        /// </summary>
        public Conversation LocalConversation { get; set; }
        /// <summary>
        /// Get/set the instructions for the NPC bot..
        /// </summary>
        public string LocalNpcInstructions { get; set; }
        /// <summary>
        /// Inidicates NPC bot state.
        /// </summary>
        public bool IsInitialized { get; set; } = false;

        /// <summary>
        /// Required first, if you want an immersive NPC for your game!
        /// </summary>
        /// <param name="npcName">Your NPC's name in the game.</param>
        [HttpPost]
        [Route("init")]
        public async Task<bool> InititializeNpcBotAsync(string npcName)
        {
            try
            {
                LocalNpcInstructions = await InstructionsManager.GetSystemInstructionsAsync(npcName);
                LocalConversation.AppendSystemMessage(LocalNpcInstructions);

                LocalConversation.AppendUserInput("Who are you?");
                LocalConversation.AppendExampleChatbotOutput($"I am {npcName}");

                LocalNpcInstructions = await InstructionsManager.GetUserInstructionsAsync(npcName);
                LocalConversation.AppendSystemMessage(LocalNpcInstructions);
                IsInitialized = true;
                return true;
            }
            catch (Exception)
            {
                IsInitialized = false;
                return false;
            }
        }

        [HttpGet]
        [Route("instruction")]
        public async Task<Instruction> GetInstruction()
        {
            Instruction instruction = new();
            try
            {
                instruction = await InstructionsManager.GetInstruction();
                return instruction;
            }
            catch (Exception)
            {
                return instruction;
            }
        }

        /// <summary>
        /// The following operation will have your synthezised voice speak out the given text prompt, for a given NPC.        
        /// </summary>
        /// <param name="question">Your question for this NPC when using gpt, else the text you want to hear spoken.</param>
        /// <param name="scribe">Defaults to true, transcribing answers to text and logging the conversation to local log file. When false, conversation is lost.</param>
        /// <param name="gpt">Defaults to false, using locally installed voice. When true, will use Conversation AI.</param>
        [HttpGet]
        [Route("prompt")]
        public async Task<string> Prompt(
            string question = "",
            bool scribe = true,
            bool gpt = false)
        {
            var task = new TaskFactory().StartNew(async () =>
            {
                var promptBuilder = new StringBuilder();

                if (string.IsNullOrEmpty(question))
                {
                    promptBuilder.AppendLine($"I have nothing to say!");
                }
                else
                {
                    if (gpt)
                    {
                        if (IsInitialized)
                        {
                            LocalConversation.AppendUserInput(question);
                            await LocalConversation.StreamResponseFromChatbotAsync(res =>
                            {
                                promptBuilder.Append(res);
                            });
                        }
                        else
                        {
                            promptBuilder.AppendLine($"I have not yet been instructed who I am! Please give instructions by posting to operation /npc/init.");
                        }
                    }
                }
                LocalPrompt.AppendText(promptBuilder.ToString());
                LocalSynthesizer.Speak(LocalPrompt);
                if (scribe)
                {
                    LocalLogger.Log(LogLevel.Information, nameof(Prompt), promptBuilder.ToString());
                }
                return promptBuilder.ToString();
            });
            var result = await task;
            string responseToPrompt = await result;
            return responseToPrompt;
        }

    }
}
