using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SynthNetVoice.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<VoiceDBContext>(opt =>
    opt.UseInMemoryDatabase("VoiceDB"));

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "SynthNetVoice",
        Description = "An ASP.NET Core Web API for Windows-only TTS. To perform text-to-speech, a speech synthesis engine that supports your language-country code must be installed.",
        TermsOfService = new Uri("https://www.youtube.com/@ImmanuelVanMeirhaeghe"),
        Contact = new OpenApiContact
        {
            Name = "[Dragon Legion]Immaanuel",
            Url = new Uri("https://www.youtube.com/@ImmanuelVanMeirhaeghe")
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.SerializeAsV2 = true;
    });
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();