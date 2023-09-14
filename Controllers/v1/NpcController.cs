using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenAI_API.Chat;
using SynthNetVoice.Data.Instructions;
using SynthNetVoice.Data.Models;
using System;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;
using System.Media;
using SynthNetVoice.Data.Enums;

namespace SynthNetVoice.Controllers.v1
{

    /// <summary>
    /// Manages NPC actions.
    /// </summary>
    [Route("npc")]
    [SupportedOSPlatform("windows")]
    public class NpcController : BaseController
    {
        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="config"></param>
        public NpcController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
            LocalConversation = NewConversation();
        }

        [Route("conversation")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        private Conversation NewConversation()
        {
            return LocalOpenAIAPI.Chat.CreateConversation();
        }

        [Route("conversation/append")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        private async Task AppendInstructions()
        {
            LocalConversation ??= NewConversation();
            LocalSystemInstructions = await InstructionsManager.GetSystemInstructionsAsync(LocalGameName.ToString(), LocalNpcName);
            LocalConversation.AppendSystemMessage(LocalSystemInstructions);
            LocalUserInstructions = await InstructionsManager.GetUserInstructionsAsync(LocalGameName.ToString(), LocalNpcName);
            LocalConversation.AppendUserInput(LocalUserInstructions);
        }

        [Route("train")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        private async Task<bool> TrainNpc()
        {
            LocalConversation ??= LocalOpenAIAPI.Chat.CreateConversation();
            LocalSystemInstructions = await InstructionsManager.GetSystemInstructionsAsync(LocalGameName.ToString(), LocalNpcName);
            LocalConversation.AppendSystemMessage(LocalSystemInstructions);
            LocalSystemInstructions = await InstructionsManager.GetUserInstructionsAsync(LocalGameName.ToString(), LocalNpcName);
            LocalConversation.AppendUserInput(LocalSystemInstructions);
            LocalConversation.AppendUserInput("What is your name?");
            LocalConversation.AppendExampleChatbotOutput($"My name is {LocalNpcName}.");
            
            IsTrained = true;
            return IsTrained;
        }

        /// <summary>
        /// Conversation which encapsulates an AI ongoing chat.
        /// </summary>
        public static Conversation? LocalConversation { get; set; } = default;
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
        /// Status NPC training
        /// </summary>
        public static bool IsTrained { get; set; } = false;

        /// <summary>
        /// Required first, if you want an immersive NPC for your game!
        /// When not given, uses Fallout 4 and MamaMurphy as defaults and default instructions.
        /// </summary>
        /// <param name="gameName">Your game where the NPC resides</param>
        /// <param name="npcName">Your NPC's name in the game.</param>
        [Route("init")]
        [HttpPost]
        public async Task<IActionResult> InitAsync(
            [FromQuery] string gameName = "Fallout4",
            [FromQuery] string npcName = "MamaMurphy")
        {
            try
            {
                if (!ValidateDefaultParameters(gameName.ToString(), npcName))
                {
                    return BadRequest(new { gameName, npcName });
                }

                await AppendInstructions();

                IsInitialized = true;
                return Ok(new Instruction { FromSystem = LocalSystemInstructions, FromUser = LocalUserInstructions });
            }
            catch (Exception)
            {
                IsInitialized = false;
                return Problem();
            }
        }

        /// <summary>
        /// Get the currently configured instructions (system - and user - instruction) that will be used to train the NPC bot.
        /// </summary>
        /// <param name="gameName">Your game where the NPC resides</param>
        /// <param name="npcName">Your NPC's name in the game.</param>
        [Route("instruction")]
        [HttpGet]
        public async Task<IActionResult> GetInstruction(
            [FromQuery] string gameName = "Fallout4",
            [FromQuery] string npcName = "MamaMurphy")
        {
            try
            {
                if (!ValidateDefaultParameters(gameName.ToString(), npcName))
                {
                    return BadRequest(new { gameName, npcName });
                }
                Instruction instruction = await InstructionsManager.GetInstruction(LocalGameName.ToString(), LocalNpcName);
                return Ok(instruction);
            }
            catch (Exception)
            {
                return Problem();
            }
        }

        /// <summary>
        /// The following operation will have your synthezised voice speak out the given text prompt, for a given NPC.        
        /// </summary>
        /// <param name="question">Your question for this NPC when using gpt, else the text you want to hear spoken.</param>
        /// <param name="scribe">Defaults to true, transcribing answers to text and logging the conversation to local log file. When false, conversation is lost.</param>
        /// <param name="gpt">Defaults to false, using locally installed voice. When true, will use Conversation AI.</param>
        /// <param name="gameName">Your game where the NPC resides</param>
        /// <param name="npcName">Your NPC's name in the game.</param>
        [Route("prompt")]
        [HttpGet]
        public async Task<IActionResult> Prompt(
            [FromQuery] string gameName = "Fallout4",
            [FromQuery] string npcName = "MamaMurphy",
            [FromQuery] string question = "",
            [FromQuery] bool scribe = true,
            [FromQuery] bool gpt = false)
        {
            if (!ValidateDefaultParameters(gameName.ToString(), npcName))
            {
                return BadRequest(new { gameName, npcName });
            }

            string responseToPrompt = string.Empty;

            var task = new TaskFactory().StartNew(async () =>
            {
                var promptBuilder = new StringBuilder();

                if (string.IsNullOrEmpty(question))
                {
                    promptBuilder.AppendLine($"You have not prepared any question for {npcName}. Set {nameof(npcName)}, {nameof(gameName)} and type in your {nameof(question)}.");
                }
                else
                {
                    if (gpt)
                    {
                        if (IsInitialized)
                        {
                            IsTrained = await TrainNpc();
                            if (IsTrained && LocalConversation != null)
                            {
                                LocalConversation.AppendUserInput(question);
                                await LocalConversation.StreamResponseFromChatbotAsync(res =>
                                {
                                    promptBuilder.Append(res);
                                });
                            }
                        }
                        else
                        {
                            promptBuilder.AppendLine($"You have not yet set instructions for the {npcName}! Set instruction through POST npc/init.");
                        }
                    }
                    else
                    {
                        promptBuilder.AppendLine($"Your prepared question for {npcName}: {question}");
                    }
                }
                return promptBuilder.ToString();
            });

            var result = await task;
            responseToPrompt = await result;

            Transcription script = await NewScript(gameName, responseToPrompt);
            SpeechToText(script);
            TextToSpeech(script);
            if (scribe)
            {
                LogConversation(script);
                LocalLogger.Log(LogLevel.Information, nameof(Prompt), responseToPrompt);
            }

            return Ok(script);
        }

    }

}
