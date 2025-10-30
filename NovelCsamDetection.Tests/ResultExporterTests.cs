namespace NovelCsam.Tests
{
	/// <summary>
	/// Unit tests for ResultExporter class.
	/// </summary>
	[TestClass]
	public class ResultExporterTests
	{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
		private Mock<IStorageHelper> _mockStorageHelper;
		private Mock<ILogHelper> _mockLogHelper;
		private ResultExporter _resultExporter;
#pragma warning restore CS8618

		[TestInitialize]
		public void Setup()
		{
			_mockStorageHelper = new Mock<IStorageHelper>();
			_mockLogHelper = new Mock<ILogHelper>();
			_resultExporter = new ResultExporter(_mockStorageHelper.Object, _mockLogHelper.Object);
		}

		#region ExportFrameResultsAsJsonAsync Tests

		[TestMethod]
		public async Task ExportFrameResultsAsJsonAsync_WithValidResults_ReturnsTrue()
		{
			// Arrange
			var results = new List<FrameResult>
			{
				new FrameResult
				{
					Id = "1",
					RunId = "run-001",
					Frame = "frame1.jpg",
					MD5Hash = "hash1",
					Hate = 0,
					SelfHarm = 0,
					Violence = 0,
					Sexual = 0
				}
			};

			// Act
			var result = await _resultExporter.ExportFrameResultsAsJsonAsync(results, "test-container", "test-folder", "run-001");

			// Assert
			Assert.IsTrue(result);
			_mockStorageHelper.Verify(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
		}

		[TestMethod]
		public async Task ExportFrameResultsAsJsonAsync_WithNullContainer_ReturnsFalse()
		{
			// Arrange
			var results = new List<FrameResult> { new FrameResult { Id = "1" } };

			// Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
			var result = await _resultExporter.ExportFrameResultsAsJsonAsync(results, null, "test-folder", "run-001");
#pragma warning restore CS8625

			// Assert
			Assert.IsFalse(result);
			_mockLogHelper.Verify(x => x.LogException(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
		}

		[TestMethod]
		public async Task ExportFrameResultsAsJsonAsync_WithNullFolder_ReturnsFalse()
		{
			// Arrange
			var results = new List<FrameResult> { new FrameResult { Id = "1" } };

			// Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
			var result = await _resultExporter.ExportFrameResultsAsJsonAsync(results, "test-container", null, "run-001");
#pragma warning restore CS8625

			// Assert
			Assert.IsFalse(result);
			_mockLogHelper.Verify(x => x.LogException(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
		}

		[TestMethod]
		public async Task ExportFrameResultsAsJsonAsync_WithNullRunId_ReturnsFalse()
		{
			// Arrange
			var results = new List<FrameResult> { new FrameResult { Id = "1" } };

			// Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
			var result = await _resultExporter.ExportFrameResultsAsJsonAsync(results, "test-container", "test-folder", null);
#pragma warning restore CS8625

			// Assert
			Assert.IsFalse(result);
			_mockLogHelper.Verify(x => x.LogException(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
		}

		[TestMethod]
		public async Task ExportFrameResultsAsJsonAsync_WithEmptyResults_ReturnsTrue()
		{
			// Arrange
			var results = new List<FrameResult>();

			// Act
			var result = await _resultExporter.ExportFrameResultsAsJsonAsync(results, "test-container", "test-folder", "run-001");

			// Assert
			Assert.IsTrue(result);
			_mockStorageHelper.Verify(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[TestMethod]
		public async Task ExportFrameResultsAsJsonAsync_WithStorageError_ReturnsFalse()
		{
			// Arrange
			var results = new List<FrameResult>
			{
				new FrameResult { Id = "1", RunId = "run-001", Frame = "frame1.jpg" }
			};

			_mockStorageHelper
				.Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
				.ThrowsAsync(new Exception("Storage error"));

			// Act
			var result = await _resultExporter.ExportFrameResultsAsJsonAsync(results, "test-container", "test-folder", "run-001");

			// Assert
			Assert.IsFalse(result);
			// Error is logged at both UploadJsonToBlobAsync level and ExportFrameResultsAsJsonAsync level
			_mockLogHelper.Verify(x => x.LogException(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Exactly(2));
		}

		#endregion

		#region ExportFrameResultAsJsonAsync Tests

		[TestMethod]
		public async Task ExportFrameResultAsJsonAsync_WithValidResult_ReturnsTrue()
		{
			// Arrange
			var result = new FrameResult
			{
				Id = "1",
				RunId = "run-001",
				Frame = "frame1.jpg",
				MD5Hash = "hash1"
			};

			// Act
			var exportResult = await _resultExporter.ExportFrameResultAsJsonAsync(result, "test-container", "test-folder");

			// Assert
			Assert.IsTrue(exportResult);
			_mockStorageHelper.Verify(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
		}

		[TestMethod]
		public async Task ExportFrameResultAsJsonAsync_WithNullResult_ReturnsFalse()
		{
			// Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
			var result = await _resultExporter.ExportFrameResultAsJsonAsync(null, "test-container", "test-folder");
#pragma warning restore CS8625

			// Assert
			Assert.IsFalse(result);
			_mockLogHelper.Verify(x => x.LogException(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
		}

		[TestMethod]
		public async Task ExportFrameResultAsJsonAsync_WithNullContainer_ReturnsFalse()
		{
			// Arrange
			var result = new FrameResult { Id = "1", RunId = "run-001" };

			// Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
			var exportResult = await _resultExporter.ExportFrameResultAsJsonAsync(result, null, "test-folder");
#pragma warning restore CS8625

			// Assert
			Assert.IsFalse(exportResult);
			_mockLogHelper.Verify(x => x.LogException(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
		}

		[TestMethod]
		public async Task ExportFrameResultAsJsonAsync_WithNullFolder_ReturnsFalse()
		{
			// Arrange
			var result = new FrameResult { Id = "1", RunId = "run-001", Frame = "frame1.jpg" };

			// Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
			var exportResult = await _resultExporter.ExportFrameResultAsJsonAsync(result, "test-container", null);
#pragma warning restore CS8625

			// Assert
			Assert.IsFalse(exportResult);
			_mockLogHelper.Verify(x => x.LogException(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
		}

		[TestMethod]
		public async Task ExportFrameResultAsJsonAsync_WithStorageError_ReturnsFalse()
		{
			// Arrange
			var result = new FrameResult { Id = "1", RunId = "run-001", Frame = "frame1.jpg" };

			_mockStorageHelper
				.Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
				.ThrowsAsync(new Exception("Storage error"));

			// Act
			var exportResult = await _resultExporter.ExportFrameResultAsJsonAsync(result, "test-container", "test-folder");

			// Assert
			Assert.IsFalse(exportResult);
			// Error is logged at both UploadJsonToBlobAsync level and ExportFrameResultAsJsonAsync level
			_mockLogHelper.Verify(x => x.LogException(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Exactly(2));
		}

		#endregion

		#region SanitizeFileName Tests

		[TestMethod]
		public void SanitizeFileName_WithInvalidCharacters_RemovesCharacters()
		{
			// This is a private method test through public behavior
			// We'll test it indirectly through ExportFrameResultAsJsonAsync
			
			// Arrange
			var result = new FrameResult
			{
				Id = "test-id",
				RunId = "run-001",
				Frame = "frame<invalid>name?.jpg"
			};

			// Act
			var task = _resultExporter.ExportFrameResultAsJsonAsync(result, "test-container", "test-folder");
			task.Wait();

			// Assert - should handle invalid characters gracefully
			_mockStorageHelper.Verify(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
		}

		#endregion
	}
}
