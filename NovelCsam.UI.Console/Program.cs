namespace NovelCsam.UI.Console;

internal class Program
{
	[STAThread]
	public static async Task Main(string[] args)
	{
		try
		{
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

			if (videoHelper == null || storageHelper == null || sqlHelper == null)
			{
				LogHelper.LogException("Failed to initialize required services",
					nameof(Program), nameof(Main), new InvalidOperationException("Services not initialized"));
				return;
			}

			string choice = PrintMenu();
			while (choice != "x")
			{
				switch (choice)
				{
					case "1":
						var chosenFileName = ShowFileDialog();
						if (!string.IsNullOrEmpty(chosenFileName))
						{
							await UploadVideoAsync(videoHelper, GetContainerName("CONTAINER_VIDEOS", "videos"), GetContainerName("CONTAINER_INPUT", "input"), chosenFileName);
						}
						break;

					case "2":
						var chosenFolderName = ShowFolderBrowserDialog();
						if (!string.IsNullOrEmpty(chosenFolderName))
						{
							var uploadImagesResult = await UploadImagesAsync(videoHelper, GetContainerName("CONTAINER_VIDEOS", "videos"), GetContainerName("CONTAINER_EXTRACTED", "extracted"), chosenFolderName);
							if (uploadImagesResult)
							{
								System.Console.WriteLine("****************************************************");
								System.Console.WriteLine("Image files uploaded!");
								System.Console.WriteLine("****************************************************\n\n");
							}
							else
							{
								System.Console.WriteLine("There was an issue while uploading the images.");
							}
						}
						break;

					case "3":
						await ExtractFramesAsync(videoHelper, storageHelper, GetContainerName("CONTAINER_VIDEOS", "videos"), GetContainerName("CONTAINER_INPUT", "input"), GetContainerName("CONTAINER_EXTRACTED", "extracted"));
						break;

					case "4":
						await RunSafetyAnalysisAsync(videoHelper, storageHelper, GetContainerName("CONTAINER_VIDEOS", "videos"), GetContainerName("CONTAINER_EXTRACTED", "extracted"), GetContainerName("CONTAINER_RESULTS", "results"));
						break;

					case "5":
						await ExportRunAsync(videoHelper, storageHelper, sqlHelper, GetContainerName("CONTAINER_VIDEOS", "videos"), GetContainerName("CONTAINER_EXTRACTED", "extracted"), GetContainerName("CONTAINER_RESULTS", "results"), csvHelper);
						break;

					case "6":
						await RunSafetyAnalysisDurableFunctionAsync(videoHelper, storageHelper, GetContainerName("CONTAINER_VIDEOS", "videos"), GetContainerName("CONTAINER_EXTRACTED", "extracted"), GetContainerName("CONTAINER_RESULTS", "results"));
						break;
				}
				choice = PrintMenu();
			}

			System.Console.WriteLine("Thank you for using Novel CSAM Detection. Goodbye!");
			LogHelper.LogInformation("Application terminated normally", nameof(Program), nameof(Main));
		}
		catch (Exception ex)
		{
			LogHelper.LogException($"A critical error occurred: {ex.Message}",
				nameof(Program), nameof(Main), ex);
			System.Console.WriteLine("A critical error occurred. Please check the logs for details.");
		}
	}

	#region Configuration Methods
	/// <summary>
	/// Configures dependency injection services for the application.
	/// </summary>
	private static void ConfigureServices(IServiceCollection services)
	{
		services.AddScoped<IAzureSQLHelper, AzureSQLHelper>();
		services.AddTransient<IContentSafetyHelper, ContentSafetyHelper>();
		services.AddTransient<IStorageHelper, StorageHelper>();
		services.AddTransient<ICsvExporter, CsvExporter>();
		services.AddTransient<IVideoHelper, VideoHelper>();
		services.AddSingleton<HttpClient>();
	}

