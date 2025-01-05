namespace NovelCsam.Helpers
{
	public class CosmosDBHelper : ICosmosDBHelper
	{
		#region Private Fields

		private readonly CosmosClient _cosmosClient;
		private readonly Container _container;
		private readonly ILogHelper _logHelper;

		#endregion

		#region Constructor

		public CosmosDBHelper(ILogHelper logHelper)
		{
			_logHelper = logHelper;


			var cdbcs = Environment.GetEnvironmentVariable("COSMOS_DB_CONNECTION_STRING");
			var cdbname = Environment.GetEnvironmentVariable("COSMOS_DB_DATABASE_NAME");
			var cdbcname = Environment.GetEnvironmentVariable("COSMOS_DB_CONTAINER_NAME");

			_cosmosClient = new CosmosClient(cdbcs);
			_cosmosClient.CreateDatabaseIfNotExistsAsync(cdbname);
			_container = _cosmosClient.GetContainer(cdbname, cdbcname);
		}
		#endregion

		#region Public Methods
		public async Task<IFrameResult?> CreateFrameResult(IFrameResult item)
		{
			try
			{
				await _container.CreateItemAsync(item);
				return item;
			}
			catch (Exception ex)
			{
				_logHelper.LogException(ex.Message, nameof(CosmosDBHelper),
				  nameof(CreateFrameResult), ex);
				return null;
			}
		}

		#endregion
	}
}
