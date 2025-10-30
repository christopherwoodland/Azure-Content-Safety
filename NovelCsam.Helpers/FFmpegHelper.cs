namespace NovelCsam.Helpers
{
	/// <summary>
	/// Utility class for FFmpeg operations including frame extraction and video segmentation.
	/// </summary>
	internal class FFmpegHelper
	{
		private enum FFMPEG_MODE { VIDEO_SEGMENT = 0, FRAME_SEGMENT = 1 }

		private const string FrameExtensionJpg = "*.jpg";
		private const string VideoExtensionMkv = "*.mkv";
		private const int MaxFileSize = 4194304; // 4MB

		/// <summary>
		/// Extracts frames from a video file at the specified interval.
		/// </summary>
		/// <param name="videoPath">Path to the video file</param>
		/// <param name="frameInterval">Interval between frames in seconds</param>
		/// <param name="filename">Output filename prefix (unused but kept for potential future use)</param>
		/// <returns>List of extracted frame file paths</returns>
		public async Task<List<string>> ExtractFramesAsync(string videoPath, int frameInterval = 1, string filename = "frame")
		{
			try
			{
				var outputDir = Path.GetDirectoryName(videoPath) ?? throw new InvalidOperationException("Unable to determine output directory");
				Directory.CreateDirectory(outputDir);

				await RunFFmpegAsync(videoPath, outputDir, null, FFMPEG_MODE.FRAME_SEGMENT, frameInterval);
				
				var extractedFrames = Directory.GetFiles(outputDir, FrameExtensionJpg).ToList();
				extractedFrames.Sort();
				
				LogHelper.LogInformation($"Successfully extracted {extractedFrames.Count} frames from {videoPath}", 
					nameof(FFmpegHelper), nameof(ExtractFramesAsync));
				
				return extractedFrames;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while extracting frames from {videoPath}: {ex.Message}", 
					nameof(FFmpegHelper), nameof(ExtractFramesAsync), ex);
				return new List<string>();
			}
		}

		/// <summary>
		/// Segments a video file into smaller chunks of specified duration.
		/// </summary>
		/// <param name="videoPath">Path to the video file</param>
		/// <param name="splitTimeMinutes">Duration of each segment in minutes</param>
		/// <returns>List of segmented video file paths</returns>
		public async Task<List<string>> SegmentVideoAsync(string videoPath, int splitTimeMinutes)
		{
			try
			{
				var dirName = Path.GetDirectoryName(videoPath) ?? throw new InvalidOperationException("Unable to determine output directory");
				string outputDir = Path.Combine(dirName, "output");
				Directory.CreateDirectory(outputDir);

				var segmentDuration = TimeSpan.FromMinutes(splitTimeMinutes);
				await RunFFmpegAsync(videoPath, outputDir, segmentDuration, FFMPEG_MODE.VIDEO_SEGMENT);
				
				var segmentedVideos = Directory.GetFiles(outputDir, VideoExtensionMkv).ToList();
				segmentedVideos.Sort();
				
				LogHelper.LogInformation($"Successfully segmented video into {segmentedVideos.Count} chunks", 
					nameof(FFmpegHelper), nameof(SegmentVideoAsync));
				
				return segmentedVideos;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while segmenting video {videoPath}: {ex.Message}", 
					nameof(FFmpegHelper), nameof(SegmentVideoAsync), ex);
				return new List<string>();
			}
		}

		/// <summary>
		/// Executes FFmpeg command with the specified parameters.
		/// </summary>
		private async Task RunFFmpegAsync(string videoPath, string outputFilePath, TimeSpan? segmentDuration, 
			FFMPEG_MODE mode = FFMPEG_MODE.FRAME_SEGMENT, int frameInterval = 1)
		{
			try
			{
				// Set the FFmpeg executables path from the application base directory
				string ffmpegPath = AppDomain.CurrentDomain.BaseDirectory;
				FFmpeg.SetExecutablesPath(ffmpegPath);

				var conversion = FFmpeg.Conversions.New();

				if (mode == FFMPEG_MODE.VIDEO_SEGMENT)
				{
					BuildVideoSegmentationCommand(conversion, videoPath, outputFilePath, segmentDuration);
				}
				else if (mode == FFMPEG_MODE.FRAME_SEGMENT)
				{
					BuildFrameExtractionCommand(conversion, videoPath, outputFilePath, frameInterval);
				}

				var result = await conversion.Start();
				LogHelper.LogInformation($"FFmpeg execution completed. Arguments: {result.Arguments}", 
					nameof(FFmpegHelper), nameof(RunFFmpegAsync));
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"FFmpeg execution failed: {ex.Message}", 
					nameof(FFmpegHelper), nameof(RunFFmpegAsync), ex);
				throw;
			}
		}

		/// <summary>
		/// Builds FFmpeg command parameters for video segmentation.
		/// </summary>
		private static void BuildVideoSegmentationCommand(IConversion conversion, string videoPath, 
			string outputFilePath, TimeSpan? segmentDuration)
		{
			string framePattern = Path.Combine(outputFilePath, $"output_%010d.mkv");
			conversion.AddParameter($"-i \"{videoPath}\"")
				.AddParameter($"-c:v ffv1")
				.AddParameter($"-c:a copy")
				.AddParameter($"-map 0")
				.AddParameter($"-segment_time {segmentDuration?.TotalSeconds}")
				.AddParameter($"-force_key_frames \"expr:gte(t,n_forced*{segmentDuration?.TotalSeconds})\"")
				.AddParameter($"-f segment")
				.AddParameter($"-reset_timestamps 1")
				.SetOutput(framePattern);
		}

		/// <summary>
		/// Builds FFmpeg command parameters for frame extraction.
		/// </summary>
		private static void BuildFrameExtractionCommand(IConversion conversion, string videoPath, 
			string outputFilePath, int frameInterval)
		{
			string framePattern = Path.Combine(outputFilePath, $"frame_%010d.jpg");
			conversion.AddParameter($"-i \"{videoPath}\"")
				.AddParameter($"-vf \"fps=1/{frameInterval}\"")
				.AddParameter($"-compression_level 0")
				.AddParameter($"-fs {MaxFileSize}")
				.SetOutput(framePattern);
		}
	}
}
