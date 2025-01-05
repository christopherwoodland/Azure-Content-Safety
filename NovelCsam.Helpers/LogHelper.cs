using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;

namespace NovelCsam.Helpers
{
	public class LogHelper : ILogHelper
	{
		private readonly ILogger<LogHelper> _logger;
		private readonly TelemetryClient _telemetryClient;

		public LogHelper(ILogger<LogHelper> logger, TelemetryClient telemetryClient)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
		}

		#region Information Logging

		public void LogInformation(string message, string sourceClassName, string sourceFunction)
		{
			Log(LogLevel.Information, message, sourceClassName, sourceFunction);
		}

		#endregion

		#region Exception Logging

		public void LogException(string message, string sourceClassName, string sourceFunction, Exception ex)
		{
			var logMessage = $"{sourceClassName}:{sourceFunction}:{message}:{ex.Message}";
			_logger.LogError(logMessage);
			_telemetryClient.TrackException(ex, new Dictionary<string, string>
			{
				{ "Custom Message", message },
				{ "sourceClassName", sourceClassName },
				{ "sourceFunction", sourceFunction }
			});
		}

		#endregion

		#region Trace Logging

		public void LogTrace(string message, string sourceClassName, string sourceFunction)
		{
			Log(LogLevel.Trace, message, sourceClassName, sourceFunction);
		}

		#endregion

		private void Log(LogLevel logLevel, string message, string sourceClassName, string sourceFunction)
		{
			var logMessage = $"{sourceClassName}:{sourceFunction}:{message}";
			_logger.Log(logLevel, logMessage);
			_telemetryClient.TrackTrace(logMessage, new Dictionary<string, string>
			{
				{ "Custom Message", message },
				{ "sourceClassName", sourceClassName },
				{ "sourceFunction", sourceFunction }
			});
		}
	}
}
