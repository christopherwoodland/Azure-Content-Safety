namespace NovelCsam.Models
{
	/// <summary>
	/// Represents the result of analyzing a frame.
	/// </summary>
	public record FrameDetailResult : IFrameDetailResult
	{
		/// <summary>
		/// Gets or sets the unique identifier for the frame result.
		/// </summary>
		[JsonProperty("id")]
		public required string Id { get; set; }

		/// <summary>
		/// Gets or sets the summary of the frame analysis.
		/// </summary>
		public string? Summary { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether a child is present in the frame.
		/// </summary>
		public string? ChildYesNo { get; set; }

		/// <summary>
		/// Gets or sets the MD5 hash of the frame.
		/// </summary>
		public string? MD5Hash { get; set; }

		/// <summary>
		/// Gets or sets the frame data.
		/// </summary>
		public string? Frame { get; set; }

		/// <summary>
		/// Gets or sets the run identifier for the analysis.
		/// </summary>
		public string? RunId { get; set; }

		/// <summary>
		/// Gets or sets the hate score of the frame.
		/// </summary>
		public int Hate { get; set; }

		/// <summary>
		/// Gets or sets the self-harm score of the frame.
		/// </summary>
		public int SelfHarm { get; set; }

		/// <summary>
		/// Gets or sets the violence score of the frame.
		/// </summary>
		public int Violence { get; set; }

		/// <summary>
		/// Gets or sets the sexual content score of the frame.
		/// </summary>
		public int Sexual { get; set; }

		/// <summary>
		/// Gets or sets the base64-encoded image data of the frame.
		/// </summary>
		public string? ImageBase64 { get; set; }

		/// <summary>
		/// Gets or sets the date and time when the analysis was run.
		/// </summary>
		public DateTime RunDateTime { get; set; }
		public string? HateLevel { get; set; }
		public string? SelfHarmLevel { get; set; }
		public string? ViolenceLevel { get; set; }
		public string? SexualLevel { get; set; }
	}
}