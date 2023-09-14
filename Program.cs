using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SynthNetVoice.Data.Models;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // added this
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
}); ;

builder.Services.AddDbContext<VoiceDBContext>(opt =>
    opt.UseInMemoryDatabase("VoiceDB"));

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "SynthNetVoice, Fallout 4 NPC simulator",
        Description = "An ASP.NET Core Web API for Windows-only TTS. To perform text-to-speech, a speech synthesis engine that supports your language-country code must be installed.",
        TermsOfService = new Uri("https://www.youtube.com/@ImmanuelVanMeirhaeghe"),
        Contact = new OpenApiContact
        {
            Name = "[Dragon Legion]Immaanuel",
            Url = new Uri("https://www.youtube.com/@ImmanuelVanMeirhaeghe")
        }
    });
    //Project properties: Build > Output > Documentation file must be enabled
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
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

// For route configs:
/*
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "areaRoute",
    pattern: "{area:exists}/{controller=home}/{action=index}/{id?}");
    
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=home}/{action=index}/{id?}");
	
*/

app.Run();
