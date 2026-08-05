param(
    [string]$FunctionAppName,
    [string]$ResourceGroup
)

# Add app settings
$AppSettings = @{
    "FUNCTIONS_WORKER_RUNTIME" = "dotnet-isolated"
    "FUNCTIONS_WORKER_RUNTIME_VERSION" = "10"

    "APPLICATIONINSIGHTS_CONNECTION_STRING" = ""
    "ANALYZE_FRAME_AZURE_FUNCTION_URL" = ""

    "STORAGE_USE_MANAGED_IDENTITY" = "true"
    "AZURE_STORAGE_CONNECTION_STRING" = ""
    "STORAGE_ACCOUNT_NAME" = "cwacstest001"
    "STORAGE_ACCOUNT_URL" = "https://cwacstest001.dfs.core.windows.net"

    "CONTENT_SAFETY_USE_MANAGED_IDENTITY" = "true"
    "CONTENT_SAFETY_ENDPOINT1" = ""
    "CONTENT_SAFETY_CONNECTION_KEY1" = "@Microsoft.KeyVault(SecretUri=)"
    "CONTENT_SAFETY_ENDPOINT2" = ""
    "CONTENT_SAFETY_CONNECTION_KEY2" = "@Microsoft.KeyVault(SecretUri=)"
    "CONTENT_SAFETY_ENDPOINT3" = ""
    "CONTENT_SAFETY_CONNECTION_KEY3" = "@Microsoft.KeyVault(SecretUri=)"

    "INVOKE_OPEN_AI" = "false"
    "OPEN_AI_USE_MANAGED_IDENTITY" = "true"
    "OPEN_AI_PROJECT_ENDPOINT" = ""
    "OPEN_AI_ENDPOINT" = ""
    "OPEN_AI_DEPLOYMENT_NAME" = ""
    "OPEN_AI_MODEL" = ""
    "OPEN_AI_TIMEOUT_SECONDS" = "2147483647"

    "RETRY_MAX_ATTEMPTS" = "3"
    "RETRY_BACKOFF_MULTIPLIER" = "2.0"
    "DURABLE_BATCH_SIZE" = "25"
    "DETAILED_ANALYSIS_PROMPT" = "Can you do a detail analysis and tell me all the minute details about this image. Use no more than 450 words!!!"
    "CHILD_DETECTION_PROMPT" = "Is there a younger person or child in this image? If you can't make a determination ANSWER No, ONLY ANSWER Yes or No!!"
}

foreach ($key in $AppSettings.Keys) {
    az functionapp config appsettings set --name $FunctionAppName --resource-group $ResourceGroup --settings "$key=$($AppSettings[$key])"
}
