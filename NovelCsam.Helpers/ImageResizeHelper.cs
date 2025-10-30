namespace NovelCsam.Helpers
{
	/// <summary>
	/// Utility class for image resizing and transformation operations.
	/// </summary>
	internal static class ImageResizeHelper
	{
		/// <summary>
		/// Resizes an image if it does not meet the specified dimension requirements.
		/// </summary>
		/// <param name="originalImage">The original image to potentially resize</param>
		/// <param name="minWidth">Minimum acceptable width</param>
		/// <param name="minHeight">Minimum acceptable height</param>
		/// <param name="maxWidth">Maximum acceptable width</param>
		/// <param name="maxHeight">Maximum acceptable height</param>
		/// <returns>The original image or a resized copy</returns>
		public static Image ResizeImageIfNeeded(Image originalImage, int minWidth, int minHeight, int maxWidth, int maxHeight)
		{
			try
			{
				int width = originalImage.Width;
				int height = originalImage.Height;

				if (width < minWidth || height < minHeight || width > maxWidth || height > maxHeight)
				{
					int newWidth = width < minWidth ? minWidth : (width > maxWidth ? maxWidth : width);
					int newHeight = height < minHeight ? minHeight : (height > maxHeight ? maxHeight : height);

					return ResizeImage(originalImage, newWidth, newHeight);
				}

				// Return a copy with the same dimensions as the original image
				var newImage = new Bitmap(originalImage.Width, originalImage.Height);
				using (var graphics = Graphics.FromImage(newImage))
				{
					graphics.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);
				}

				return newImage;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while resizing image: {ex.Message}", 
					nameof(ImageResizeHelper), nameof(ResizeImageIfNeeded), ex);
				throw;
			}
		}

		/// <summary>
		/// Resizes an image to specific dimensions using high-quality settings.
		/// </summary>
		/// <param name="image">The original image</param>
		/// <param name="width">Target width</param>
		/// <param name="height">Target height</param>
		/// <returns>The resized image</returns>
		public static Image ResizeImage(Image image, int width, int height)
		{
			try
			{
				var destRect = new Rectangle(0, 0, width, height);
				var destImage = new Bitmap(width, height);

				destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

				using (var graphics = Graphics.FromImage(destImage))
				{
					graphics.CompositingMode = CompositingMode.SourceCopy;
					graphics.CompositingQuality = CompositingQuality.HighQuality;
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

					using (var wrapMode = new ImageAttributes())
					{
						wrapMode.SetWrapMode(WrapMode.TileFlipXY);
						graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
					}
				}

				return destImage;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while resizing image to {width}x{height}: {ex.Message}", 
					nameof(ImageResizeHelper), nameof(ResizeImage), ex);
				throw;
			}
		}

		/// <summary>
		/// Resizes an image to fit within a maximum size while maintaining aspect ratio.
		/// </summary>
		/// <param name="image">The original image</param>
		/// <param name="maxSize">Maximum size in pixels</param>
		/// <returns>The resized image</returns>
		public static Image ResizeImageByMaxSize(Image image, int maxSize)
		{
			try
			{
				int newWidth = image.Width > image.Height ? maxSize : (int)(image.Width * (maxSize / (double)image.Height));
				int newHeight = image.Width > image.Height ? (int)(image.Height * (maxSize / (double)image.Width)) : maxSize;

				var resizedImage = new Bitmap(newWidth, newHeight);
				using (var graphics = Graphics.FromImage(resizedImage))
				{
					graphics.CompositingQuality = CompositingQuality.HighQuality;
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					graphics.DrawImage(image, 0, 0, newWidth, newHeight);
				}

				return resizedImage;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while resizing image to max size {maxSize}: {ex.Message}", 
					nameof(ImageResizeHelper), nameof(ResizeImageByMaxSize), ex);
				throw;
			}
		}
	}
}
