public class OrchestrationStatus
{
	public string Name { get; set; } = string.Empty;
	public string InstanceId { get; set; } = string.Empty;
	public string RuntimeStatus { get; set; } = string.Empty;
	public string Input { get; set; } = string.Empty;
	public object? CustomStatus { get; set; }
	public object? Output { get; set; }
	public DateTime CreatedTime { get; set; }
	public DateTime LastUpdatedTime { get; set; }
}