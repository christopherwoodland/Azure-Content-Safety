public class DurableTaskInstance
{
	public string Id { get; set; } = string.Empty;
	public string PurgeHistoryDeleteUri { get; set; } = string.Empty;
	public string SendEventPostUri { get; set; } = string.Empty;
	public string StatusQueryGetUri { get; set; } = string.Empty;
	public string TerminatePostUri { get; set; } = string.Empty;
	public string SuspendPostUri { get; set; } = string.Empty;
	public string ResumePostUri { get; set; } = string.Empty;
}