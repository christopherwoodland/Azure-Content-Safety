var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register services
builder.Services.AddTransient<IAzureSQLHelper, AzureSQLHelper>();
builder.Services.AddScoped<IContentSafetyHelper, ContentSafetyHelper>();
builder.Services.AddScoped<IStorageHelper, StorageHelper>();
builder.Services.AddScoped<ICsvExporter, CsvExporter>();
builder.Services.AddTransient<IVideoHelper, VideoHelper>();
builder.Services.AddScoped<HttpClient>();

builder.Build().Run();
