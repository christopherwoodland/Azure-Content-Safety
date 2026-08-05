using NovelCsam.Models.Interfaces.Orchestration;

namespace NovelCsam.Models.Orchestration
{
    public class AnalyzeFrameOrchestrationModel : IAnalyzeFrameOrchestrationModel
    {
        public string BlobPath { get; set; } = string.Empty;
        public string RunId { get; set; } = string.Empty;
        public DateTime RunDateTime { get; set; }

        public string ContainerName { get; set; } = string.Empty;
        public string ContainerDirectory { get; set; } = string.Empty;
        public bool ImageBase64ToDB { get; set; }
        public bool GetSummary { get; set; }
        public bool GetChildYesNo { get; set; }
    }
}