	/// <summary>
	/// Gets container name from environment variable with default fallback.
	/// </summary>
	private static string GetContainerName(string envVarName, string defaultValue)
		=> Environment.GetEnvironmentVariable(envVarName) ?? defaultValue;

	/// <summary>
	/// Gets FilesPerFolder setting from environment variable.
	/// </summary>
	private static int GetFilesPerFolder()
		=> int.TryParse(Environment.GetEnvironmentVariable("FILES_PER_FOLDER"), out var value) ? value : 100;

	/// <summary>
	/// Loads application configuration from appsettings.json and sets environment variables.
	/// </summary>
	private static void SetEnvVariables()
	{
		// Build configuration
		var configuration = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.Build();

		var envVariables = new Dictionary<string, string>
		{
			{ "AZURE_SQL_CONNECTION_STRING", configuration["Azure:SqlConnectionString"] },
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

			{ "CONTAINER_VIDEOS", configuration["Azure:Storage:ContainerVideos"] ?? "videos" },
			{ "CONTAINER_INPUT", configuration["Azure:Storage:ContainerInput"] ?? "input" },
			{ "CONTAINER_EXTRACTED", configuration["Azure:Storage:ContainerExtracted"] ?? "extracted" },
			{ "CONTAINER_RESULTS", configuration["Azure:Storage:ContainerResults"] ?? "results" },
			{ "FILES_PER_FOLDER", configuration["Azure:Storage:FilesPerFolder"] ?? "100" },

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
				System.Console.WriteLine($"**********************************************************************");
				System.Console.WriteLine($"Warning: Missing environment variable value for key '{envVariable.Key}'.");
				System.Console.WriteLine($"This may cause the application to fail or have degraded functionality.");
				System.Console.WriteLine($"**********************************************************************");
			}

			Environment.SetEnvironmentVariable(envVariable.Key, envVariable.Value);
		}
	}
	#endregion

	#region Menu Methods
	private static string PrintMenu()
	{
		string choice;
		do
		{
			System.Console.WriteLine("\n\n\n\nNovel CSAM Detection Menu");
			System.Console.WriteLine("#######################################################");
			System.Console.WriteLine("#####..1.) Upload Video to Azure..................#####");
			System.Console.WriteLine("#####..2.) Upload Images to Azure.................#####");
			System.Console.WriteLine("#####..3.) Extract Frames.........................#####");
			System.Console.WriteLine("#####..4.) Run Safety Analysis....................#####");
			System.Console.WriteLine("#####..5.) Export Run.............................#####");
			System.Console.WriteLine("#####..6.) Run Safety Analysis (Durable Function) #####");
			System.Console.WriteLine("#####..X.) Exit...................................#####");
			System.Console.WriteLine("#######################################################");

			System.Console.WriteLine("Please enter a valid choice 1 - 6, or X to exit");
			choice = System.Console.ReadLine()?.ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "";
		} while (!new[] { "1", "2", "3", "4", "5", "6", "x" }.Contains(choice));

		return choice;
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
				System.Console.WriteLine("No folder selected.");
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
				System.Console.WriteLine("No file selected.");
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

	#region Upload Methods
	private static async Task UploadVideoAsync(IVideoHelper videoHelper, string containerName, string inputFolder, string selectedFilePath)
	{
		System.Console.WriteLine($"----------------------------------------------------------------------------\n");
		System.Console.WriteLine($"Selected file: {selectedFilePath}");

		var progressBar = new Helpers.ProgressBar();
		var done = "";
		await progressBar.RunWithProgressBarAsync(async () =>
		{
			done = await videoHelper.UploadFileToBlobAsync(containerName, inputFolder, selectedFilePath);
		});

		System.Console.WriteLine($"Selected file uploaded: {done}");
		System.Console.WriteLine($"----------------------------------------------------------------------------\r\n");
	}

	private static async Task<bool> UploadImagesAsync(IVideoHelper videoHelper, string containerName, string inputFolder, string selectedFolderPath)
	{
		System.Console.WriteLine($"----------------------------------------------------------------------------\n");
		System.Console.WriteLine($"Selected folder: {selectedFolderPath}");

		var imageFiles = Directory.GetFiles(selectedFolderPath, "*.*")
			.Where(file => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" }
				.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
			.ToList();

		if (imageFiles.Count == 0)
		{
			System.Console.WriteLine("No image files found in the selected folder.");
			return false;
		}

		System.Console.WriteLine("Enter a custom folder name please...");
		var customFolderName = System.Console.ReadLine();

		int folderIndex = 1;
		string currentFolderName = GenerateFolderName(folderIndex);
		string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

		var progressBar = new Helpers.ProgressBar();
		var done = false;

		await progressBar.RunWithProgressBarAsync(async () =>
		{
			var uploadTasks = imageFiles.Select((imageFile, index) =>
			{
				if (index > 0 && index % GetFilesPerFolder() == 0)
				{
					folderIndex++;
					currentFolderName = GenerateFolderName(folderIndex);
				}

				System.Console.WriteLine($"Selected file: {imageFile}");

				return Task.Run(async () =>
				{
					var uploadPath = await videoHelper.UploadFileToBlobAsync(containerName, inputFolder, imageFile, currentFolderName, true, timestamp, customFolderName);
					System.Console.WriteLine($"Selected file Upload Path: {uploadPath}");
				});
			}).ToList();

			await Task.WhenAll(uploadTasks);

			done = true;
		});

		return done;
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
				System.Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var item in menuItems)
				{
					System.Console.WriteLine($"({item.Key}): {item.Value}");
				}
				System.Console.WriteLine($"(-1): Return to Menu");
				System.Console.WriteLine($"----------------------------------------------------------------------");
				System.Console.WriteLine("Choose which file to extract frames from...e.g. 1");
				var userInput = System.Console.ReadLine();
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

				var progressBar = new Helpers.ProgressBar();
				var done = false;
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					done = await videoHelper.UploadExtractedFramesToBlobAsync(1, fileName, containerName, folderPath, extractedFolder, fileName);
				});

				if (done)
				{
					System.Console.WriteLine("************************************************************");
					System.Console.WriteLine($"{chosenDirValue} is done extracting!");
					System.Console.WriteLine("************************************************************");
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
				System.Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var dir in dirList)
				{
					System.Console.WriteLine($"({dir.Key}): {dir.Value}");
				}
				System.Console.WriteLine($"(-1): Return to Menu");
				System.Console.WriteLine($"----------------------------------------------------------------------");
				System.Console.WriteLine("Choose which directory...e.g. 1");
				var userInput = System.Console.ReadLine();
				bool isInteger = int.TryParse(userInput, out int result);
				chosenDirKey = isInteger ? result : -1;
			} while (!dirList.ContainsKey(chosenDirKey) && chosenDirKey != -1);

			if (chosenDirKey == -1)
				return;

			string chosenDirValue = dirList[chosenDirKey];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				System.Console.WriteLine($"Create a summary for each frame using GPT? (y or n)");
				var getSummary = System.Console.ReadLine();
				var getSummaryB = true;
				var getChildYesNoB = true;
				if (!string.IsNullOrEmpty(getSummary) && getSummary.ToLower() != "y")
					getSummaryB = false;

				System.Console.WriteLine($"Identify if a child is in the frame using GPT? (y or n)");
				var getChildYesNo = System.Console.ReadLine();
				if (!string.IsNullOrEmpty(getChildYesNo) && getChildYesNo.ToLower() != "y")
					getChildYesNoB = false;

				var progressBar = new Helpers.ProgressBar();
				var runId = "";
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					runId = await videoHelper.UploadFrameResultsAsync(containerName,
						chosenDirValue, resultsFolder,
						true, getSummaryB, getChildYesNoB);
				});

				if (!string.IsNullOrEmpty(runId))
				{
					System.Console.WriteLine("********************************************************************************");
					System.Console.WriteLine($"{chosenDirValue} is done running! RunId: {runId}");
					System.Console.WriteLine("********************************************************************************");
				}
			}
		}
		else
		{
			System.Console.WriteLine("There are no directories containing images for processing. \r\n" +
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
				System.Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var dir in dirList)
				{
					System.Console.WriteLine($"({dir.Key}): {dir.Value}");
				}
				System.Console.WriteLine($"(-1): Return to Menu");
				System.Console.WriteLine($"----------------------------------------------------------------------");
				System.Console.WriteLine("Choose which directory...e.g. 1");
				var userInput = System.Console.ReadLine();
				bool isInteger = int.TryParse(userInput, out int result);
				chosenDirKey = isInteger ? result : -1;
			} while (!dirList.ContainsKey(chosenDirKey) && chosenDirKey != -1);

			if (chosenDirKey == -1)
				return;

			string chosenDirValue = dirList[chosenDirKey];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				System.Console.WriteLine($"Create a summary for each frame using GPT? (y or n)");
				var getSummary = System.Console.ReadLine();
				var getSummaryB = true;
				var getChildYesNoB = true;
				if (!string.IsNullOrEmpty(getSummary) && getSummary.ToLower() != "y")
					getSummaryB = false;

				System.Console.WriteLine($"Identify if a child is in the frame using GPT? (y or n)");
				var getChildYesNo = System.Console.ReadLine();
				if (!string.IsNullOrEmpty(getChildYesNo) && getChildYesNo.ToLower() != "y")
					getChildYesNoB = false;

				var progressBar = new Helpers.ProgressBar();
				var runId = Guid.NewGuid().ToString();
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					await videoHelper.UploadFrameResultsDurableFunctionAsync(containerName,
						chosenDirValue, resultsFolder,
						true, getSummaryB, getChildYesNoB, runId);
				});

				if (!string.IsNullOrEmpty(runId))
				{
					System.Console.WriteLine("********************************************************************************");
					System.Console.WriteLine($"{chosenDirValue} is done running! RunId: {runId}");
					System.Console.WriteLine("********************************************************************************");
				}
			}
		}
		else
		{
			System.Console.WriteLine("There are no directories containing images for processing. \r\n" +
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
				System.Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var dir in dirList)
				{
					System.Console.WriteLine($"({dir.Key}): {dir.Value}");
				}
				System.Console.WriteLine($"(-1): Return to Menu");
				System.Console.WriteLine($"----------------------------------------------------------------------");
				System.Console.WriteLine("Choose which directory...e.g. 1");
				var userInput = System.Console.ReadLine();
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
					System.Console.WriteLine("Enter your export file name...e.g. output.csv");
					var userInput = System.Console.ReadLine();

					if (records == null || userInput == null)
					{
						throw new Exception("Records and/or UserInput is null");
					}

					var progressBar = new Helpers.ProgressBar();
					var ret = false;
					await progressBar.RunWithProgressBarAsync(async () =>
					{
						ret = await csvHelper.ExportToCsvAsync(records, userInput);
					});

					if (ret)
					{
						System.Console.WriteLine("****************************************************");
						System.Console.WriteLine($"{chosenDirValue} is done exporting!");
						System.Console.WriteLine("****************************************************");
					}
					else
					{
						System.Console.WriteLine("****************************************************");
						System.Console.WriteLine($"An error occurred when exporting to {chosenDirValue}!");
						System.Console.WriteLine("****************************************************");
					}
				}
			}
		}
		else
		{
			System.Console.WriteLine("There are no directories containing images for processing. \r\n" +
				"Try extracting some frames or uploading some images.");
		}
	}
	#endregion

	#region Helper Methods
	private static string GenerateFolderName(int index) => $"Batch_{index:D3}";
	#endregion
}
