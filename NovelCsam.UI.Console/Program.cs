using Newtonsoft.Json;
using NovelCsam.Models;
using NovelCsam.Models.Interfaces;

internal class Program
{
	#region Constants
	private const int FilesPerFolder = 100;
	#endregion

	#region Helper Methods
	private static string GenerateFolderName(int folderIndex) => $"Folder_{folderIndex}";
	#endregion

	#region Upload Methods
	private static async Task<bool> UploadImagesAsync(IVideoHelper videoHelper, string containerName, string inputFolder, string selectedFolderPath)
	{
		Console.WriteLine($"----------------------------------------------------------------------------\n");
		Console.WriteLine($"Selected folder: {selectedFolderPath}");

		var imageFiles = Directory.GetFiles(selectedFolderPath, "*.*")
								  .Where(file => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" }
								  .Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
								  .ToList();

		if (imageFiles.Count == 0)
		{
			Console.WriteLine("No image files found in the selected folder.");
			return false;
		}
		Console.WriteLine("Enter a custom folder name please...");
		var customFolderName = Console.ReadLine() ?? string.Empty;
		int folderIndex = 1;
		string currentFolderName = GenerateFolderName(folderIndex);
		string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

		var progressBar = new NovelCsam.Helpers.ProgressBar();
		var done = false;
		await progressBar.RunWithProgressBarAsync(async () =>
		{
			var uploadTasks = imageFiles.Select((imageFile, index) =>
			{
				if (index > 0 && index % FilesPerFolder == 0)
				{
					folderIndex++;
					currentFolderName = GenerateFolderName(folderIndex);
				}

				Console.WriteLine($"Selected file: {imageFile}");

				return Task.Run(async () =>
				{
					var uploadPath = await videoHelper.UploadFileToBlobAsync(containerName, inputFolder, imageFile, currentFolderName, true, timestamp, customFolderName);
					Console.WriteLine($"Selected file Upload Path: {uploadPath}");
				});
			}).ToList();

			await Task.WhenAll(uploadTasks);
			done = true;
		});

		return done;
	}

	private static async Task UploadVideoAsync(IVideoHelper videoHelper, string containerName, string inputFolder, string selectedFilePath)
	{
		Console.WriteLine($"----------------------------------------------------------------------------\n");
		Console.WriteLine($"Selected file: {selectedFilePath}");
		var progressBar = new NovelCsam.Helpers.ProgressBar();
		var done = "";
		await progressBar.RunWithProgressBarAsync(async () =>
		{
			done = await videoHelper.UploadFileToBlobAsync(containerName, inputFolder, selectedFilePath);
		});
		Console.WriteLine($"Selected file uploaded: {done}");
		Console.WriteLine($"----------------------------------------------------------------------------\r\n");
	}
	#endregion

	#region Menu Methods
	private static string PrintMenu()
	{
		string choice;
		do
		{
			Console.WriteLine("\n\n\n\nNovel CSAM Detection Menu");
			Console.WriteLine("#######################################################");
			Console.WriteLine("#####..1.) Upload Video to Azure..................#####");
			Console.WriteLine("#####..2.) Upload Images to Azure.................#####");
			Console.WriteLine("#####..3.) Extract Frames.........................#####");
			Console.WriteLine("#####..4.) Run Safety Analysis....................#####");
			Console.WriteLine("#####..5.) Export Run.............................#####");
			//Console.WriteLine("#####..6.) Run Safety Analysis (Durable Function) #####");
			Console.WriteLine("#####..X.) Exit...................................#####");
			Console.WriteLine("#######################################################");

			Console.WriteLine("Please enter a valid choice 1 - 4, or X to exit");
			choice = Console.ReadLine()?.ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "";
		} while (!new[] { "1", "2", "3", "4", "5", "6", "x" }.Contains(choice));

		return choice;
	}
	#endregion

	#region Configuration Methods
	private static void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient<IContentSafetyHelper, ContentSafetyHelper>();
		services.AddTransient<IStorageHelper, StorageHelper>();
		services.AddTransient<ICsvExporter, CsvExporter>();
		services.AddTransient<IVideoHelper, VideoHelper>();
		services.AddHttpClient();
	}

