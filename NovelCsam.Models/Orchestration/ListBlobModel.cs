using NovelCsam.Models.Interfaces.Orchestration;

namespace NovelCsam.Models.Orchestration
{
    public class ListBlobModel : IListBlobModel
	{
        public string ContainerName { get; set; }
        public string ContainerDirectory { get; set; }
    }
}
