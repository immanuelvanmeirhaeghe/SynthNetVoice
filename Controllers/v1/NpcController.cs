using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using SynthNetVoice.Data.Helpers;
using SynthNetVoice.Data.Instructions;
using SynthNetVoice.Data.Models;
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
        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="config"></param>
        public NpcController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        {
            NpcControllerHelpers.LocalConversation = NewConversation();
        }

        [Route("conversation")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        private ChatClient NewConversation()
        {
            return LocalOpenAIClient.GetChatClient("gpt-4o-mini");
        }

        [Route("conversation/append")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        private async Task AppendInstructions()
        {
            NpcControllerHelpers.LocalMessages ??= [];
            NpcControllerHelpers.LocalConversation ??= NewConversation();
            NpcControllerHelpers.LocalSystemInstructions =
                await InstructionsManager.GetSystemInstructionsAsync(BaseControllerHelpers.LocalGameName.ToString(), BaseControllerHelpers.LocalNpcName);
            NpcControllerHelpers.LocalUserInstructions =
                await InstructionsManager.GetUserInstructionsAsync(BaseControllerHelpers.LocalGameName.ToString(), BaseControllerHelpers.LocalNpcName);
            NpcControllerHelpers.LocalMessages.Add(new SystemChatMessage(NpcControllerHelpers.LocalSystemInstructions));
            NpcControllerHelpers.LocalMessages.Add(new SystemChatMessage(NpcControllerHelpers.LocalUserInstructions));
        }

        [Route("train")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        private async Task<IActionResult> TrainNpc()
        {
            try
            {
                await AppendInstructions();
                NpcControllerHelpers.LocalMessages.Add(new SystemChatMessage($"When asked: What is your name? You should reply: My name is {BaseControllerHelpers.LocalNpcName}."));
                NpcControllerHelpers.IsTrained = true;
                return Ok(NpcControllerHelpers.IsTrained);
            }
            catch (Exception exc)
            {
                NpcControllerHelpers.IsTrained = false;
                return Problem(exc.Message);
            }
        }

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
                NpcControllerHelpers.IsInitialized = true;
                return Ok(new Instruction { FromSystem = NpcControllerHelpers.LocalSystemInstructions, FromUser = NpcControllerHelpers.LocalUserInstructions });
            }
            catch (Exception exc)
            {
                NpcControllerHelpers.IsInitialized = false;
                return Problem(exc.Message);
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
                if (!NpcControllerHelpers.IsInitialized)
                {
                    return Problem("Please run npc/init first!");
                }
                if (!ValidateDefaultParameters(gameName.ToString(), npcName))
                {
                    return BadRequest(new { gameName, npcName });
                }
                Instruction instruction = await InstructionsManager.GetInstruction(BaseControllerHelpers.LocalGameName.ToString(), BaseControllerHelpers.LocalNpcName);
                return Ok(instruction);
            }
            catch (Exception exc)
            {
                return Problem(exc.Message);
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
            if (!NpcControllerHelpers.IsInitialized)
            {
                return Problem("Please run npc/init first!");
            }
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
                        if (NpcControllerHelpers.IsInitialized)
                        {
                            await TrainNpc();
                            if (NpcControllerHelpers.IsTrained && NpcControllerHelpers.LocalConversation != null)
                            {
                                NpcControllerHelpers.LocalMessages.Add(new UserChatMessage(question));
                                var result = NpcControllerHelpers.LocalConversation.CompleteChatStreaming(NpcControllerHelpers.LocalMessages);
                                foreach (StreamingChatCompletionUpdate completionUpdate in result)
                                {
                                    if (completionUpdate.ContentUpdate.Count > 0)
                                    {
                                        promptBuilder.Append(completionUpdate.ContentUpdate[0].Text);
                                    }
                                }
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
                LocalLogger.Log(LogLevel.Information, responseToPrompt, nameof(Prompt));
            }

            return Ok(script);
        }

    }

}
