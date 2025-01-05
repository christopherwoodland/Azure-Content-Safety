namespace NovelCsam.Models.Interfaces
{
	/// <summary>
	/// Represents the result of analyzing a frame.
	/// </summary>
	public interface IFrameDetailResult
	{
		/// <summary>
		/// Gets or sets the unique identifier for the frame result.
		/// </summary>
		string Id { get; set; }

		/// <summary>
		/// Gets or sets the summary of the frame analysis.
		/// </summary>
		string? Summary { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether a child is present in the frame.
		/// </summary>
		string? ChildYesNo { get; set; }

		/// <summary>
		/// Gets or sets the MD5 hash of the frame.
		/// </summary>
		string? MD5Hash { get; set; }

		/// <summary>
		/// Gets or sets the frame data.
		/// </summary>
		string? Frame { get; set; }

		/// <summary>
		/// Gets or sets the run identifier for the analysis.
		/// </summary>
		string? RunId { get; set; }

		/// <summary>
		/// Gets or sets the hate score of the frame.
		/// </summary>
		int Hate { get; set; }

		/// <summary>
		/// Gets or sets the self-harm score of the frame.
		/// </summary>
		int SelfHarm { get; set; }

		/// <summary>
		/// Gets or sets the violence score of the frame.
		/// </summary>
		int Violence { get; set; }

		/// <summary>
		/// Gets or sets the sexual content score of the frame.
		/// </summary>
		int Sexual { get; set; }

		/// <summary>
		/// Gets or sets the base64-encoded image data of the frame.
		/// </summary>
		string? ImageBase64 { get; set; }

		/// <summary>
		/// Gets or sets the date and time when the analysis was run.
		/// </summary>
		DateTime RunDateTime { get; set; }

		string? HateLevel { get; set; }
		string? SelfHarmLevel { get; set; }
		string? ViolenceLevel { get; set; }
		string? SexualLevel { get; set; }

	}
}