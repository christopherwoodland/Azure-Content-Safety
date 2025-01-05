namespace NovelCsam.Helpers.Interfaces
{
	public interface IContentSafetyHelper
	{
		public Task<AnalyzeImageResult?> AnalyzeImageAsync(BinaryData inputImage);
	}
}
