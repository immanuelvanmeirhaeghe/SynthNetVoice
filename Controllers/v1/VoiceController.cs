using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.Versioning;
using System.Speech.Synthesis;

namespace SynthNetVoice.Controllers.v1
{
    [Route("voice")]
    [SupportedOSPlatform("windows")]
    public class VoiceController : BaseController
    {
        public VoiceController(ILogger<PlayerController> logger, IConfiguration config) : base(logger, config)
        { }

        /// <summary>
        /// Get voice info for given id.
        /// </summary>
        /// <param name="id"><see cref="VoiceInfo.Id"/></param>
        /// <returns><see cref="VoiceInfo"/></returns>
        [HttpGet]
        public async Task<InstalledVoice?> GetVoiceAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return default;
            }
           string  validated = id.Trim().ToLower();
            var task = new TaskFactory().StartNew(() =>
            {
                return LocalSynthesizer.GetInstalledVoices().ToList();
            });
            var result = await task;
            var voice = result.Find(iv => iv.VoiceInfo.Id.ToLower().Contains(validated));
            return voice;
        }

        /// <summary>
        /// Get a list of installed voices.
        /// </summary>
        [HttpGet]
        [Route("installed/list")]
        public async Task<List<InstalledVoice>> InstalledVoicesAsync()
        {
            var task = new TaskFactory().StartNew(() =>
            {
                return LocalSynthesizer.GetInstalledVoices().ToList();
            });
            var list = await task;
            return list;
        }

    }
}
