var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register services
builder.Services.AddSingleton<IAzureSQLHelper, AzureSQLHelper>();
builder.Services.AddSingleton<IContentSafetyHelper, ContentSafetyHelper>();
builder.Services.AddSingleton<IStorageHelper, StorageHelper>();
builder.Services.AddScoped<ICsvExporter, CsvExporter>();
builder.Services.AddScoped<IVideoHelper, VideoHelper>();
builder.Services.AddHttpClient();

builder.Build().Run();