	private static void SetEnvVariables()
	{
		// Build configuration
		var configuration = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.Build();

		var envVariables = new Dictionary<string, string?>
		{
			{ "FFMPEG_PATH", configuration["Azure:FfmpegPath"] },
			{ "STORAGE_USE_MANAGED_IDENTITY", configuration["Azure:StorageUseManagedIdentity"] },
			{ "STORAGE_USE_AZURE_CLI_CREDENTIAL", configuration["Azure:StorageUseAzureCliCredential"] },
			{ "AZURE_STORAGE_CONNECTION_STRING", configuration["Azure:StorageConnectionString"] },
			{ "STORAGE_ACCOUNT_NAME", configuration["Azure:StorageAccountName"] },
			{ "STORAGE_ACCOUNT_KEY", configuration["Azure:StorageAccountKey"] },
			{ "STORAGE_ACCOUNT_URL", configuration["Azure:StorageAccountUrl"] },

			{ "CONTENT_SAFETY_USE_MANAGED_IDENTITY", configuration["Azure:ContentSafety:UseManagedIdentity"] },
			{ "CONTENT_SAFETY_ENDPOINT1", configuration["Azure:ContentSafety:Endpoint1"] },
			{ "OPEN_AI_KEY", configuration["Azure:OpenAiKey"] },
			{ "OPEN_AI_USE_MANAGED_IDENTITY", configuration["Azure:OpenAiUseManagedIdentity"] },
			{ "OPEN_AI_PROJECT_ENDPOINT", configuration["Azure:OpenAiProjectEndpoint"] },
			{ "OPEN_AI_ENDPOINT", configuration["Azure:OpenAiEndpoint"] },
			{ "OPEN_AI_DEPLOYMENT_NAME", configuration["Azure:OpenAiDeploymentName"] },
			{ "OPEN_AI_MODEL", configuration["Azure:OpenAiModel"] },
			{ "OPEN_AI_TIMEOUT_SECONDS", configuration["Azure:OpenAiTimeoutSeconds"] },

			{ "ENABLE_JSON_EXPORT", configuration["Azure:EnableJsonExport"] },
			{ "JSON_EXPORT_CONTAINER_NAME", configuration["Azure:JsonExportContainerName"] },
			{ "JSON_EXPORT_FOLDER_PATH", configuration["Azure:JsonExportFolderPath"] },

			{ "APPLICATIONINSIGHTS_CONNECTION_STRING", configuration["Azure:AppInsightsConnectionString"] },
			{ "INVOKE_OPEN_AI", configuration["Azure:InvokeOpenAI"] },
			{ "ANALYZE_FRAME_AZURE_FUNCTION_URL", configuration["Azure:AnalyzeFrameAzureFunctionUrl"] },
			{ "DEBUG_TO_CONSOLE", configuration["Azure:DebugToConsole"] },
			{ "CONTENT_SAFETY_CONNECTION_KEY1", configuration["Azure:ContentSafety:ContentSafetyConnectionKey1"] },
			{ "CONTENT_SAFETY_ENDPOINT2", configuration["Azure:ContentSafety:Endpoint2"] },
			{ "CONTENT_SAFETY_CONNECTION_KEY2", configuration["Azure:ContentSafety:ContentSafetyConnectionKey2"] },
			{ "CONTENT_SAFETY_ENDPOINT3", configuration["Azure:ContentSafety:Endpoint3"] },
			{ "CONTENT_SAFETY_CONNECTION_KEY3", configuration["Azure:ContentSafety:ContentSafetyConnectionKey3"] },
		};
		foreach (var envVariable in envVariables)
		{
			Environment.SetEnvironmentVariable(envVariable.Key, envVariable.Value ?? string.Empty);
		}

		var storageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
		var storageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
		if (string.IsNullOrWhiteSpace(storageConnectionString) && string.IsNullOrWhiteSpace(storageAccountUrl))
		{
			Console.WriteLine("Storage is not fully configured. Set AZURE_STORAGE_CONNECTION_STRING or STORAGE_ACCOUNT_URL before running upload/analysis operations.");
		}

		var invokeOpenAi = string.Equals(Environment.GetEnvironmentVariable("INVOKE_OPEN_AI"), "true", StringComparison.OrdinalIgnoreCase);
		if (invokeOpenAi)
		{
			var openAiModel = Environment.GetEnvironmentVariable("OPEN_AI_MODEL");
			var openAiProjectEndpoint = Environment.GetEnvironmentVariable("OPEN_AI_PROJECT_ENDPOINT");
			var openAiEndpoint = Environment.GetEnvironmentVariable("OPEN_AI_ENDPOINT");
			if (string.IsNullOrWhiteSpace(openAiModel) || (string.IsNullOrWhiteSpace(openAiProjectEndpoint) && string.IsNullOrWhiteSpace(openAiEndpoint)))
			{
				Console.WriteLine("OpenAI is enabled but not fully configured. Set OPEN_AI_MODEL and OPEN_AI_PROJECT_ENDPOINT (preferred) or OPEN_AI_ENDPOINT.");
			}
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
		Console.WriteLine("Enter frame interval in seconds (e.g. 1 = every second). Press Enter for default 1:");
		var frameIntervalInput = Console.ReadLine();
		var frameIntervalSeconds = 1;
		if (!string.IsNullOrWhiteSpace(frameIntervalInput) && int.TryParse(frameIntervalInput, out var parsedInterval))
		{
			frameIntervalSeconds = Math.Max(1, parsedInterval);
		}

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
				var folderPath = (Path.GetDirectoryName(chosenDirValue) ?? string.Empty).Replace("\\", "/");

				var progressBar = new NovelCsam.Helpers.ProgressBar();
				var done = false;
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					done = await videoHelper.UploadExtractedFramesToBlobAsync(frameIntervalSeconds, fileName, containerName, folderPath, extractedFolder, fileName);
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
	private static async Task ExportRunAsync(IStorageHelper storageHelper,
		string containerName, string extractedFolder, string resultsFolder, ICsvExporter csvHelper)
	{
		var dirList = await storageHelper.ListDirectoriesInFolderAsync(containerName, resultsFolder, 2) ?? [];
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
				var manifestPath = $"{resultsFolder}/{chosenDirValue}/job-result.json";
				var manifestJson = await storageHelper.DownloadTextAsync(containerName, manifestPath);
				var manifest = string.IsNullOrWhiteSpace(manifestJson)
					? null
					: JsonConvert.DeserializeObject<JobResultManifest>(manifestJson);
				var frameResultBlobs = manifest?.FrameResultBlobs ?? [];
				if (frameResultBlobs.Count != 0)
				{
					Console.WriteLine("Enter your export file name..e.g. output.csv");
					var userInput = Console.ReadLine();

					if (userInput == null)
					{
						throw new Exception("UserInput is null");
					}

					var records = new List<IFrameDetailResult>();
					foreach (var blobPath in frameResultBlobs)
					{
						var recordJson = await storageHelper.DownloadTextAsync(containerName, blobPath);
						if (string.IsNullOrWhiteSpace(recordJson))
						{
							continue;
						}

						var recordEnvelope = JsonConvert.DeserializeObject<FrameResultEnvelope>(recordJson);
						if (recordEnvelope?.FrameResult == null)
						{
							continue;
						}

						records.Add(recordEnvelope.FrameResult);
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
			var videoHelper = serviceProvider.GetRequiredService<IVideoHelper>();
			var storageHelper = serviceProvider.GetRequiredService<IStorageHelper>();
			var csvHelper = serviceProvider.GetRequiredService<ICsvExporter>();

			if (args.Length > 0 && File.Exists(args[0]))
			{
				await UploadVideoAsync(videoHelper, ContainerVideos, ContainerInput, args[0]);
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
						await ExportRunAsync(storageHelper, ContainerVideos, ContainerExtracted, ContainerResults, csvHelper);
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

	private sealed class FrameResultEnvelope
	{
		public FrameDetailResult? FrameResult { get; set; }
	}
	#endregion
}
