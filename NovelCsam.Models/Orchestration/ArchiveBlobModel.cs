namespace NovelCsam.Models.Orchestration
{
    public class ArchiveBlobModel
    {
        public string ContainerName { get; set; } = string.Empty;
        public string SourceBlobPath { get; set; } = string.Empty;
        public string DestinationBlobPath { get; set; } = string.Empty;
    }
}
