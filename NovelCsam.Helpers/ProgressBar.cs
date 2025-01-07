namespace NovelCsam.Helpers
{
	public class ProgressBar
	{
		public async Task RunWithProgressBarAsync(Func<Task> functionToRun, int updateInterval = 100)
		{
			var progress = new Progress<int>(percent => DrawProgressBar(percent));
			var cts = new CancellationTokenSource();

			var progressTask = Task.Run(async () =>
			{
				int percentComplete = 0;
				while (!cts.Token.IsCancellationRequested)
				{
					percentComplete = (percentComplete + 1) % 101;
					((IProgress<int>)progress).Report(percentComplete);
					await Task.Delay(updateInterval);
				}
			}, cts.Token);

			await functionToRun();

			cts.Cancel();
			await progressTask;

			Console.WriteLine();
		}

		private static void DrawProgressBar(int percent)
		{
			const int barWidth = 50;
			int progress = (int)((percent / 100.0) * barWidth);
			string progressBar = new string('#', progress) + new string('-', barWidth - progress);
			Console.Write($"\r[{progressBar}]");// {percent}%");
		}
	}

}
