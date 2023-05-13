using Microsoft.AspNetCore.Mvc;
using OpenAI_API.Chat;
using SynthNetVoice.Data.Instructions;
using SynthNetVoice.Data.Models;
using System;
using System.Runtime.Versioning;
using System.Text;

namespace SynthNetVoice.Controllers.v1
{
    /// <summary>
    /// Manages NPC actions.
    /// </summary>
    [Route("npc")]
    [SupportedOSPlatform("windows")]
    public class NpcController : BaseController
    {
        public NpcController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
            LocalConversation = LocalOpenAIAPI.Chat.CreateConversation();
        }

        /// <summary>
        /// Conversation which encapsulates an AI ongoing chat.
        /// </summary>
        public Conversation LocalConversation { get; set; }
        /// <summary>
        /// Get/set the instructions for the NPC bot..
        /// </summary>
        public static string LocalSystemInstructions { get; set; } = string.Empty;
        /// <summary>
        /// Get/set the instructions for the NPC bot..
        /// </summary>
        public static string LocalUserInstructions { get; set; } = string.Empty;
        /// <summary>
        /// Inidicates NPC bot state.
        /// </summary>
        public static bool IsInitialized { get; set; } = false;
        /// <summary>
        /// Name of the NPC.
        /// </summary>
        public static string NpcName { get; set; } = string.Empty;
        /// <summary>
        /// Game of the NPC.
        /// </summary>
        public static string GameName { get; set; } = string.Empty;

        /// <summary>
        /// Required first, if you want an immersive NPC for your game!
        /// When not given, uses Fallout 4 and Codsworth as defaults and default instructions.
        /// </summary>
        /// <param name="npcName">Your NPC's name in the game.</param>
        [HttpPost]
        [Route("init")]
        public async Task<bool> InitAsync(
            string? gameName,
            string? npcName
            )
        {
            try
            {
                NpcName = npcName ?? string.Empty;
                GameName = gameName ?? string.Empty;
                GameName =GameName.Trim().ToLower();

                if ( !string.IsNullOrEmpty(GameName))
                {
                    LocalSystemInstructions = await InstructionsManager.GetSystemInstructionsAsync(GameName, NpcName);
                    LocalConversation.AppendSystemMessage(LocalSystemInstructions);
                    LocalUserInstructions = await InstructionsManager.GetUserInstructionsAsync(GameName, NpcName);
                    LocalConversation.AppendUserInput(LocalUserInstructions);

                    IsInitialized = true;
                    return true;
                }
                else 
                {
                    IsInitialized = false;
                    return false; 
                }
            }
            catch (Exception)
            {
                IsInitialized = false;
                return false;
            }
        }

        /// <summary>
        /// Get the currently configured instructions (system - and user - instruction) that will be used to train the NPC bot.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("instruction")]
        public async Task<Instruction> GetInstruction(
            string? gameName = null, 
            string? npcName = null)
        {
            Instruction instruction = new();
            try
            {
                NpcName = npcName ?? string.Empty;
                GameName = gameName ?? string.Empty;
                GameName = GameName.Trim().ToLower();

                if(!string.IsNullOrEmpty(GameName))
                {
                    instruction = await InstructionsManager.GetInstruction(GameName, NpcName);
                }
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
            string? gameName = null,
            string? npcName = null,
            string question = "",
            bool scribe = true,
            bool gpt = false)
        {

            NpcName = npcName ?? string.Empty;
            GameName = gameName ?? string.Empty;
            GameName = GameName.Trim().ToLower();

            if (!string.IsNullOrEmpty(GameName))
            {
                var promptBuilder = new StringBuilder();
                string responseToPrompt = string.Empty;

                var task = new TaskFactory().StartNew(async () =>
                {
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
                                LocalConversation ??= LocalOpenAIAPI.Chat.CreateConversation();

                                LocalSystemInstructions = await InstructionsManager.GetSystemInstructionsAsync(GameName, NpcName);
                                LocalConversation.AppendSystemMessage(LocalSystemInstructions);

                                LocalSystemInstructions = await InstructionsManager.GetUserInstructionsAsync(GameName, NpcName);
                                LocalConversation.AppendUserInput(LocalSystemInstructions);

                                LocalConversation.AppendUserInput("Who are you?");
                                LocalConversation.AppendExampleChatbotOutput($"I am {NpcName}");

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
                        else
                        {
                            promptBuilder.AppendLine(question);
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
                responseToPrompt = await result;
                return responseToPrompt;
            }
            else
            {
                return string.Empty;
            }            
        }

    }

}
