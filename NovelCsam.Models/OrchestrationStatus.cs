
public class OrchestrationStatus
{
	public string Name { get; set; }
	public string InstanceId { get; set; }
	public string RuntimeStatus { get; set; }
	public string Input { get; set; }
	public object CustomStatus { get; set; }
	public object Output { get; set; }
	public DateTime CreatedTime { get; set; }
	public DateTime LastUpdatedTime { get; set; }
}