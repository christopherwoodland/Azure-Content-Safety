
namespace NovelCsam.Helpers
{
	public class CsvExporter: ICsvExporter
	{
		public async Task ExportToCsvAsync(IEnumerable<IFrameDetailResult> records, string filePath)
		{
			var csvContent = new StringBuilder();

			// Add header
			csvContent.AppendLine("MD5Hash,Summary,ChildYesNo,Frame,Hate,HateLevel,SelfHarm,SelfHarmLevel,Violence,ViolenceLevel,Sexual,SexualLevel,RunDateTime");

			// Add records
			foreach (var record in records)
			{
				csvContent.AppendLine($"{record.MD5Hash},{record.Summary},{record.ChildYesNo},{record.Frame},{record.Hate},{record.HateLevel},{record.SelfHarm},{record.SelfHarmLevel},{record.Violence},{record.ViolenceLevel},{record.Sexual},{record.SexualLevel},{record.RunDateTime:yyyy-MM-dd HH:mm:ss}");
			}

			// Write to file
			await File.WriteAllTextAsync(filePath, csvContent.ToString());
		}
	}
}