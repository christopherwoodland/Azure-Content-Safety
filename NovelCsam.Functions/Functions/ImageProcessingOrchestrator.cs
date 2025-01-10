using Newtonsoft.Json;
using NovelCsam.Models.Orchestration;

namespace NovelCsam.Functions.Functions
{


	public class ImageProcessingOrchestrator
	{
		private readonly ILogHelper _logHelper;
		private readonly IStorageHelper _sth;

		public ImageProcessingOrchestrator(IStorageHelper sth, ILogHelper logHelper)
		{
			_logHelper = logHelper;
			_sth = sth;
		}

		[Function(nameof(ImageProcessingOrchestrator))]
		public async Task<string> RunOrchestrator(
			[OrchestrationTrigger] TaskOrchestrationContext context)
		{
			try
			{
				_logHelper.LogInformation("ImageProcessingOrchestrator Started", nameof(ImageProcessingOrchestrator), nameof(RunOrchestrator));
				var fom = JsonConvert.DeserializeObject<FrameOrchestrationModel>(context.GetInput<string>() ?? "");
				if (fom != null)
				{
					AnalyzeFrameOrchestrationModel afm = new()
					{
						GetSummary = fom.GetSummary,
						GetChildYesNo = fom.GetChildYesNo,
						ContainerDirectory = fom.ContainerDirectory,
						ContainerName = fom.ContainerName,
						ImageBase64ToDB = fom.ImageBase64ToDB
					};

					//var frames = await _sth.ListBlobsInFolderWithResizeAsync(fom.ContainerName, fom.ContainerDirectory, 3);
					var frames = await context.CallActivityAsync<Dictionary<string, CustomBinaryData>>("ListBlobs", new { fom.ContainerName, fom.ContainerDirectory });


					var runId = Guid.NewGuid().ToString();
					var runDateTime = DateTime.UtcNow;
					afm.RunId = runId;
					afm.RunDateTime = runDateTime;
					var withBase64ofImage = fom.ImageBase64ToDB;
					var getSummaryB = fom.GetSummary;
					var getChildYesNoB = fom.GetChildYesNo;
					var tasks = new List<Task<bool>>();



					// Fan-Out: Start multiple tasks in parallel to resize the image in different resolutions
					foreach (var frame in frames)
					{
						afm.Frame = frame.Value;
						//afm.Frame.Key = frame.Key;
						var task = context.CallActivityAsync<bool>("AnalyzeFrame", afm);
						tasks.Add(task);
					}
					// Wait for all tasks to complete (Fan-In)
					await Task.WhenAll(tasks);

					return runId;
				}
				else
				{
					_logHelper.LogInformation($"Frame model is null", nameof(ImageProcessingOrchestrator), nameof(RunOrchestrator));
					return "";
				}
			}
			catch (Exception ex)
			{
				_logHelper.LogException($"An error occurred when processing an image: {ex.Message}", nameof(ImageProcessingOrchestrator), nameof(RunOrchestrator), ex);
				return "";
			}
		}

		[Function("AnalyzeFrames_HttpStart")]
		public static async Task<HttpResponseData> HttpStart(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
			[DurableClient] DurableTaskClient client,
			FunctionContext executionContext)
		{
			ILogger logger = executionContext.GetLogger("Function1_HttpStart");
			string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

			// Function input comes from the request content.
			string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
				nameof(ImageProcessingOrchestrator), requestBody);

			logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

			// Returns an HTTP 202 response with an instance management payload.
			// See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
			return await client.CreateCheckStatusResponseAsync(req, instanceId);
		}
	}
}
