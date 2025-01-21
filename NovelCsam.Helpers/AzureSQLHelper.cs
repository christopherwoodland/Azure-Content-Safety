namespace NovelCsam.Helpers
{
	public class AzureSQLHelper : IAzureSQLHelper
	{
		#region Private Fields
		private readonly string _connectionString;
		private readonly ILogHelper _logHelper;
		private readonly AsyncRetryPolicy _retryPolicy;
		#endregion

		#region Constructor
		public AzureSQLHelper()
		{
			_connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING") ?? "";

			_retryPolicy = Policy
				.Handle<SqlException>(ex => ex.Number == -2) // SQL Server timeout error number
				.Or<SqlException>(ex => ex.Number == 1205) // SQL Server deadlock error number
				.Or<SqlException>(ex => ex.Number == 40613) // SQL Server database unavailable error number
				.Or<SqlException>(ex => ex.Number == 40501) //The service is currently busy. Retry the request after 10 seconds. Incident ID: %ls. Code: %d.
				.Or<SqlException>(ex => ex.Number == 49919) //Cannot process create or update request. Too many create or update operations in progress for subscription "%ld".
				.Or<SqlException>(ex => ex.Number == 49920) //Cannot process request. Too many operations in progress for subscription "%ld".
				.WaitAndRetryAsync(10, retryAttempt => TimeSpan.FromSeconds(Math.Pow(3, retryAttempt)),
					(exception, timeSpan, retryCount, context) =>
					{
						LogHelper.LogInformation($"Retry {retryCount} encountered an error: {exception.Message}. Waiting {timeSpan} before next retry.", nameof(AzureSQLHelper), "Constructor");
					});
		}
		#endregion

		#region Public Methods
		public async Task<IFrameResult?> CreateFrameResult(IFrameResult item)
		{
			string query = @"
                INSERT INTO FrameResults (
                    Id,
                    Summary,
                    ChildYesNo,
                    MD5Hash,
                    Frame,
                    RunId,
                    Hate,
                    SelfHarm,
                    Violence,
                    Sexual,
                    RunDateTime
                ) VALUES (
                    @Id,
                    @Summary,
                    @ChildYesNo,
                    @MD5Hash,
                    @Frame,
                    @RunId,
                    @Hate,
                    @SelfHarm,
                    @Violence,
                    @Sexual,
                    @RunDateTime
                )";

			return await ExecuteNonQueryAsync(query, item);
		}

		public async Task<IFrameResult?> InsertBase64(IFrameResult item)
		{
			string query = @"
                INSERT INTO FrameBase64 (
                    Id,
                    Frame,
                    ImageBase64,
                    RunDateTime,
                    RunId
                ) VALUES (
                    @Id,
                    @Frame,
                    @ImageBase64,
                    @RunDateTime,
                    @RunId
                )";

			return await ExecuteNonQueryAsync(query, item);
		}

		public async Task<List<IFrameDetailResult>?> GetFrameResultWithLevelsAsync(string frameId)
		{
			string query = @"
                DECLARE @inputString NVARCHAR(MAX) = @FrameId;

                WITH SplitStrings AS
                (
                    SELECT value, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS PartNumber
                    FROM STRING_SPLIT(@inputString, '/')
                ),
                FirstThreeParts AS
                (
                    SELECT 
                        STRING_AGG(value, '/') WITHIN GROUP (ORDER BY PartNumber) AS FirstThreeParts
                    FROM SplitStrings
                    WHERE PartNumber <= 3
                )
                SELECT 
                    *,
                    CASE 
                        WHEN Hate >= 0 AND Hate <= 1 THEN 'none'
                        WHEN Hate >= 2 AND Hate <= 3 THEN 'low'
                        WHEN Hate >= 4 AND Hate <= 5 THEN 'medium'
                        WHEN Hate >= 6 THEN 'high'
                    END AS HateLevel,
                    SelfHarm,
                    CASE 
                        WHEN SelfHarm >= 0 AND SelfHarm <= 1 THEN 'none'
                        WHEN SelfHarm >= 2 AND SelfHarm <= 3 THEN 'low'
                        WHEN SelfHarm >= 4 AND SelfHarm <= 5 THEN 'medium'
                        WHEN SelfHarm >= 6 THEN 'high'
                    END AS SelfHarmLevel,
                    Violence,
                    CASE 
                        WHEN Violence >= 0 AND Violence <= 1 THEN 'none'
                        WHEN Violence >= 2 AND Violence <= 3 THEN 'low'
                        WHEN Violence >= 4 AND Violence <= 5 THEN 'medium'
                        WHEN Violence >= 6 THEN 'high'
                    END AS ViolenceLevel,
                    Sexual,
                    CASE 
                        WHEN Sexual >= 0 AND Sexual <= 1 THEN 'none'
                        WHEN Sexual >= 2 AND Sexual <= 3 THEN 'low'
                        WHEN Sexual >= 4 AND Sexual <= 5 THEN 'medium'
                        WHEN Sexual >= 6 THEN 'high'
                    END AS SexualLevel,
                    RunDateTime
                FROM 
                    FrameResults
                WHERE 
                    Frame LIKE (SELECT FirstThreeParts + '%'
                                FROM FirstThreeParts);";

			var parameters = new Dictionary<string, object>
			{
				{ "@FrameId", frameId }
			};

			return await ExecuteReaderAsync(query, parameters);
		}
		#endregion

		#region Private Methods
		private async Task<List<IFrameDetailResult>> ExecuteReaderAsync(string query, Dictionary<string, object> parameters)
		{
			return await _retryPolicy.ExecuteAsync(async () =>
			{
				var results = new List<IFrameDetailResult>();

				using (var connection = new SqlConnection(_connectionString))
				{
					await connection.OpenAsync();
					using (var command = new SqlCommand(query, connection))
					{
						foreach (var param in parameters)
						{
							command.Parameters.AddWithValue(param.Key, param.Value);
						}

						using (var reader = await command.ExecuteReaderAsync())
						{
							while (await reader.ReadAsync())
							{
								var result = new FrameDetailResult
								{
									Id = reader["Id"].ToString(),
									RunId = reader["RunId"].ToString(),
									Summary = reader["Summary"].ToString(),
									ChildYesNo = reader["ChildYesNo"].ToString(),
									MD5Hash = reader["MD5Hash"].ToString(),
									Frame = reader["Frame"].ToString(),
									Hate = Convert.ToInt32(reader["Hate"]),
									HateLevel = reader["HateLevel"].ToString(),
									SelfHarm = Convert.ToInt32(reader["SelfHarm"]),
									SelfHarmLevel = reader["SelfHarmLevel"].ToString(),
									Violence = Convert.ToInt32(reader["Violence"]),
									ViolenceLevel = reader["ViolenceLevel"].ToString(),
									Sexual = Convert.ToInt32(reader["Sexual"]),
									SexualLevel = reader["SexualLevel"].ToString(),
									RunDateTime = Convert.ToDateTime(reader["RunDateTime"])
								};

								results.Add(result);
							}
						}
					}
				}

				return results;
			});
		}

		private async Task<IFrameResult?> ExecuteNonQueryAsync(string query, IFrameResult item)
		{
			return await _retryPolicy.ExecuteAsync(async () =>
			{
				try
				{
					using (SqlConnection connection = new(_connectionString))
					{
						await connection.OpenAsync();

						using SqlCommand command = new(query, connection);
						command.Parameters.AddWithValue("@Id", item.Id);
						command.Parameters.AddWithValue("@Frame", item.Frame);
						command.Parameters.AddWithValue("@RunId", item.RunId);
						command.Parameters.AddWithValue("@RunDateTime", item.RunDateTime);

						if (query.Contains("FrameResults"))
						{
							command.Parameters.AddWithValue("@Summary", item.Summary);
							command.Parameters.AddWithValue("@ChildYesNo", item.ChildYesNo);
							command.Parameters.AddWithValue("@MD5Hash", item.MD5Hash);
							command.Parameters.AddWithValue("@Hate", item.Hate);
							command.Parameters.AddWithValue("@SelfHarm", item.SelfHarm);
							command.Parameters.AddWithValue("@Violence", item.Violence);
							command.Parameters.AddWithValue("@Sexual", item.Sexual);
						}
						else if (query.Contains("FrameBase64"))
						{
							command.Parameters.AddWithValue("@ImageBase64", item.ImageBase64);
						}

						await command.ExecuteNonQueryAsync();
					}
					return item;
				}
				catch (Exception ex)
				{
					LogHelper.LogException(ex.Message, nameof(AzureSQLHelper), nameof(ExecuteNonQueryAsync), ex);
					return null;
				}
			});
		}
		#endregion
	}
}