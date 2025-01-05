namespace NovelCsam.Helpers.Interfaces
{
    /// <summary>
    /// Defines a contract for logging helper objects.
    /// </summary>
    public interface ILogHelper
    {
        /// <summary>
        /// Logs information with the specified message, source class name, and source function.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        /// <param name="sourceClassName">The name of the source class.</param>
        /// <param name="sourceFunction">The name of the source function.</param>
        /// <returns>A task representing the synchronous logging operation.</returns>
        void LogInformation(string message, string sourceClassName, string sourceFunction);

        /// <summary>
        /// Logs an exception synchronously.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="sourceClassName">The name of the source class.</param>
        /// <param name="sourceFunction">The name of the source function.</param>
        /// <param name="ex">The exception to be logged.</param>
        void LogException(string message, string sourceClassName, string sourceFunction, Exception ex);

        /// <summary>
        /// Logs a trace message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="sourceClassName">The name of the source class.</param>
        /// <param name="sourceFunction">The name of the source function.</param>
        void LogTrace(string message, string sourceClassName, string sourceFunction);
    }
}
