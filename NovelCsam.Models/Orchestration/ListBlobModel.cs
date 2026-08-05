using NovelCsam.Models.Interfaces.Orchestration;

namespace NovelCsam.Models.Orchestration
{
    public class ListBlobModel : IListBlobModel
	{
        public string ContainerName { get; set; } = string.Empty;
        public string ContainerDirectory { get; set; } = string.Empty;
    }
}
