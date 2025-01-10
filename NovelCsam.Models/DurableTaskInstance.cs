public class DurableTaskInstance
{
	public string Id { get; set; }
	public string PurgeHistoryDeleteUri { get; set; }
	public string SendEventPostUri { get; set; }
	public string StatusQueryGetUri { get; set; }
	public string TerminatePostUri { get; set; }
	public string SuspendPostUri { get; set; }
	public string ResumePostUri { get; set; }
}