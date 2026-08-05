var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton<IContentSafetyHelper, ContentSafetyHelper>();
builder.Services.AddSingleton<IStorageHelper, StorageHelper>();
builder.Services.AddScoped<ICsvExporter, CsvExporter>();
builder.Services.AddScoped<IVideoHelper, VideoHelper>();
builder.Services.AddHttpClient();

builder.Build().Run();
