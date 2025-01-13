var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register services
builder.Services.AddApplicationInsightsTelemetryWorkerService(options =>
{
	options.ConnectionString = Environment.GetEnvironmentVariable("APP_INSIGHTS_CONNECTION_STRING");
}).ConfigureFunctionsApplicationInsights();

builder.Services.AddTransient<IAzureSQLHelper, AzureSQLHelper>();
builder.Services.AddScoped<ILogHelper, LogHelper>();
builder.Services.AddScoped<IContentSafetyHelper, ContentSafetyHelper>();
builder.Services.AddScoped<IStorageHelper, StorageHelper>();
builder.Services.AddScoped<ICsvExporter, CsvExporter>();
builder.Services.AddTransient<IVideoHelper, VideoHelper>();
builder.Services.AddScoped<HttpClient>();

builder.Build().Run();
