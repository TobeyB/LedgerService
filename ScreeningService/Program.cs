using System.Text.Json.Serialization;
using ScreeningService.Options;
using ScreeningService.Services;

namespace ScreeningService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.Configure<ScreeningOptions>(builder.Configuration.GetSection(ScreeningOptions.SectionName));
        builder.Services.AddSingleton<DecisionCache>();

        var app = builder.Build();

        app.MapControllers();

        app.Run();
    }
}
