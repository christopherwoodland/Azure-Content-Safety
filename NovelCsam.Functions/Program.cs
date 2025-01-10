var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

//Set Env Variables
// Build configuration
var configuration = new ConfigurationBuilder()
	.SetBasePath(AppContext.BaseDirectory)
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.Build();

var envVariables = new Dictionary<string, string>
		{
			{ "AZURE_SQL_CONNECTION_STRING", configuration["Azure:SqlConnectionString"] },
			{ "CONTENT_SAFETY_CONNECTION_STRING", configuration["Azure:ContentSafetyConnectionString"] },
			{ "CONTENT_SAFETY_CONNECTION_KEY", configuration["Azure:ContentSafetyConnectionKey"] },
			{ "STORAGE_ACCOUNT_NAME", configuration["Azure:StorageAccountName"] },
			{ "STORAGE_ACCOUNT_KEY", configuration["Azure:StorageAccountKey"] },
			{ "STORAGE_ACCOUNT_URL", configuration["Azure:StorageAccountUrl"] },
			{ "OPEN_AI_DEPLOYMENT_NAME", configuration["Azure:OpenAiDeploymentName"] },
			{ "OPEN_AI_KEY", configuration["Azure:OpenAiKey"] },
			{ "OPEN_AI_ENDPOINT", configuration["Azure:OpenAiEndpoint"] },
			{ "OPEN_AI_MODEL", configuration["Azure:OpenAiModel"] },
			{ "APP_INSIGHTS_CONNECTION_STRING", configuration["Azure:AppInsightsConnectionString"] }
		};
foreach (var envVariable in envVariables)
{
	Environment.SetEnvironmentVariable(envVariable.Key, envVariable.Value);
}

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
