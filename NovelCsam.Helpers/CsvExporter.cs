namespace NovelCsam.Helpers
{
	public class CsvExporter : ICsvExporter
	{
		private readonly ILogHelper _logHelper;

		public CsvExporter(ILogHelper logHelper)
		{
			_logHelper = logHelper;

		}
		public async Task<bool> ExportToCsvAsync(IEnumerable<IFrameDetailResult> records, string filePath)
		{
			try
			{

				var config = new CsvConfiguration(CultureInfo.InvariantCulture)
				{
					Delimiter = ",",
					NewLine = Environment.NewLine,
					HasHeaderRecord = true
				};

				using var writer = new StreamWriter(filePath);
				using var csv = new CsvWriter(writer, config);
				// Write header
				csv.WriteHeader<IFrameDetailResult>();
				await csv.NextRecordAsync();

				// Write records
				await csv.WriteRecordsAsync(records);
				return true;
			}
			catch (Exception ex)
			{
				_logHelper.LogException(ex.Message, nameof(CsvExporter), nameof(ExportToCsvAsync), ex);
				return false;
			}
		}
	}
}