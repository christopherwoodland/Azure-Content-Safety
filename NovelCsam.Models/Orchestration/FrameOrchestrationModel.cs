using NovelCsam.Models.Interfaces.Orchestration;

namespace NovelCsam.Models.Orchestration
{
    public class FrameOrchestrationModel : IFrameOrchestrationModel
    {
        [JsonProperty(PropertyName = "containerName")]
        public string ContainerName { get; set; }
        [JsonProperty(PropertyName = "containerDirectory")]
        public string ContainerDirectory { get; set; }
        [JsonProperty(PropertyName = "imageBase64ToDB")]
        public bool ImageBase64ToDB { get; set; }
        [JsonProperty(PropertyName = "getSummary")]
        public bool GetSummary { get; set; }
        [JsonProperty(PropertyName = "getChildYesNo")]
        public bool GetChildYesNo { get; set; }
    }
}
