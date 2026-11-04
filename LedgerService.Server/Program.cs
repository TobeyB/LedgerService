using LedgerService.Server.Data;
using LedgerService.Server.Services;
using LedgerService.Server.Services.Screening;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace LedgerService.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers()
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            builder.Services.AddScoped<ILedgerService, Services.LedgerService>();

            builder.Services.AddDbContext<AppDbContext>(o =>
                o.UseSqlServer(builder.Configuration.GetConnectionString("Ledger")));

            builder.Services.AddScoped<IAuditService, AuditService>();

            builder.Services.AddOptions<ScreeningClientOptions>()
                .Bind(builder.Configuration.GetSection(ScreeningClientOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var screeningOptions = builder.Configuration.GetSection(ScreeningClientOptions.SectionName)
                .Get<ScreeningClientOptions>() ?? new ScreeningClientOptions();

            builder.Services.AddHttpClient<IScreeningClient, ScreeningClient>((sp, http) =>
                    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<ScreeningClientOptions>>().Value.BaseUrl))
                .AddStandardResilienceHandler(o =>
                {
                    // A transfer waits for screening, so the wait is kept short. When time runs out the transfer is refused.
                    o.AttemptTimeout.Timeout = screeningOptions.AttemptTimeout;
                    o.TotalRequestTimeout.Timeout = screeningOptions.TotalRequestTimeout;
                    o.Retry.MaxRetryAttempts = screeningOptions.MaxRetryAttempts;
                    o.Retry.Delay = screeningOptions.RetryDelay;
                    o.Retry.ShouldRetryAfterHeader = true; // honour Retry-After on 429/503
                });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                await DbSeeder.SeedAsync(db);
            }

            app.UseDefaultFiles();
            app.MapStaticAssets();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.MapFallbackToFile("/index.html");

            await app.RunAsync();
        }
    }
}
