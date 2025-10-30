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
				Console.WriteLine("\n\n\n\nNovel CSAM Detection Menu");
				Console.WriteLine("#######################################################");
				Console.WriteLine("#####..1.) Upload Video to Azure..................#####");
				Console.WriteLine("#####..2.) Upload Images to Azure.................#####");
				Console.WriteLine("#####..3.) Extract Frames.........................#####");
				Console.WriteLine("#####..4.) Run Safety Analysis....................#####");
				Console.WriteLine("#####..5.) Export Run.............................#####");
				Console.WriteLine("#####..6.) Run Safety Analysis (Durable Function) #####");
				Console.WriteLine("#####..X.) Exit...................................#####");
				Console.WriteLine("#######################################################");

				Console.WriteLine("Please enter a valid choice 1 - 6, or X to exit");
				choice = Console.ReadLine()?.ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "";
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
				Console.WriteLine($"An error occurred: {ex.Message}");
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
			Console.WriteLine($"----------------------------------------------------------------------------\n");
			Console.WriteLine($"Selected file: {selectedFilePath}");
			var progressBar = new ProgressBar();
			var done = "";
			await progressBar.RunWithProgressBarAsync(async () =>
			{
				done = await _videoHelper.UploadFileToBlobAsync(ContainerVideos, ContainerInput, selectedFilePath);
			});
			Console.WriteLine($"Selected file uploaded: {done}");
			Console.WriteLine($"----------------------------------------------------------------------------\r\n");
			LogHelper.LogInformation($"Video uploaded: {done}", nameof(MenuHandler), nameof(UploadVideoAsync));
		}

		private async Task<bool> UploadImagesAsync(string selectedFolderPath)
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
				LogHelper.LogWarning("No image files found in folder", nameof(MenuHandler), nameof(UploadImagesAsync));
				return false;
			}

			Console.WriteLine("Enter a custom folder name please...");
			var customFolderName = Console.ReadLine();
			int folderIndex = 1;
			string currentFolderName = GenerateFolderName(folderIndex);
			string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

			var progressBar = new ProgressBar();
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
						var uploadPath = await _videoHelper.UploadFileToBlobAsync(ContainerVideos, ContainerInput, 
							imageFile, currentFolderName, true, timestamp, customFolderName);
						Console.WriteLine($"Selected file Upload Path: {uploadPath}");
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
				Console.WriteLine("No files available for frame extraction.");
				return;
			}

			var selectedFile = SelectFromMenuAsync(blobList.Select((item, index) => 
				new { Key = index + 1, Value = item.Key }).ToList());

			if (!selectedFile.HasValue)
				return;

			var chosenDirValue = blobList.ElementAt(selectedFile.Value - 1).Key;

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				var fileName = Path.GetFileName(chosenDirValue);
				var folderPath = Path.GetDirectoryName(chosenDirValue)?.Replace("\\", "/") ?? "";

				var progressBar = new ProgressBar();
				var done = false;
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					done = await _videoHelper.UploadExtractedFramesToBlobAsync(1, fileName, ContainerVideos, 
						folderPath, ContainerExtracted, fileName);
				});

				if (done)
				{
					Console.WriteLine("************************************************************");
					Console.WriteLine($"{chosenDirValue} is done extracting!");
					Console.WriteLine("************************************************************");
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
				Console.WriteLine("There are no directories containing images for processing.");
				return;
			}

			var selectedKey = SelectDirectoryFromMenu(dirList);
			if (!selectedKey.HasValue)
				return;

			string chosenDirValue = dirList[selectedKey.Value];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				var (getSummary, getChildYesNo) = GetAnalysisOptions();

				var progressBar = new ProgressBar();
				var runId = "";
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					runId = await _videoHelper.UploadFrameResultsAsync(ContainerVideos,
						chosenDirValue, ContainerResults,
						true, getSummary, getChildYesNo);
				});

				if (!string.IsNullOrEmpty(runId))
				{
					Console.WriteLine("********************************************************************************");
					Console.WriteLine($"{chosenDirValue} is done running! RunId: {runId}");
					Console.WriteLine("********************************************************************************");
					LogHelper.LogInformation($"Safety analysis completed. RunId: {runId}", nameof(MenuHandler), nameof(RunSafetyAnalysisAsync));
				}
			}
		}

		private async Task RunSafetyAnalysisDurableFunctionAsync()
		{
			var dirList = await _storageHelper.ListDirectoriesInFolderAsync(ContainerVideos, ContainerExtracted, 2) ?? [];
			if (dirList?.Count == 0)
			{
				Console.WriteLine("There are no directories containing images for processing.");
				return;
			}

			var selectedKey = SelectDirectoryFromMenu(dirList);
			if (!selectedKey.HasValue)
				return;

			string chosenDirValue = dirList[selectedKey.Value];

			if (!string.IsNullOrEmpty(chosenDirValue))
			{
				var (getSummary, getChildYesNo) = GetAnalysisOptions();

				var progressBar = new ProgressBar();
				var runId = Guid.NewGuid().ToString();
				await progressBar.RunWithProgressBarAsync(async () =>
				{
					await _videoHelper.UploadFrameResultsDurableFunctionAsync(ContainerVideos,
						chosenDirValue, ContainerResults,
						true, getSummary, getChildYesNo, runId);
				});

				if (!string.IsNullOrEmpty(runId))
				{
					Console.WriteLine("********************************************************************************");
					Console.WriteLine($"{chosenDirValue} is done running! RunId: {runId}");
					Console.WriteLine("********************************************************************************");
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
				Console.WriteLine("There are no directories containing images for processing.");
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
					Console.WriteLine("No records found for export.");
					return;
				}

				Console.WriteLine("Enter your export file name..e.g. output.csv");
				var userInput = Console.ReadLine();

				if (records == null || userInput == null)
				{
					throw new Exception("Records and/or UserInput is null");
				}

				var progressBar = new ProgressBar();
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

		private int? SelectDirectoryFromMenu(Dictionary<int, string> dirList)
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

			return chosenDirKey == -1 ? null : (int?)chosenDirKey;
		}

		private int? SelectFromMenuAsync<T>(List<dynamic> menuItems)
		{
			int chosenKey;
			do
			{
				Console.WriteLine($"----------------------------------------------------------------------");
				foreach (var item in menuItems)
				{
					Console.WriteLine($"({item.Key}): {item.Value}");
				}
				Console.WriteLine($"(-1): Return to Menu");
				Console.WriteLine($"----------------------------------------------------------------------");
				Console.WriteLine("Choose which file...e.g. 1");
				var userInput = Console.ReadLine();
				bool isInteger = int.TryParse(userInput, out int result);
				chosenKey = isInteger ? result : -1;
			} while (!menuItems.Any(m => m.Key == chosenKey) && chosenKey != -1);

			return chosenKey == -1 ? null : (int?)chosenKey;
		}

		private (bool getSummary, bool getChildYesNo) GetAnalysisOptions()
		{
			Console.WriteLine($"Create a summary for each frame using GPT? (y or n)");
			var getSummary = Console.ReadLine();
			var getSummaryB = string.IsNullOrEmpty(getSummary) || getSummary.ToLower() == "y";

			Console.WriteLine($"Identify if a child is in the frame using GPT? (y or n)");
			var getChildYesNo = Console.ReadLine();
			var getChildYesNoB = string.IsNullOrEmpty(getChildYesNo) || getChildYesNo.ToLower() == "y";

			return (getSummaryB, getChildYesNoB);
		}

		private void PrintResult(string message, bool success)
		{
			if (success)
			{
				Console.WriteLine("****************************************************");
				Console.WriteLine(message);
				Console.WriteLine("****************************************************\n\n");
			}
			else
			{
				Console.WriteLine("There was an issue while processing your request.");
			}
		}

		private void PrintExportResult(bool success, string directoryValue)
		{
			Console.WriteLine("****************************************************");
			if (success)
			{
				Console.WriteLine($"{directoryValue} is done exporting!");
			}
			else
			{
				Console.WriteLine($"An error occurred when exporting from {directoryValue}!");
			}
			Console.WriteLine("****************************************************");
		}

		#endregion
	}
}
