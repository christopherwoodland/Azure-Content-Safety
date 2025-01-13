param(
    [string]$FunctionAppName,
    [string]$ResourceGroup
)

# Add app settings
$AppSettings = @{
    "AnalyzeFrameAzureFunctionUrl" = ""
    "AppInsightsConnectionString" = ""
    "ContentSafetyConnectionKey" = ""
    "ContentSafetyConnectionString" = ""
    "OpenAiDeploymentName" = ""
    "OpenAiEndpoint" = ""
    "OpenAiKey" = ""
    "OpenAiModel" = ""
    "SqlConnectionString" = ""
    "StorageAccountKey" = ""
    "StorageAccountName" = ""
    "StorageAccountUrl" = ""
}

foreach ($key in $AppSettings.Keys) {
    az functionapp config appsettings set --name $FunctionAppName --resource-group $ResourceGroup --settings "$key=$($AppSettings[$key])"
}
