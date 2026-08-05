namespace NovelCsam.Models.Interfaces.Orchestration
{
    public interface IListBlobModel
    {
        string ContainerName { get; set; }
		string ContainerDirectory { get; set; }
    }
}
