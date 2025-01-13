namespace NovelCsam.Models.Interfaces.Orchestration
{
    public interface IAnalyzeFrameOrchestrationModel
    {
        CustomBinaryData Frame { get; set; }
        string? RunId { get; set; }
        DateTime RunDateTime { get; set; }

        string ContainerName { get; set; }
        string ContainerDirectory { get; set; }
        bool ImageBase64ToDB { get; set; }
        bool GetSummary { get; set; }
        bool GetChildYesNo { get; set; }

    }
}
