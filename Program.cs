using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Newtonsoft.Json;
using FBRExtension.Extensions;
using FBRExtension.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        options.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add simple HttpClient for API calls
builder.Services.AddHttpClient("PralFbrClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
});

// Add default HttpClient for other uses
builder.Services.AddHttpClient();

// Save log 
string? homeDir = Environment.GetEnvironmentVariable("HOME");
string logDirectory = homeDir != null
            ? Path.Combine(homeDir, "LogFiles", "MyApp") // Azure
            : Path.Combine(AppContext.BaseDirectory, "Logs"); // Local

if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

// Konfigurasi logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new FileLoggerProvider(logDirectory, LogLevel.Information));

// Simplified configuration without complex service dependencies

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Use global exception handling in production
    app.UseGlobalExceptionHandling();
}

// Use request localization
var defaultCulture = new CultureInfo("en-US");
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
    SupportedCultures = new List<CultureInfo> { defaultCulture },
    SupportedUICultures = new List<CultureInfo> { defaultCulture }
};
app.UseRequestLocalization(localizationOptions);

app.UseRouting();
app.UseCors("AllowAll");


// Force HTTPS redirection for all routes
app.UseHttpsRedirection();

app.UseStaticFiles();
app.MapControllers();

// Serve index.html as default for non-API routes
app.MapFallbackToFile("index.html").Add(endpointBuilder =>
{
    endpointBuilder.Metadata.Add(new HttpMethodMetadata(new[] { "GET" }));
});

app.Run();