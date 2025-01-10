namespace NovelCsam.Models.Interfaces.Orchestration
{
    public interface IFrameOrchestrationModel
    {
        [JsonProperty(PropertyName = "containerName")]
        public string ContainerName { get; set; }
        [JsonProperty(PropertyName = "containerDirectory")]
        string ContainerDirectory { get; set; }
        [JsonProperty(PropertyName = "imageBase64ToDB")]
        bool ImageBase64ToDB { get; set; }
        [JsonProperty(PropertyName = "getSummary")]
        bool GetSummary { get; set; }
        [JsonProperty(PropertyName = "getChildYesNo")]
        bool GetChildYesNo { get; set; }

    }
}
