using NovelCsam.Models.Interfaces.Orchestration;

namespace NovelCsam.Models.Orchestration
{
    public class AnalyzeFrameOrchestrationModel : IAnalyzeFrameOrchestrationModel
    {
        public CustomBinaryData Frame { get; set; }
        public string RunId { get; set; }
        public DateTime RunDateTime { get; set; }

        public string ContainerName { get; set; }
        public string ContainerDirectory { get; set; }
        public bool ImageBase64ToDB { get; set; }
        public bool GetSummary { get; set; }
        public bool GetChildYesNo { get; set; }
    }
}
