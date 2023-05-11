using Microsoft.EntityFrameworkCore;

namespace SynthNetVoice.Models
{
    public class VoiceDBContext : DbContext
    {

        public VoiceDBContext(DbContextOptions<VoiceDBContext> options)
        : base(options)
        {
            
        }

        public DbSet<GameInfo> GameInfo { get; set; } = null!;

    }
}
