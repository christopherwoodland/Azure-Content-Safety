namespace NovelCsam.UI.Console
{
	/// <summary>
	/// Handles menu display and user input for the console application.
	/// </summary>
	internal class MenuHandler
	{
		private const int FilesPerFolder = 100;
		private const string ContainerVideos = "videos";
		private const string ContainerInput = "input";
		private const string ContainerExtracted = "extracted";
		private const string ContainerResults = "results";

		private readonly IVideoHelper _videoHelper;
		private readonly IStorageHelper _storageHelper;
		private readonly IAzureSQLHelper _sqlHelper;
		private readonly ICsvExporter _csvHelper;

		/// <summary>
		/// Initializes a new instance of the <see cref="MenuHandler"/> class.
		/// </summary>
		public MenuHandler(IVideoHelper videoHelper, IStorageHelper storageHelper, 
			IAzureSQLHelper sqlHelper, ICsvExporter csvHelper)
		{
			_videoHelper = videoHelper ?? throw new ArgumentNullException(nameof(videoHelper));
			_storageHelper = storageHelper ?? throw new ArgumentNullException(nameof(storageHelper));
			_sqlHelper = sqlHelper ?? throw new ArgumentNullException(nameof(sqlHelper));
			_csvHelper = csvHelper ?? throw new ArgumentNullException(nameof(csvHelper));
		}

		/// <summary>
		/// Displays the main menu and returns the user's choice.
		/// </summary>
		public string PrintMenu()
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

		/// <summary>
		/// Processes the user's menu selection.
		/// </summary>
		public async Task ProcessMenuSelectionAsync(string choice)
		{
			try
			{
				switch (choice)
				{
					case "1":
						await ProcessUploadVideoAsync();
						break;
					case "2":
						await ProcessUploadImagesAsync();
						break;
					case "3":
						await ProcessExtractFramesAsync();
						break;
					case "4":
						await ProcessRunSafetyAnalysisAsync();
						break;
					case "5":
						await ProcessExportRunAsync();
						break;
					case "6":
						await ProcessRunSafetyAnalysisDurableFunctionAsync();
						break;
				}
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred processing menu selection '{choice}': {ex.Message}", 
					nameof(MenuHandler), nameof(ProcessMenuSelectionAsync), ex);
				System.Console.WriteLine($"An error occurred: {ex.Message}");
			}
		}

		#region Private Menu Processing Methods

		private async Task ProcessUploadVideoAsync()
		{
			var chosenFileName = ShowFileDialog();
			if (!string.IsNullOrEmpty(chosenFileName))
			{
				await UploadVideoAsync(chosenFileName);
			}
		}

		private async Task ProcessUploadImagesAsync()
		{
			var chosenFolderName = ShowFolderBrowserDialog();
			if (!string.IsNullOrEmpty(chosenFolderName))
			{
				var uploadImagesResult = await UploadImagesAsync(chosenFolderName);
				PrintResult("Image files uploaded!", uploadImagesResult);
			}
		}

		private async Task ProcessExtractFramesAsync()
		{
			await ExtractFramesAsync();
		}

		private async Task ProcessRunSafetyAnalysisAsync()
		{
			await RunSafetyAnalysisAsync();
		}

		private async Task ProcessRunSafetyAnalysisDurableFunctionAsync()
		{
			await RunSafetyAnalysisDurableFunctionAsync();
		}

		private async Task ProcessExportRunAsync()
		{
			await ExportRunAsync();
		}

		#endregion

		#region Upload Methods

		private async Task UploadVideoAsync(string selectedFilePath)
		{
			System.Console.WriteLine($"----------------------------------------------------------------------------\n");
			System.Console.WriteLine($"Selected file: {selectedFilePath}");
			var progressBar = new Helpers.ProgressBar();
			var done = "";
			await progressBar.RunWithProgressBarAsync(async () =>
			{
				done = await _videoHelper.UploadFileToBlobAsync(ContainerVideos, ContainerInput, selectedFilePath);
			});
			System.Console.WriteLine($"Selected file uploaded: {done}");
			System.Console.WriteLine($"----------------------------------------------------------------------------\r\n");
			LogHelper.LogInformation($"Video uploaded: {done}", nameof(MenuHandler), nameof(UploadVideoAsync));
		}

		private async Task<bool> UploadImagesAsync(string selectedFolderPath)
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
			LogHelper.LogInformation("No image files found in folder", nameof(MenuHandler), nameof(UploadImagesAsync));
			return false;
		}			System.Console.WriteLine("Enter a custom folder name please...");
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
					if (index > 0 && index % FilesPerFolder == 0)
					{
						folderIndex++;
						currentFolderName = GenerateFolderName(folderIndex);
					}

					System.Console.WriteLine($"Selected file: {imageFile}");

					return Task.Run(async () =>
					{
						var uploadPath = await _videoHelper.UploadFileToBlobAsync(ContainerVideos, ContainerInput, 
							imageFile, currentFolderName, true, timestamp, customFolderName);
						System.Console.WriteLine($"Selected file Upload Path: {uploadPath}");
						LogHelper.LogInformation($"Uploaded: {uploadPath}", nameof(MenuHandler), nameof(UploadImagesAsync));
					});
				}).ToList();

				await Task.WhenAll(uploadTasks);
				done = true;
			});

			return done;
		}

		#endregion

		#region Frame Extraction Methods

		private async Task ExtractFramesAsync()
		{
			var blobList = await _storageHelper.ListBlobsInFolderWithResizeAsync(ContainerVideos, ContainerInput, 3, false) ?? [];
			if (blobList?.Count == 0)
			{
				System.Console.WriteLine("No files available for frame extraction.");
				return;
			}

		var selectedFile = SelectFromMenuAsync<dynamic>(blobList.Select((item, index) => 
			new { Key = index + 1, Value = item.Key }).Cast<dynamic>().ToList());			if (!selectedFile.HasValue)
				return;

			var chosenDirValue = blobList.ElementAt(selectedFile.Value - 1).Key;

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				var fileName = Path.GetFileName(chosenDirValue);
				var folderPath = Path.GetDirectoryName(chosenDirValue)?.Replace("\\", "/") ?? "";

				var progressBar = new Helpers.ProgressBar();
				var done = false;
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					done = await _videoHelper.UploadExtractedFramesToBlobAsync(1, fileName, ContainerVideos, 
						folderPath, ContainerExtracted, fileName);
				});

				if (done)
				{
					System.Console.WriteLine("************************************************************");
					System.Console.WriteLine($"{chosenDirValue} is done extracting!");
					System.Console.WriteLine("************************************************************");
					LogHelper.LogInformation($"Frames extracted from: {chosenDirValue}", nameof(MenuHandler), nameof(ExtractFramesAsync));
				}
			}
		}

		#endregion

		#region Safety Analysis Methods

		private async Task RunSafetyAnalysisAsync()
		{
			var dirList = await _storageHelper.ListDirectoriesInFolderAsync(ContainerVideos, ContainerExtracted, 2) ?? [];
			if (dirList?.Count == 0)
			{
				System.Console.WriteLine("There are no directories containing images for processing.");
				return;
			}

			var selectedKey = SelectDirectoryFromMenu(dirList);
			if (!selectedKey.HasValue)
				return;

			string chosenDirValue = dirList[selectedKey.Value];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				var (getSummary, getChildYesNo) = GetAnalysisOptions();

				var progressBar = new Helpers.ProgressBar();
				var runId = "";
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					runId = await _videoHelper.UploadFrameResultsAsync(ContainerVideos,
						chosenDirValue, ContainerResults,
						true, getSummary, getChildYesNo);
				});

				if (!string.IsNullOrEmpty(runId))
				{
					System.Console.WriteLine("********************************************************************************");
					System.Console.WriteLine($"{chosenDirValue} is done running! RunId: {runId}");
					System.Console.WriteLine("********************************************************************************");
					LogHelper.LogInformation($"Safety analysis completed. RunId: {runId}", nameof(MenuHandler), nameof(RunSafetyAnalysisAsync));
				}
			}
		}

		private async Task RunSafetyAnalysisDurableFunctionAsync()
		{
			var dirList = await _storageHelper.ListDirectoriesInFolderAsync(ContainerVideos, ContainerExtracted, 2) ?? [];
			if (dirList?.Count == 0)
			{
				System.Console.WriteLine("There are no directories containing images for processing.");
				return;
			}

			var selectedKey = SelectDirectoryFromMenu(dirList);
			if (!selectedKey.HasValue)
				return;

			string chosenDirValue = dirList[selectedKey.Value];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				var (getSummary, getChildYesNo) = GetAnalysisOptions();

				var progressBar = new Helpers.ProgressBar();
				var runId = Guid.NewGuid().ToString();
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					await _videoHelper.UploadFrameResultsDurableFunctionAsync(ContainerVideos,
						chosenDirValue, ContainerResults,
						true, getSummary, getChildYesNo, runId);
				});

				if (!string.IsNullOrEmpty(runId))
				{
					System.Console.WriteLine("********************************************************************************");
					System.Console.WriteLine($"{chosenDirValue} is done running! RunId: {runId}");
					System.Console.WriteLine("********************************************************************************");
					LogHelper.LogInformation($"Durable function safety analysis completed. RunId: {runId}", 
						nameof(MenuHandler), nameof(RunSafetyAnalysisDurableFunctionAsync));
				}
			}
		}

		#endregion

		#region Export Methods

		private async Task ExportRunAsync()
		{
			var dirList = await _storageHelper.ListDirectoriesInFolderAsync(ContainerVideos, ContainerExtracted, 2) ?? [];
			if (dirList?.Count == 0)
			{
				System.Console.WriteLine("There are no directories containing images for processing.");
				return;
			}

			var selectedKey = SelectDirectoryFromMenu(dirList);
			if (!selectedKey.HasValue)
				return;

			string chosenDirValue = dirList[selectedKey.Value];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				var records = await _sqlHelper.GetFrameResultWithLevelsAsync(chosenDirValue);
				if (records?.Count == 0)
				{
					System.Console.WriteLine("No records found for export.");
					return;
				}

				System.Console.WriteLine("Enter your export file name..e.g. output.csv");
				var userInput = System.Console.ReadLine();

				if (records == null || userInput == null)
				{
					throw new Exception("Records and/or UserInput is null");
				}

				var progressBar = new Helpers.ProgressBar();
				var ret = false;
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					ret = await _csvHelper.ExportToCsvAsync(records, userInput);
				});

				PrintExportResult(ret, chosenDirValue);
				LogHelper.LogInformation($"Export completed. Result: {ret}", nameof(MenuHandler), nameof(ExportRunAsync));
			}
		}

		#endregion

		#region Helper Methods

		private string GenerateFolderName(int folderIndex) => $"Folder_{folderIndex}";

		private string ShowFolderBrowserDialog()
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

		private string ShowFileDialog()
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

		private int? SelectDirectoryFromMenu(Dictionary<int, string> dirList)
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

			return chosenDirKey == -1 ? null : (int?)chosenDirKey;
		}

		private int? SelectFromMenuAsync<T>(List<dynamic> menuItems)
		{
			int chosenKey;
			do
			{
				System.Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var item in menuItems)
				{
					System.Console.WriteLine($"({item.Key}): {item.Value}");
				}
				System.Console.WriteLine($"(-1): Return to Menu");
				System.Console.WriteLine($"----------------------------------------------------------------------");
				System.Console.WriteLine("Choose which file...e.g. 1");
				var userInput = System.Console.ReadLine();
				bool isInteger = int.TryParse(userInput, out int result);
				chosenKey = isInteger ? result : -1;
			} while (!menuItems.Any(m => m.Key == chosenKey) && chosenKey != -1);

			return chosenKey == -1 ? null : (int?)chosenKey;
		}

		private (bool getSummary, bool getChildYesNo) GetAnalysisOptions()
		{
			System.Console.WriteLine($"Create a summary for each frame using GPT? (y or n)");
			var getSummary = System.Console.ReadLine();
			var getSummaryB = string.IsNullOrEmpty(getSummary) || getSummary.ToLower() == "y";

			System.Console.WriteLine($"Identify if a child is in the frame using GPT? (y or n)");
			var getChildYesNo = System.Console.ReadLine();
			var getChildYesNoB = string.IsNullOrEmpty(getChildYesNo) || getChildYesNo.ToLower() == "y";

			return (getSummaryB, getChildYesNoB);
		}

		private void PrintResult(string message, bool success)
		{
			if (success)
			{
				System.Console.WriteLine("****************************************************");
				System.Console.WriteLine(message);
				System.Console.WriteLine("****************************************************\n\n");
			}
			else
			{
				System.Console.WriteLine("There was an issue while processing your request.");
			}
		}

		private void PrintExportResult(bool success, string directoryValue)
		{
			System.Console.WriteLine("****************************************************");
			if (success)
			{
				System.Console.WriteLine($"{directoryValue} is done exporting!");
			}
			else
			{
				System.Console.WriteLine($"An error occurred when exporting from {directoryValue}!");
			}
			System.Console.WriteLine("****************************************************");
		}

		#endregion
	}
}
