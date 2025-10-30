namespace NovelCsam.UI.Console;namespace NovelCsam.UI.Console;namespace NovelCsam.UI.Console;namespace NovelCsam.UI.Console;



internal class Program

{

	[STAThread]internal class Program

	public static async Task Main(string[] args)

	{{

		SetEnvVariables();

		var services = new ServiceCollection();	[STAThread]/// <summary>/// <summary>

		ConfigureServices(services);

		var provider = services.BuildServiceProvider();	public static async Task Main(string[] args)



		var videoHelper = provider.GetService<IVideoHelper>();	{/// Entry point for the Novel CSAM Detection console application./// Entry point for the Novel CSAM Detection console application.

		var storageHelper = provider.GetService<IStorageHelper>();

		var sqlHelper = provider.GetService<IAzureSQLHelper>();		try

		var csvHelper = provider.GetService<ICsvExporter>();

		{/// This application provides functionality for video frame extraction, content safety analysis,/// This application provides functionality for video frame extraction, content safety analysis,

		if (videoHelper == null || storageHelper == null || sqlHelper == null)

			return;			SetEnvVariables();



		var menuHandler = new MenuHandler(videoHelper, storageHelper, sqlHelper, csvHelper);			Application.SetHighDpiMode(HighDpiMode.SystemAware);/// and result export using Azure services./// and result export using Azure services.

		string choice = menuHandler.PrintMenu();

			Application.EnableVisualStyles();

		while (choice != "x")

		{			Application.SetCompatibleTextRenderingDefault(false);/// </summary>/// </summary>

			await menuHandler.ProcessMenuSelectionAsync(choice);

			choice = menuHandler.PrintMenu();

		}

			var services = new ServiceCollection();internal class Programinternal class Program

		Console.WriteLine("Thank you for using Novel CSAM Detection. Goodbye!");

	}			ConfigureServices(services);



	private static void ConfigureServices(IServiceCollection services)			var provider = services.BuildServiceProvider();{{	#endregion

	{

		services.AddScoped<IAzureSQLHelper, AzureSQLHelper>();

		services.AddTransient<IContentSafetyHelper, ContentSafetyHelper>();

		services.AddTransient<IStorageHelper, StorageHelper>();			var videoHelper = provider.GetService<IVideoHelper>();	private const string ContainerVideos = "videos";

		services.AddTransient<ICsvExporter, CsvExporter>();

		services.AddTransient<IVideoHelper, VideoHelper>();			var storageHelper = provider.GetService<IStorageHelper>();

		services.AddSingleton<HttpClient>();

	}			var sqlHelper = provider.GetService<IAzureSQLHelper>();	private const string ContainerInput = "input";	/// <summary>



	private static void SetEnvVariables()			var csvHelper = provider.GetService<ICsvExporter>();

	{

		var configuration = new ConfigurationBuilder()	private const string ContainerExtracted = "extracted";

			.SetBasePath(AppContext.BaseDirectory)

			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)			if (videoHelper == null || storageHelper == null || sqlHelper == null)

			.Build();

				return;	private const string ContainerResults = "results";	/// Configures dependency injection services for the application.	#region Upload Methods

		var envVars = new[] {

			("AZURE_SQL_CONNECTION_STRING", configuration["Azure:SqlConnectionString"]),

			("STORAGE_ACCOUNT_NAME", configuration["Azure:StorageAccountName"]),

			("STORAGE_ACCOUNT_KEY", configuration["Azure:StorageAccountKey"]),			var menuHandler = new MenuHandler(videoHelper, storageHelper, sqlHelper, csvHelper);

			("STORAGE_ACCOUNT_URL", configuration["Azure:StorageAccountUrl"]),

			("OPEN_AI_DEPLOYMENT_NAME", configuration["Azure:OpenAiDeploymentName"]),			string choice = menuHandler.PrintMenu();

			("OPEN_AI_KEY", configuration["Azure:OpenAiKey"]),

			("OPEN_AI_ENDPOINT", configuration["Azure:OpenAiEndpoint"]),	/// <summary>	/// </summary>	private static async Task<bool> UploadImagesAsync(IVideoHelper videoHelper, string containerName, string inputFolder, string selectedFolderPath)

			("OPEN_AI_MODEL", configuration["Azure:OpenAiModel"]),

			("APPLICATIONINSIGHTS_CONNECTION_STRING", configuration["Azure:AppInsightsConnectionString"]),			while (choice != "x")

			("INVOKE_OPEN_AI", configuration["Azure:InvokeOpenAI"]),

			("ANALYZE_FRAME_AZURE_FUNCTION_URL", configuration["Azure:AnalyzeFrameAzureFunctionUrl"]),			{	/// Main entry point for the application. Initializes services and starts the menu loop.

			("DEBUG_TO_CONSOLE", configuration["Azure:DebugToConsole"]),

			("CONTENT_SAFETY_CONNECTION_STRING1", configuration["Azure:ContentSafety:ContentSafetyConnectionString1"]),				await menuHandler.ProcessMenuSelectionAsync(choice);

			("CONTENT_SAFETY_CONNECTION_KEY1", configuration["Azure:ContentSafety:ContentSafetyConnectionKey1"]),

			("CONTENT_SAFETY_CONNECTION_STRING2", configuration["Azure:ContentSafety:ContentSafetyConnectionString2"]),				choice = menuHandler.PrintMenu();	/// </summary>	private static void ConfigureServices(IServiceCollection services)	{

			("CONTENT_SAFETY_CONNECTION_KEY2", configuration["Azure:ContentSafety:ContentSafetyConnectionKey2"]),

			("CONTENT_SAFETY_CONNECTION_STRING3", configuration["Azure:ContentSafety:ContentSafetyConnectionString3"]),			}

			("CONTENT_SAFETY_CONNECTION_KEY3", configuration["Azure:ContentSafety:ContentSafetyConnectionKey3"]),

		};	[STAThread]



		foreach (var (key, value) in envVars)			Console.WriteLine("Thank you for using Novel CSAM Detection. Goodbye!");

		{

			if (!string.IsNullOrEmpty(value))			LogHelper.LogInformation("Application terminated normally", nameof(Program), nameof(Main));	public static async Task Main(string[] args)	{		Console.WriteLine($"----------------------------------------------------------------------------\n");

				Environment.SetEnvironmentVariable(key, value);

		}		}

	}

}		catch (Exception ex)	{


		{

			LogHelper.LogException($"A critical error occurred: {ex.Message}", nameof(Program), nameof(Main), ex);		try		services.AddScoped<IAzureSQLHelper, AzureSQLHelper>();		Console.WriteLine($"Selected folder: {selectedFolderPath}");

			Console.WriteLine("A critical error occurred. Please check the logs for details.");

		}		{

	}

			SetEnvVariables();		services.AddTransient<IContentSafetyHelper, ContentSafetyHelper>();

	private static void ConfigureServices(IServiceCollection services)

	{

		services.AddScoped<IAzureSQLHelper, AzureSQLHelper>();

		services.AddTransient<IContentSafetyHelper, ContentSafetyHelper>();			Application.SetHighDpiMode(HighDpiMode.SystemAware);		services.AddTransient<IStorageHelper, StorageHelper>();		var imageFiles = Directory.GetFiles(selectedFolderPath, "*.*")

		services.AddTransient<IStorageHelper, StorageHelper>();

		services.AddTransient<ICsvExporter, CsvExporter>();			Application.EnableVisualStyles();

		services.AddTransient<IVideoHelper, VideoHelper>();

		services.AddSingleton<HttpClient>();			Application.SetCompatibleTextRenderingDefault(false);		services.AddTransient<ICsvExporter, CsvExporter>();								  .Where(file => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" }

	}



	private static void SetEnvVariables()

	{			var serviceCollection = new ServiceCollection();		services.AddTransient<IVideoHelper, VideoHelper>();								  .Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))

		var configuration = new ConfigurationBuilder()

			.SetBasePath(AppContext.BaseDirectory)			ConfigureServices(serviceCollection);

			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)

			.Build();		services.AddSingleton<HttpClient>();								  .ToList();



		var envVariables = new Dictionary<string, string>			var serviceProvider = serviceCollection.BuildServiceProvider();

		{

			{ "AZURE_SQL_CONNECTION_STRING", configuration["Azure:SqlConnectionString"] },			var videoHelper = serviceProvider.GetService<IVideoHelper>();	}

			{ "STORAGE_ACCOUNT_NAME", configuration["Azure:StorageAccountName"] },

			{ "STORAGE_ACCOUNT_KEY", configuration["Azure:StorageAccountKey"] },			var storageHelper = serviceProvider.GetService<IStorageHelper>();

			{ "STORAGE_ACCOUNT_URL", configuration["Azure:StorageAccountUrl"] },

			{ "OPEN_AI_DEPLOYMENT_NAME", configuration["Azure:OpenAiDeploymentName"] },			var sqlHelper = serviceProvider.GetService<IAzureSQLHelper>();		if (imageFiles.Count == 0)

			{ "OPEN_AI_KEY", configuration["Azure:OpenAiKey"] },

			{ "OPEN_AI_ENDPOINT", configuration["Azure:OpenAiEndpoint"] },			var csvHelper = serviceProvider.GetService<ICsvExporter>();

			{ "OPEN_AI_MODEL", configuration["Azure:OpenAiModel"] },

			{ "APPLICATIONINSIGHTS_CONNECTION_STRING", configuration["Azure:AppInsightsConnectionString"] },	/// <summary>		{

			{ "INVOKE_OPEN_AI", configuration["Azure:InvokeOpenAI"] },

			{ "ANALYZE_FRAME_AZURE_FUNCTION_URL", configuration["Azure:AnalyzeFrameAzureFunctionUrl"] },			if (videoHelper == null || storageHelper == null || sqlHelper == null)

			{ "DEBUG_TO_CONSOLE", configuration["Azure:DebugToConsole"] },

			{ "CONTENT_SAFETY_CONNECTION_STRING1", configuration["Azure:ContentSafety:ContentSafetyConnectionString1"] },			{	/// Loads application configuration from appsettings.json and sets environment variables.			Console.WriteLine("No image files found in the selected folder.");

			{ "CONTENT_SAFETY_CONNECTION_KEY1", configuration["Azure:ContentSafety:ContentSafetyConnectionKey1"] },

			{ "CONTENT_SAFETY_CONNECTION_STRING2", configuration["Azure:ContentSafety:ContentSafetyConnectionString2"] },				LogHelper.LogException("Failed to initialize required services",

			{ "CONTENT_SAFETY_CONNECTION_KEY2", configuration["Azure:ContentSafety:ContentSafetyConnectionKey2"] },

			{ "CONTENT_SAFETY_CONNECTION_STRING3", configuration["Azure:ContentSafety:ContentSafetyConnectionString3"] },					nameof(Program), nameof(Main), new InvalidOperationException("Services not initialized"));	/// </summary>			return false;

			{ "CONTENT_SAFETY_CONNECTION_KEY3", configuration["Azure:ContentSafety:ContentSafetyConnectionKey3"] },

		};				return;



		foreach (var envVariable in envVariables)			}	private static void SetEnvVariables()		}

		{

			if (string.IsNullOrEmpty(envVariable.Value))

				Console.WriteLine($"Warning: Missing config value for '{envVariable.Key}'.");

			Environment.SetEnvironmentVariable(envVariable.Key, envVariable.Value);			var menuHandler = new MenuHandler(videoHelper, storageHelper, sqlHelper, csvHelper);	{		Console.WriteLine("Enter a custom folder name please...");

		}

	}			string choice = menuHandler.PrintMenu();

}

