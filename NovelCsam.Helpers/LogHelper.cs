namespace NovelCsam.Helpers
{
	public static class LogHelper
	{
		private static readonly ILogger? _logger;

		static LogHelper()
		{

			var aie = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
			var dtc = Environment.GetEnvironmentVariable("DEBUG_TO_CONSOLE");
			if (!string.IsNullOrEmpty(aie))
			{
				// Create a new tracer provider builder and add an Azure Monitor trace exporter to the tracer provider builder.
				// It is important to keep the TracerProvider instance active throughout the process lifetime.
				// See https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/trace#tracerprovider-management
				var tracerProvider = Sdk.CreateTracerProviderBuilder()
					.AddAzureMonitorTraceExporter(options =>
					{
						options.ConnectionString = aie;
					});


				// Add an Azure Monitor metric exporter to the metrics provider builder.
				// It is important to keep the MetricsProvider instance active throughout the process lifetime.
				// See https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics#meterprovider-management
				var metricsProvider = Sdk.CreateMeterProviderBuilder()
					.AddAzureMonitorMetricExporter(options =>
					{
						options.ConnectionString = aie;
					});

				// Create a new logger factory.
				// It is important to keep the LoggerFactory instance active throughout the process lifetime.
				// See https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/logs#logger-management
				var resourceAttributes = new Dictionary<string, object>
			{
				{ "Custom Message", "message"},
				{ "sourceClassName", "sourceClassName" },
				{ "sourceFunction", "sourceFunction" }
			};
				var resourceBuilder = ResourceBuilder.CreateDefault().AddAttributes(resourceAttributes);
				var loggerFactory = LoggerFactory.Create(builder =>
				{
					//builder.AddConsole();
					builder.AddOpenTelemetry(options =>
					{
						options.SetResourceBuilder(resourceBuilder);
						options.AddAzureMonitorLogExporter(o => o.ConnectionString = aie, null);
					});
				});
				_logger = loggerFactory.CreateLogger("LogHelper");

			}
			else
			{
				var loggerFactory = LoggerFactory.Create(builder =>
				{
					if (!string.IsNullOrEmpty(dtc) && dtc.ToLower() == "true")
					{
						builder.AddConsole();
					}
				});

				_logger = loggerFactory.CreateLogger("LogHelper");
			}

		}

		#region Information Logging

		public static void LogInformation(string message, string sourceClassName, string sourceFunction)
		{
			Log(LogLevel.Information, message, sourceClassName, sourceFunction);
		}

		#endregion

		#region Exception Logging

		public static void LogException(string message, string sourceClassName, string sourceFunction, Exception ex)
		{
			var logMessage = $"\n{sourceClassName}:{sourceFunction}:{message}:{ex.Message}\n";
			_logger?.LogError(logMessage);
		}

		#endregion

		#region Trace Logging

		public static void LogTrace(string message, string sourceClassName, string sourceFunction)
		{
			Log(LogLevel.Trace, message, sourceClassName, sourceFunction);
		}

		#endregion

		private static void Log(LogLevel logLevel, string message, string sourceClassName, string sourceFunction)
		{
			var logMessage = $"\n{sourceClassName}:{sourceFunction}:{message}\n";
			_logger?.Log(logLevel, logMessage);
		}
	}
}