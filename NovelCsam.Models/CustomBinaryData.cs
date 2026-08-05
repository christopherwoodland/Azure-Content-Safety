namespace NovelCsam.Models
{
	public class CustomBinaryData
	{
		[JsonProperty("data")]
		public byte[] Data { get; set; } = Array.Empty<byte>();
		[JsonProperty("key")]
		public string Key { get; set; } = string.Empty;
		public CustomBinaryData() { }
		[JsonConstructor]
		public CustomBinaryData(byte[] data)
		{
			Data = data;
			Key = "";
		}

		public CustomBinaryData(byte[] data, string key)
		{
			Data = data;
			Key = key;
		}
	}
}
