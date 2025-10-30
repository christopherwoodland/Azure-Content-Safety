namespace NovelCsam.Tests
{
	/// <summary>
	/// Unit tests for AnalyzeFrame configuration and storage options.
	/// Tests the SQL persistence and JSON export flag behavior.
	/// </summary>
	[TestClass]
	public class AnalyzeFrameConfigurationTests
	{
		/// <summary>
		/// Test that FunctionSettings properly loads SQL persistence configuration from environment variables.
		/// </summary>
		[TestMethod]
		public void FunctionSettings_EnableSqlPersistence_DefaultIsTrue()
		{
			// Arrange & Act
			var settings = new FunctionSettings();

			// Assert
			Assert.IsTrue(settings.EnableSqlPersistence, "SQL persistence should be enabled by default");
		}

		/// <summary>
		/// Test that FunctionSettings properly loads JSON export configuration from environment variables.
		/// </summary>
		[TestMethod]
		public void FunctionSettings_EnableJsonExport_DefaultIsFalse()
		{
			// Arrange & Act
			var settings = new FunctionSettings();

			// Assert
			Assert.IsFalse(settings.EnableJsonExport, "JSON export should be disabled by default");
		}

		/// <summary>
		/// Test that all three storage options are properly configured.
		/// </summary>
		[TestMethod]
		public void FunctionSettings_AllStorageOptions_Initialized()
		{
			// Arrange & Act
			var settings = new FunctionSettings();

			// Assert - Verify all storage options exist and have values
			Assert.IsTrue(settings.EnableSqlPersistence, "SQL persistence flag should exist");
			Assert.IsTrue(settings.EnableJsonExport == false, "JSON export flag should exist");
			Assert.AreEqual("results", settings.JsonExportContainerName, "Container name should have default");
			Assert.AreEqual("json-results", settings.JsonExportFolderPath, "Folder path should have default");
		}

		/// <summary>
		/// Test storage combination: SQL only (default/traditional mode).
		/// </summary>
		[TestMethod]
		public void StorageMode_SqlOnly_BothFlagsConfigured()
		{
			// Arrange & Act
			var settings = new FunctionSettings
			{
				EnableSqlPersistence = true,
				EnableJsonExport = false
			};

			// Assert
			Assert.IsTrue(settings.EnableSqlPersistence, "SQL should be enabled");
			Assert.IsFalse(settings.EnableJsonExport, "JSON should be disabled");
		}

		/// <summary>
		/// Test storage combination: SQL + JSON (dual storage mode).
		/// </summary>
		[TestMethod]
		public void StorageMode_SqlAndJson_BothFlagsConfigured()
		{
			// Arrange & Act
			var settings = new FunctionSettings
			{
				EnableSqlPersistence = true,
				EnableJsonExport = true
			};

			// Assert
			Assert.IsTrue(settings.EnableSqlPersistence, "SQL should be enabled");
			Assert.IsTrue(settings.EnableJsonExport, "JSON should be enabled");
		}

		/// <summary>
		/// Test storage combination: JSON only (new blob-only mode).
		/// </summary>
		[TestMethod]
		public void StorageMode_JsonOnly_BothFlagsConfigured()
		{
			// Arrange & Act
			var settings = new FunctionSettings
			{
				EnableSqlPersistence = false,
				EnableJsonExport = true
			};

			// Assert
			Assert.IsFalse(settings.EnableSqlPersistence, "SQL should be disabled");
			Assert.IsTrue(settings.EnableJsonExport, "JSON should be enabled");
		}

		/// <summary>
		/// Test that JSON export container and folder names are properly configured.
		/// </summary>
		[TestMethod]
		public void FunctionSettings_JsonExportPaths_ProperlyConfigured()
		{
			// Arrange & Act
			var settings = new FunctionSettings
			{
				JsonExportContainerName = "custom-results",
				JsonExportFolderPath = "custom-folder/2025"
			};

			// Assert
			Assert.AreEqual("custom-results", settings.JsonExportContainerName);
			Assert.AreEqual("custom-folder/2025", settings.JsonExportFolderPath);
		}

		/// <summary>
		/// Test that FromEnvironment() properly loads EnableSqlPersistence when set to true.
		/// </summary>
		[TestMethod]
		public void FunctionSettings_FromEnvironment_LoadsSqlPersistenceTrue()
		{
			// Arrange
			Environment.SetEnvironmentVariable("ENABLE_SQL_PERSISTENCE", "true");

			try
			{
				// Act
				var settings = FunctionSettings.FromEnvironment();

				// Assert
				Assert.IsTrue(settings.EnableSqlPersistence);
			}
			finally
			{
				// Cleanup
				Environment.SetEnvironmentVariable("ENABLE_SQL_PERSISTENCE", null);
			}
		}

		/// <summary>
		/// Test that FromEnvironment() properly loads EnableSqlPersistence when set to false.
		/// </summary>
		[TestMethod]
		public void FunctionSettings_FromEnvironment_LoadsSqlPersistenceFalse()
		{
			// Arrange
			Environment.SetEnvironmentVariable("ENABLE_SQL_PERSISTENCE", "false");

			try
			{
				// Act
				var settings = FunctionSettings.FromEnvironment();

				// Assert
				Assert.IsFalse(settings.EnableSqlPersistence);
			}
			finally
			{
				// Cleanup
				Environment.SetEnvironmentVariable("ENABLE_SQL_PERSISTENCE", null);
			}
		}

		/// <summary>
		/// Test that FromEnvironment() loads JSON export settings correctly.
		/// </summary>
		[TestMethod]
		public void FunctionSettings_FromEnvironment_LoadsJsonExportSettings()
		{
			// Arrange
			Environment.SetEnvironmentVariable("ENABLE_JSON_EXPORT", "true");
			Environment.SetEnvironmentVariable("JSON_EXPORT_CONTAINER_NAME", "test-results");
			Environment.SetEnvironmentVariable("JSON_EXPORT_FOLDER_PATH", "test-folder");

			try
			{
				// Act
				var settings = FunctionSettings.FromEnvironment();

				// Assert
				Assert.IsTrue(settings.EnableJsonExport);
				Assert.AreEqual("test-results", settings.JsonExportContainerName);
				Assert.AreEqual("test-folder", settings.JsonExportFolderPath);
			}
			finally
			{
				// Cleanup
				Environment.SetEnvironmentVariable("ENABLE_JSON_EXPORT", null);
				Environment.SetEnvironmentVariable("JSON_EXPORT_CONTAINER_NAME", null);
				Environment.SetEnvironmentVariable("JSON_EXPORT_FOLDER_PATH", null);
			}
		}

		/// <summary>
		/// Test that invalid ENABLE_SQL_PERSISTENCE values fall back to default (true).
		/// </summary>
		[TestMethod]
		public void FunctionSettings_FromEnvironment_InvalidSqlPersistenceFallsbackToDefault()
		{
			// Arrange
			Environment.SetEnvironmentVariable("ENABLE_SQL_PERSISTENCE", "invalid");

			try
			{
				// Act
				var settings = FunctionSettings.FromEnvironment();

				// Assert
				Assert.IsTrue(settings.EnableSqlPersistence, "Should fallback to default (true) on invalid value");
			}
			finally
			{
				// Cleanup
				Environment.SetEnvironmentVariable("ENABLE_SQL_PERSISTENCE", null);
			}
		}

		/// <summary>
		/// Test that missing ENABLE_SQL_PERSISTENCE uses default value (true).
		/// </summary>
		[TestMethod]
		public void FunctionSettings_FromEnvironment_MissingSqlPersistenceUsesDefault()
		{
			// Arrange
			Environment.SetEnvironmentVariable("ENABLE_SQL_PERSISTENCE", null);

			// Act
			var settings = FunctionSettings.FromEnvironment();

			// Assert
			Assert.IsTrue(settings.EnableSqlPersistence, "Should use default (true) when not set");
		}
	}
}
