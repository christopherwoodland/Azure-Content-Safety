var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register settings
builder.Services.AddSingleton(_ => FunctionSettings.FromEnvironment());

// Register services
builder.Services.AddTransient<IAzureSQLHelper, AzureSQLHelper>();
builder.Services.AddScoped<IContentSafetyHelper, ContentSafetyHelper>();
builder.Services.AddScoped<IStorageHelper, StorageHelper>();
builder.Services.AddScoped<ICsvExporter, CsvExporter>();
builder.Services.AddScoped<IResultExporter, ResultExporter>();
builder.Services.AddTransient<IVideoHelper, VideoHelper>();
builder.Services.AddScoped<HttpClient>();

builder.Build().Run();