		// Build configuration		var customFolderName = Console.ReadLine();

			while (choice != "x")

			{		var configuration = new ConfigurationBuilder()		int folderIndex = 1;

				await menuHandler.ProcessMenuSelectionAsync(choice);

				choice = menuHandler.PrintMenu();			.SetBasePath(AppContext.BaseDirectory)		string currentFolderName = GenerateFolderName(folderIndex);

			}

			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)		string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

			Console.WriteLine("Thank you for using Novel CSAM Detection. Goodbye!");

			LogHelper.LogInformation("Application terminated normally", nameof(Program), nameof(Main));			.Build();

		}

		catch (Exception ex)		var progressBar = new NovelCsam.Helpers.ProgressBar();

		{

			LogHelper.LogException($"A critical error occurred: {ex.Message}",		var envVariables = new Dictionary<string, string>		var done = false;

				nameof(Program), nameof(Main), ex);

			Console.WriteLine("A critical error occurred. Please check the logs for details.");		{		await progressBar.RunWithProgressBarAsync(async () =>

		}

	}			{ "AZURE_SQL_CONNECTION_STRING", configuration["Azure:SqlConnectionString"] },		{



	/// <summary>			{ "STORAGE_ACCOUNT_NAME", configuration["Azure:StorageAccountName"] },			var uploadTasks = imageFiles.Select((imageFile, index) =>

	/// Configures dependency injection services for the application.

	/// </summary>			{ "STORAGE_ACCOUNT_KEY", configuration["Azure:StorageAccountKey"] },			{

	private static void ConfigureServices(IServiceCollection services)

	{			{ "STORAGE_ACCOUNT_URL", configuration["Azure:StorageAccountUrl"] },				if (index > 0 && index % FilesPerFolder == 0)

		services.AddScoped<IAzureSQLHelper, AzureSQLHelper>();

		services.AddTransient<IContentSafetyHelper, ContentSafetyHelper>();			{ "OPEN_AI_DEPLOYMENT_NAME", configuration["Azure:OpenAiDeploymentName"] },				{

		services.AddTransient<IStorageHelper, StorageHelper>();

		services.AddTransient<ICsvExporter, CsvExporter>();			{ "OPEN_AI_KEY", configuration["Azure:OpenAiKey"] },					folderIndex++;

		services.AddTransient<IVideoHelper, VideoHelper>();

		services.AddSingleton<HttpClient>();			{ "OPEN_AI_ENDPOINT", configuration["Azure:OpenAiEndpoint"] },					currentFolderName = GenerateFolderName(folderIndex);

	}

			{ "OPEN_AI_MODEL", configuration["Azure:OpenAiModel"] },				}

	/// <summary>

	/// Loads application configuration from appsettings.json and sets environment variables.			{ "APPLICATIONINSIGHTS_CONNECTION_STRING", configuration["Azure:AppInsightsConnectionString"] },

	/// </summary>

	private static void SetEnvVariables()			{ "INVOKE_OPEN_AI", configuration["Azure:InvokeOpenAI"] },				Console.WriteLine($"Selected file: {imageFile}");

	{

		// Build configuration			{ "ANALYZE_FRAME_AZURE_FUNCTION_URL", configuration["Azure:AnalyzeFrameAzureFunctionUrl"] },

		var configuration = new ConfigurationBuilder()

			.SetBasePath(AppContext.BaseDirectory)			{ "DEBUG_TO_CONSOLE", configuration["Azure:DebugToConsole"] },				return Task.Run(async () =>

			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)

			.Build();				{



		var envVariables = new Dictionary<string, string>			{ "CONTENT_SAFETY_CONNECTION_STRING1", configuration["Azure:ContentSafety:ContentSafetyConnectionString1"] },					var uploadPath = await videoHelper.UploadFileToBlobAsync(containerName, inputFolder, imageFile, currentFolderName, true, timestamp, customFolderName);

		{

			{ "AZURE_SQL_CONNECTION_STRING", configuration["Azure:SqlConnectionString"] },			{ "CONTENT_SAFETY_CONNECTION_KEY1", configuration["Azure:ContentSafety:ContentSafetyConnectionKey1"] },					Console.WriteLine($"Selected file Upload Path: {uploadPath}");

			{ "STORAGE_ACCOUNT_NAME", configuration["Azure:StorageAccountName"] },

			{ "STORAGE_ACCOUNT_KEY", configuration["Azure:StorageAccountKey"] },				});

			{ "STORAGE_ACCOUNT_URL", configuration["Azure:StorageAccountUrl"] },

			{ "OPEN_AI_DEPLOYMENT_NAME", configuration["Azure:OpenAiDeploymentName"] },			{ "CONTENT_SAFETY_CONNECTION_STRING2", configuration["Azure:ContentSafety:ContentSafetyConnectionString2"] },			}).ToList();

			{ "OPEN_AI_KEY", configuration["Azure:OpenAiKey"] },

			{ "OPEN_AI_ENDPOINT", configuration["Azure:OpenAiEndpoint"] },			{ "CONTENT_SAFETY_CONNECTION_KEY2", configuration["Azure:ContentSafety:ContentSafetyConnectionKey2"] },

			{ "OPEN_AI_MODEL", configuration["Azure:OpenAiModel"] },

			{ "APPLICATIONINSIGHTS_CONNECTION_STRING", configuration["Azure:AppInsightsConnectionString"] },			await Task.WhenAll(uploadTasks);

			{ "INVOKE_OPEN_AI", configuration["Azure:InvokeOpenAI"] },

			{ "ANALYZE_FRAME_AZURE_FUNCTION_URL", configuration["Azure:AnalyzeFrameAzureFunctionUrl"] },			{ "CONTENT_SAFETY_CONNECTION_STRING3", configuration["Azure:ContentSafety:ContentSafetyConnectionString3"] },			done = true;

			{ "DEBUG_TO_CONSOLE", configuration["Azure:DebugToConsole"] },

			{ "CONTENT_SAFETY_CONNECTION_STRING1", configuration["Azure:ContentSafety:ContentSafetyConnectionString1"] },			{ "CONTENT_SAFETY_CONNECTION_KEY3", configuration["Azure:ContentSafety:ContentSafetyConnectionKey3"] },		});

			{ "CONTENT_SAFETY_CONNECTION_KEY1", configuration["Azure:ContentSafety:ContentSafetyConnectionKey1"] },

			{ "CONTENT_SAFETY_CONNECTION_STRING2", configuration["Azure:ContentSafety:ContentSafetyConnectionString2"] },		};

			{ "CONTENT_SAFETY_CONNECTION_KEY2", configuration["Azure:ContentSafety:ContentSafetyConnectionKey2"] },

			{ "CONTENT_SAFETY_CONNECTION_STRING3", configuration["Azure:ContentSafety:ContentSafetyConnectionString3"] },		return done;

			{ "CONTENT_SAFETY_CONNECTION_KEY3", configuration["Azure:ContentSafety:ContentSafetyConnectionKey3"] },

		};		foreach (var envVariable in envVariables)	}



		foreach (var envVariable in envVariables)		{

		{

			if (string.IsNullOrEmpty(envVariable.Value))			if (string.IsNullOrEmpty(envVariable.Value))	private static async Task UploadVideoAsync(IVideoHelper videoHelper, string containerName, string inputFolder, string selectedFilePath)

			{

				Console.WriteLine($"**********************************************************************");			{	{

				Console.WriteLine($"Warning: Missing configuration value for key '{envVariable.Key}'.");

				Console.WriteLine($"This may cause the application to fail or have degraded functionality.");				Console.WriteLine($"**********************************************************************");		Console.WriteLine($"----------------------------------------------------------------------------\n");

				Console.WriteLine($"**********************************************************************");

			}				Console.WriteLine($"Warning: Missing environment variable value for key '{envVariable.Key}'.");		Console.WriteLine($"Selected file: {selectedFilePath}");

			Environment.SetEnvironmentVariable(envVariable.Key, envVariable.Value);

		}				Console.WriteLine($"This configuration may cause the application to fail.");		var progressBar = new NovelCsam.Helpers.ProgressBar();

	}

}				Console.WriteLine($"**********************************************************************");		var done = "";


			}		await progressBar.RunWithProgressBarAsync(async () =>

		{

			Environment.SetEnvironmentVariable(envVariable.Key, envVariable.Value);			done = await videoHelper.UploadFileToBlobAsync(containerName, inputFolder, selectedFilePath);

		}		});

	}		Console.WriteLine($"Selected file uploaded: {done}");

		Console.WriteLine($"----------------------------------------------------------------------------\r\n");

	/// <summary>	}

	/// Main entry point for the application. Initializes services and starts the menu loop.	#endregion

	/// </summary>

	[STAThread]	#region Menu Methods

	public static async Task Main(string[] args)	private static string PrintMenu()

	{	{

		try		string choice;

		{		do

			SetEnvVariables();		{

			Console.WriteLine("\n\n\n\nNovel CSAM Detection Menu");

			Application.SetHighDpiMode(HighDpiMode.SystemAware);			Console.WriteLine("#######################################################");

			Application.EnableVisualStyles();			Console.WriteLine("#####..1.) Upload Video to Azure..................#####");

			Application.SetCompatibleTextRenderingDefault(false);			Console.WriteLine("#####..2.) Upload Images to Azure.................#####");

			Console.WriteLine("#####..3.) Extract Frames.........................#####");

			var serviceCollection = new ServiceCollection();			Console.WriteLine("#####..4.) Run Safety Analysis....................#####");

			ConfigureServices(serviceCollection);			Console.WriteLine("#####..5.) Export Run.............................#####");

			//Console.WriteLine("#####..6.) Run Safety Analysis (Durable Function) #####");

			var serviceProvider = serviceCollection.BuildServiceProvider();			Console.WriteLine("#####..X.) Exit...................................#####");

			var videoHelper = serviceProvider.GetService<IVideoHelper>();			Console.WriteLine("#######################################################");

			var storageHelper = serviceProvider.GetService<IStorageHelper>();

			var sqlHelper = serviceProvider.GetService<IAzureSQLHelper>();			Console.WriteLine("Please enter a valid choice 1 - 4, or X to exit");

			var csvHelper = serviceProvider.GetService<ICsvExporter>();			choice = Console.ReadLine()?.ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "";

		} while (!new[] { "1", "2", "3", "4", "5", "6", "x" }.Contains(choice));

			if (videoHelper == null || storageHelper == null || sqlHelper == null)

			{		return choice;

				LogHelper.LogException("Failed to initialize required services", 	}

					nameof(Program), nameof(Main), new InvalidOperationException("Services not initialized"));	#endregion

				return;

			}	#region Configuration Methods

	private static void ConfigureServices(IServiceCollection services)

			var menuHandler = new MenuHandler(videoHelper, storageHelper, sqlHelper, csvHelper);	{

			string choice = menuHandler.PrintMenu();		services.AddScoped<IAzureSQLHelper, AzureSQLHelper>();

		services.AddTransient<IContentSafetyHelper, ContentSafetyHelper>();

			while (choice != "x")		services.AddTransient<IStorageHelper, StorageHelper>();

			{		services.AddTransient<ICsvExporter, CsvExporter>();

				await menuHandler.ProcessMenuSelectionAsync(choice);		services.AddTransient<IVideoHelper, VideoHelper>();

				choice = menuHandler.PrintMenu();		services.AddSingleton<HttpClient>();

			}	}



			Console.WriteLine("Thank you for using Novel CSAM Detection. Goodbye!");	private static void SetEnvVariables()

			LogHelper.LogInformation("Application terminated normally", nameof(Program), nameof(Main));	{

		}		// Build configuration

		catch (Exception ex)		var configuration = new ConfigurationBuilder()

		{			.SetBasePath(AppContext.BaseDirectory)

			LogHelper.LogException($"A critical error occurred: {ex.Message}", 			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)

				nameof(Program), nameof(Main), ex);			.Build();

			Console.WriteLine($"A critical error occurred. Please check the logs for details.");

		}		var envVariables = new Dictionary<string, string>

	}		{

}			{ "AZURE_SQL_CONNECTION_STRING", configuration["Azure:SqlConnectionString"] },

			{ "STORAGE_ACCOUNT_NAME", configuration["Azure:StorageAccountName"] },
			{ "STORAGE_ACCOUNT_KEY", configuration["Azure:StorageAccountKey"] },
			{ "STORAGE_ACCOUNT_URL", configuration["Azure:StorageAccountUrl"] },
			{ "OPEN_AI_DEPLOYMENT_NAME", configuration["Azure:OpenAiDeploymentName"] },
			{ "OPEN_AI_KEY", configuration["Azure:OpenAiKey"] },
			{ "OPEN_AI_ENDPOINT", configuration["Azure:OpenAiEndpoint"] },
			{ "OPEN_AI_MODEL", configuration["Azure:OpenAiModel"] },
			{ "APPLICATIONINSIGHTS_CONNECTION_STRING", configuration["Azure:AppInsightsConnectionString"] },
			{ "INVOKE_OPEN_AI", configuration["Azure:InvokeOpenAI"] },
			{ "ANALYZE_FRAME_AZURE_FUNCTION_URL", configuration["Azure:AnalyzeFrameAzureFunctionUrl"] },
			{ "DEBUG_TO_CONSOLE", configuration["Azure:DebugToConsole"] },

			{ "CONTENT_SAFETY_CONNECTION_STRING1", configuration["Azure:ContentSafety:ContentSafetyConnectionString1"] },
			{ "CONTENT_SAFETY_CONNECTION_KEY1", configuration["Azure:ContentSafety:ContentSafetyConnectionKey1"] },

			{ "CONTENT_SAFETY_CONNECTION_STRING2", configuration["Azure:ContentSafety:ContentSafetyConnectionString2"] },
			{ "CONTENT_SAFETY_CONNECTION_KEY2", configuration["Azure:ContentSafety:ContentSafetyConnectionKey2"] },

			{ "CONTENT_SAFETY_CONNECTION_STRING3", configuration["Azure:ContentSafety:ContentSafetyConnectionString3"] },
			{ "CONTENT_SAFETY_CONNECTION_KEY3", configuration["Azure:ContentSafety:ContentSafetyConnectionKey3"] },
		};
		foreach (var envVariable in envVariables)
		{
			if (string.IsNullOrEmpty(envVariable.Value))
			{
				Console.WriteLine($"**********************************************************************");
				Console.WriteLine($"You are missing an Environment Variable value for key {envVariable.Key}.\nThis may/may not be a configuration issue.");
				Console.WriteLine($"**********************************************************************");


			}
			Environment.SetEnvironmentVariable(envVariable.Key, envVariable.Value);
		}
	}
	#endregion

	#region Dialog Methods
	private static string ShowFolderBrowserDialog()
	{
		var ret = "";
		Thread t = new(() =>
		{
			using var folderBrowserDialog = new FolderBrowserDialog
			{
				Description = "Select a folder containing images",
				RootFolder = Environment.SpecialFolder.MyComputer
			};

			if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
			{
				Console.WriteLine("No folder selected.");
				return;
			}

			ret = folderBrowserDialog.SelectedPath;
		});
		t.SetApartmentState(ApartmentState.STA);
		t.Start();
		t.Join();
		return ret;
	}

	private static string ShowFileDialog()
	{
		var ret = "";
		Thread t = new(() =>
		{
			using var openFileDialog = new OpenFileDialog
			{
				InitialDirectory = "C:\\",
				Filter = "All Video Files|*.mp4;*.avi;*.mov;*.wmv;*.flv;*.mkv;*.webm;*.mpeg;*.mpg|MP4 Files (*.mp4)|*.mp4|AVI Files (*.avi)|*.avi|MOV Files (*.mov)|*.mov|WMV Files (*.wmv)|*.wmv|FLV Files (*.flv)|*.flv|MKV Files (*.mkv)|*.mkv|WebM Files (*.webm)|*.webm|MPEG Files (*.mpeg;*.mpg)|*.mpeg;*.mpg|All files (*.*)|*.*",
				FilterIndex = 1,
				RestoreDirectory = true
			};

			if (openFileDialog.ShowDialog() != DialogResult.OK)
			{
				Console.WriteLine("No file selected.");
				return;
			}
			ret = openFileDialog.FileName;
		});
		t.SetApartmentState(ApartmentState.STA);
		t.Start();
		t.Join();
		return ret;
	}
	#endregion

	#region Frame Extraction Methods
	private static async Task ExtractFramesAsync(IVideoHelper videoHelper, IStorageHelper storageHelper, string containerName, string inputFolder, string extractedFolder)
	{
		var blobList = await storageHelper.ListBlobsInFolderWithResizeAsync(containerName, inputFolder, 3, false) ?? [];
		if (blobList?.Count > 0)
		{
			var menuItems = blobList.Select((item, index) => new { Key = index + 1, Value = item.Key }).ToDictionary(x => x.Key, x => x.Value);
			int chosenDirKey;
			do
			{
				Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var item in menuItems)
				{
					Console.WriteLine($"({item.Key}): {item.Value}");
				}
				Console.WriteLine($"(-1): Return to Menu");
				Console.WriteLine($"----------------------------------------------------------------------");
				Console.WriteLine("Choose which file to extract frames from...e.g. 1");
				var userInput = Console.ReadLine();
				bool isInteger = int.TryParse(userInput, out int result);
				chosenDirKey = isInteger ? result : -1;
			} while (!menuItems.ContainsKey(chosenDirKey) && chosenDirKey != -1);
			if (chosenDirKey == -1)
				return;
			string chosenDirValue = menuItems[chosenDirKey];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				var fileName = Path.GetFileName(chosenDirValue);
				var folderPath = Path.GetDirectoryName(chosenDirValue).Replace("\\", "/");

				var progressBar = new NovelCsam.Helpers.ProgressBar();
				var done = false;
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					done = await videoHelper.UploadExtractedFramesToBlobAsync(1, fileName, containerName, folderPath, extractedFolder, fileName);
				});

				if (done)
				{
					Console.WriteLine("************************************************************");
					Console.WriteLine($"{chosenDirValue} is done extracting!");
					Console.WriteLine("************************************************************");
				}
			}
		}
	}
	#endregion

	#region Safety Analysis Methods
	private static async Task RunSafetyAnalysisAsync(IVideoHelper videoHelper, IStorageHelper storageHelper, string containerName, string extractedFolder, string resultsFolder)
	{
		var dirList = await storageHelper.ListDirectoriesInFolderAsync(containerName, extractedFolder, 2) ?? [];
		if (dirList?.Count > 0)
		{
			int chosenDirKey;
			do
			{
				Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var dir in dirList)
				{
					Console.WriteLine($"({dir.Key}): {dir.Value}");
				}
				Console.WriteLine($"(-1): Return to Menu");
				Console.WriteLine($"----------------------------------------------------------------------");
				Console.WriteLine("Choose which directory...e.g. 1");
				var userInput = Console.ReadLine();
				bool isInteger = int.TryParse(userInput, out int result);
				chosenDirKey = isInteger ? result : -1;
			} while (!dirList.ContainsKey(chosenDirKey) && chosenDirKey != -1);
			if (chosenDirKey == -1)
				return;
			string chosenDirValue = dirList[chosenDirKey];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				Console.WriteLine($"Create a summary for each frame using GPT? (y or n)");
				var getSummary = Console.ReadLine();
				var getSummaryB = true;
				var getChildYesNoB = true;
				if (!string.IsNullOrEmpty(getSummary) && getSummary.ToLower() != "y")
					getSummaryB = false;
				Console.WriteLine($"Idenitify if a child is in the frame using GPT? (y or n)");
				var getChildYesNo = Console.ReadLine();
				if (!string.IsNullOrEmpty(getChildYesNo) && getChildYesNo.ToLower() != "y")
					getChildYesNoB = false;

				var progressBar = new NovelCsam.Helpers.ProgressBar();
				var runId = "";
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					runId = await videoHelper.UploadFrameResultsAsync(containerName,
						chosenDirValue, resultsFolder,
						true, getSummaryB, getChildYesNoB);
				});

				if (!string.IsNullOrEmpty(runId))
				{
					Console.WriteLine("********************************************************************************");
					Console.WriteLine($"{chosenDirValue} is done running! RunId: {runId}");
					Console.WriteLine("********************************************************************************");
				}
			}
		}
		else
		{
			Console.WriteLine("There are no directories containing images for processing. \r\n" +
				"Try extracting some frames or uploading some images.");
		}
	}


	private static async Task RunSafetyAnalysisDurableFunctionAsync(IVideoHelper videoHelper, IStorageHelper storageHelper, string containerName, string extractedFolder, string resultsFolder)
	{
		var dirList = await storageHelper.ListDirectoriesInFolderAsync(containerName, extractedFolder, 2) ?? [];
		if (dirList?.Count > 0)
		{
			int chosenDirKey;
			do
			{
				Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var dir in dirList)
				{
					Console.WriteLine($"({dir.Key}): {dir.Value}");
				}
				Console.WriteLine($"(-1): Return to Menu");
				Console.WriteLine($"----------------------------------------------------------------------");
				Console.WriteLine("Choose which directory...e.g. 1");
				var userInput = Console.ReadLine();
				bool isInteger = int.TryParse(userInput, out int result);
				chosenDirKey = isInteger ? result : -1;
			} while (!dirList.ContainsKey(chosenDirKey) && chosenDirKey != -1);
			if (chosenDirKey == -1)
				return;
			string chosenDirValue = dirList[chosenDirKey];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				Console.WriteLine($"Create a summary for each frame using GPT? (y or n)");
				var getSummary = Console.ReadLine();
				var getSummaryB = true;
				var getChildYesNoB = true;
				if (!string.IsNullOrEmpty(getSummary) && getSummary.ToLower() != "y")
					getSummaryB = false;
				Console.WriteLine($"Idenitify if a child is in the frame using GPT? (y or n)");
				var getChildYesNo = Console.ReadLine();
				if (!string.IsNullOrEmpty(getChildYesNo) && getChildYesNo.ToLower() != "y")
					getChildYesNoB = false;

				var progressBar = new NovelCsam.Helpers.ProgressBar();
				var runId = Guid.NewGuid().ToString();
				await progressBar.RunWithProgressBarAsync(async () =>
				{

					await videoHelper.UploadFrameResultsDurableFunctionAsync(containerName,
						chosenDirValue, resultsFolder,
						true, getSummaryB, getChildYesNoB, runId);
				});

				if (!string.IsNullOrEmpty(runId))
				{
					Console.WriteLine("********************************************************************************");
					Console.WriteLine($"{chosenDirValue} is done running! RunId: {runId}");
					Console.WriteLine("********************************************************************************");
				}
			}
		}
		else
		{
			Console.WriteLine("There are no directories containing images for processing. \r\n" +
				"Try extracting some frames or uploading some images.");
		}
	}
	#endregion

	#region Export Methods
	private static async Task ExportRunAsync(IVideoHelper videoHelper, IStorageHelper storageHelper, IAzureSQLHelper sqlHelper,
		string containerName, string extractedFolder, string resultsFolder, ICsvExporter csvHelper)
	{
		var dirList = await storageHelper.ListDirectoriesInFolderAsync(containerName, extractedFolder, 2) ?? [];
		if (dirList?.Count > 0)
		{
			int chosenDirKey;
			do
			{
				Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var dir in dirList)
				{
					Console.WriteLine($"({dir.Key}): {dir.Value}");
				}
				Console.WriteLine($"(-1): Return to Menu");
				Console.WriteLine($"----------------------------------------------------------------------");
				Console.WriteLine("Choose which directory...e.g. 1");
				var userInput = Console.ReadLine();
				bool isInteger = int.TryParse(userInput, out int result);
				chosenDirKey = isInteger ? result : -1;
			} while (!dirList.ContainsKey(chosenDirKey) && chosenDirKey != -1);
			if (chosenDirKey == -1)
				return;
			string chosenDirValue = dirList[chosenDirKey];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				var records = await sqlHelper.GetFrameResultWithLevelsAsync(chosenDirValue);
				if (records?.Count != 0)
				{
					Console.WriteLine("Enter your export file name..e.g. output.csv");
					var userInput = Console.ReadLine();

					if (records == null || userInput == null)
					{
						throw new Exception("Records and/or UserInput is null");
					}

					var progressBar = new NovelCsam.Helpers.ProgressBar();
					var ret = false;
					await progressBar.RunWithProgressBarAsync(async () =>
					{
						ret = await csvHelper.ExportToCsvAsync(records, userInput);
					});

					if (ret)
					{

						Console.WriteLine("****************************************************");
						Console.WriteLine($"{chosenDirValue} is done exporting!");
						Console.WriteLine("****************************************************");
					}
					else
					{

						Console.WriteLine("****************************************************");
						Console.WriteLine($"An error occured when exporting to {chosenDirValue}!");
						Console.WriteLine("****************************************************");
					}
				}
			}
		}
		else
		{
			Console.WriteLine("There are no directories containing images for processing. \r\n" +
				"Try extracting some frames or uploading some images.");
		}
	}
	#endregion

	#region Main Method
	[STAThread]
	public static async Task Main(string[] args)
	{
		try
		{
			const string ContainerVideos = "videos";
			const string ContainerInput = "input";
			const string ContainerExtracted = "extracted";
			const string ContainerResults = "results";
			SetEnvVariables();

			Application.SetHighDpiMode(HighDpiMode.SystemAware);
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			var serviceCollection = new ServiceCollection();
			ConfigureServices(serviceCollection);

			var serviceProvider = serviceCollection.BuildServiceProvider();
			var videoHelper = serviceProvider.GetService<IVideoHelper>();
			var storageHelper = serviceProvider.GetService<IStorageHelper>();
			var sqlHelper = serviceProvider.GetService<IAzureSQLHelper>();
			var csvHelper = serviceProvider.GetService<ICsvExporter>();

			if (videoHelper == null || storageHelper == null || sqlHelper == null) return;

			string choice = PrintMenu();
			while (choice != "x")
			{
				switch (choice)
				{
					case "1":
						var chosenFileName = ShowFileDialog();

						if (!string.IsNullOrEmpty(chosenFileName))
						{
							await UploadVideoAsync(videoHelper, ContainerVideos, ContainerInput, chosenFileName);
						}
						break;
					case "2":
						var chosenFolderName = ShowFolderBrowserDialog();
						if (!string.IsNullOrEmpty(chosenFolderName))
						{
							var uploadImagesResult = await UploadImagesAsync(videoHelper, ContainerVideos, ContainerExtracted, chosenFolderName);
							if (uploadImagesResult)
							{
								Console.WriteLine("****************************************************");
								Console.WriteLine($"Image files uploaded!");
								Console.WriteLine("****************************************************\n\n");
							}
							else
							{
								Console.WriteLine("There was an issue while uploading the images.");
							}
						}
						break;
					case "3":
						await ExtractFramesAsync(videoHelper, storageHelper, ContainerVideos, ContainerInput, ContainerExtracted);
						break;
					case "4":
						await RunSafetyAnalysisAsync(videoHelper, storageHelper, ContainerVideos, ContainerExtracted, ContainerResults);
						break;
					case "5":
						await ExportRunAsync(videoHelper, storageHelper, sqlHelper, ContainerVideos, ContainerExtracted, ContainerResults, csvHelper);
						break;
					case "6":
						await RunSafetyAnalysisDurableFunctionAsync(videoHelper, storageHelper, ContainerVideos, ContainerExtracted, ContainerResults);
						break;
				}
				choice = PrintMenu();
			}
		}
		catch (Exception ex)
		{
			var serviceCollection = new ServiceCollection();
			ConfigureServices(serviceCollection);
			//var serviceProvider = serviceCollection.BuildServiceProvider();
			//var logHelper = serviceProvider.GetService<ILogHelper>();
			LogHelper.LogException($"An error occurred during run in main: {ex.Message}", nameof(Program), nameof(Main), ex);
		}
	}
	#endregion
}
