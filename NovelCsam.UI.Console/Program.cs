internal class Program
{
	private const int FilesPerFolder = 100;

	private static string GenerateFolderName(int folderIndex) => $"Folder_{folderIndex}";

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
		var customFolderName = Console.ReadLine();
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
	private static string PrintMenu()
	{
		string choice;
		do
		{
			Console.WriteLine("Novel CSAM Detection Menu");
			Console.WriteLine("###############################################");
			Console.WriteLine("#####..1.) Upload Video to Azure..........#####");
			Console.WriteLine("#####..2.) Upload Images to Azure.........#####");
			Console.WriteLine("#####..3.) Extract Frames.................#####");
			Console.WriteLine("#####..4.) Run Safety Analysis............#####");
			Console.WriteLine("#####..5.) Export Run.....................#####");
			Console.WriteLine("#####..X.) Exit...........................#####");
			Console.WriteLine("###############################################");

			Console.WriteLine("Please enter a valid choice 1 - 4, or X to exit");
			choice = Console.ReadLine()?.ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "";
		} while (!new[] { "1", "2", "3", "4", "5", "x" }.Contains(choice));

		return choice;
	}

	private static void ConfigureServices(IServiceCollection services)
	{
		services.AddApplicationInsightsTelemetryWorkerService(options =>
		{
			options.ConnectionString = Environment.GetEnvironmentVariable("APP_INSIGHTS_CONNECTION_STRING");
		});

		services.AddTransient<IAzureSQLHelper, AzureSQLHelper>();
		services.AddScoped<ILogHelper, LogHelper>();
		services.AddScoped<IContentSafetyHelper, ContentSafetyHelper>();
		services.AddScoped<IStorageHelper, StorageHelper>();
		services.AddTransient<IVideoHelper, VideoHelper>();
	}

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
	}

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
						await ExportRunAsync(videoHelper, storageHelper, sqlHelper, ContainerVideos, ContainerExtracted, ContainerResults);
						break;
				}
				choice = PrintMenu();
			}
		}
		catch (Exception ex)
		{
			var serviceCollection = new ServiceCollection();
			ConfigureServices(serviceCollection);
			var serviceProvider = serviceCollection.BuildServiceProvider();
			var logHelper = serviceProvider.GetService<ILogHelper>();
			logHelper?.LogException($"An error occurred during run in main: {ex.Message}", nameof(Program), nameof(Main), ex);
		}
	}

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
	private static async Task UploadVideoAsync(IVideoHelper videoHelper, string containerName,
		string inputFolder, string selectedFilePath)
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

				var progressBar = new NovelCsam.Helpers.ProgressBar();
				var runId = "";
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					runId = await videoHelper.UploadFrameResultsAsync(containerName, chosenDirValue, resultsFolder, true);
				});

				if (!string.IsNullOrEmpty(runId))
				{
					Console.WriteLine("****************************************************");
					Console.WriteLine($"{chosenDirValue} is done running! RunId: {runId}");
					Console.WriteLine("****************************************************");
				}
			}
		}
		else
		{
			Console.WriteLine("There are no directories containing images for processing. \r\n" +
				"Try extracting some frames or uploading some images.");
		}
	}

	private static async Task ExportRunAsync(IVideoHelper videoHelper, IStorageHelper storageHelper, IAzureSQLHelper sqlHelper, string containerName, string extractedFolder, string resultsFolder)
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
					var csvExporter = new CsvExporter();
					Console.WriteLine("Enter your export file name..e.g. output.csv");
					var userInput = Console.ReadLine();

					if (records == null || userInput == null)
					{
						throw new Exception("Records and/or UserInput is null");
					}

					var progressBar = new NovelCsam.Helpers.ProgressBar();
					await progressBar.RunWithProgressBarAsync(async () =>
					{
						await csvExporter.ExportToCsvAsync(records, userInput);

					});

					Console.WriteLine("****************************************************");
					Console.WriteLine($"{chosenDirValue} is done exporting!");
					Console.WriteLine("****************************************************");
				}
			}
		}
		else
		{
			Console.WriteLine("There are no directories containing images for processing. \r\n" +
				"Try extracting some frames or uploading some images.");
		}
	}
}
