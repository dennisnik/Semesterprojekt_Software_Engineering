using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Serilog;
using TNWatch.Alerting;
using TNWatch.Persistence;
using TNWatch.TelemetryIngestion;
using TNWatch.WebUI.Api;
using TNWatch.WebUI.Auth;
using TNWatch.WebUI.Components;
using TNWatch.WebUI.Realtime;
using TNWatch.WebUI.State;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

var connectionString = builder.Configuration.GetConnectionString("TnWatch")
    ?? throw new InvalidOperationException("ConnectionStrings:TnWatch is missing.");

// Modules (ADR-01: modular monolith, composed here)
builder.Services
    .AddTnWatchPersistence(connectionString)
    .AddTnWatchAlerting(builder.Configuration)
    .AddTnWatchIngestion(builder.Configuration)
    .AddTnWatchAuth(builder.Configuration, builder.Environment);

// Web UI + realtime (ADR-02, ADR-05)
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddScoped<FleetViewModel>();
builder.Services.Configure<TNWatch.WebUI.Ui.MapOptions>(builder.Configuration.GetSection(TNWatch.WebUI.Ui.MapOptions.SectionName));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHostedService<HubBroadcaster>();
builder.Services.AddDataProtection().SetApplicationName("TNWatch").PersistKeysToDbContext<TnWatchDbContext>();
builder.Services.AddHealthChecks().AddDbContextCheck<TnWatchDbContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var german = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = german;
CultureInfo.DefaultThreadCurrentUICulture = german;

var app = builder.Build();

await MigrateWithRetryAsync(app);

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
    ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error : Serilog.Events.LogEventLevel.Debug);
app.UseRequestLocalization(new RequestLocalizationOptions { DefaultRequestCulture = new RequestCulture(german) });
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAccountEndpoints();
app.MapFleetApi();
app.MapHub<TelemetryHub>(TelemetryHub.Path);
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();

static async Task MigrateWithRetryAsync(WebApplication app)
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await app.Services.MigrateTnWatchDatabaseAsync();
            return;
        }
        catch (Exception ex) when (attempt < 10)
        {
            app.Logger.LogWarning("Database not ready (attempt {Attempt}/10): {Message}", attempt, ex.GetBaseException().Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

/// <summary>Entry point (public for WebApplicationFactory in integration tests).</summary>
public partial class Program;
