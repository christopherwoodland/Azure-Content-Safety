# Security Guidelines

This document outlines security practices and sensitive file protection for the Novel CSAM Detection project.

## Protected Files

All sensitive configuration and secret files are protected through `.gitignore`. These files must **never** be committed to version control:

### Local Settings & Configuration

| File Path | Purpose | Contains |
|-----------|---------|----------|
| `NovelCsam.Functions/local.settings.json` | Azure Functions local development settings | Connection strings, API keys, credentials |
| `NovelCsam.Functions/appsettings.json` | Azure Functions application settings | Runtime configuration with secrets |
| `NovelCsam.UI.Console/appsettings.json` | Console app configuration | Database and service credentials |
| `*.env` | Environment variable files | Local environment configuration |
| `*.env.local` | Local-specific environment variables | Machine-specific settings |

### User Secrets

| Directory | Purpose |
|-----------|---------|
| `**/Properties/launchSettings.json` | Visual Studio launch profiles (may contain secrets) |
| `**/UserSecrets/` | Visual Studio User Secrets (protected credentials) |

### Certificates & Keys

| File Pattern | Purpose |
|--------------|---------|
| `*.pfx` | SSL certificates with private keys |
| `*.pem` | PEM-encoded certificates/keys |
| `*.key` | Private key files |
| `*.crt` | Certificate files |

### Deployment Scripts

| File | Purpose | Contains |
|------|---------|----------|
| `Infrastructure/Azure Functions/Set-AppSettings.ps1` | PowerShell deployment script | May contain credentials |

## Sensitive Data in Configuration

### Connection Strings (Never Commit)

```
❌ DO NOT COMMIT:
- Azure SQL connection strings with credentials
- Azure Storage account keys
- Azure Cosmos DB connection strings
- Azure Key Vault access credentials
```

### API Keys (Never Commit)

```
❌ DO NOT COMMIT:
- Azure Content Safety API keys
- Azure OpenAI API keys
- Application Insights instrumentation keys
- Any service-specific API keys
```

### Environment Variables (Never Commit)

```
❌ DO NOT COMMIT:
- OPEN_AI_KEY
- CONTENT_SAFETY_CONNECTION_KEY
- AZURE_SQL_CONNECTION_STRING
- AZURE_STORAGE_CONNECTION_STRING
- KEY_VAULT_URL
```

## Working with Sensitive Data

### Local Development

1. **Use local.settings.json for Azure Functions:**
   - Copy from `local.settings.example.json`
   - Update with local service credentials
   - File is protected by `.gitignore`

2. **Use appsettings.json for Console App:**
   - Copy from `appsettings.example.json`
   - Update with local connection strings
   - File is protected by `.gitignore`

3. **Use User Secrets in Development (Recommended):**
   ```bash
   # Initialize user secrets
   dotnet user-secrets init
   
   # Set a secret
   dotnet user-secrets set "ConnectionStrings:SqlDatabase" "Server=localhost;..."
   
   # List all secrets
   dotnet user-secrets list
   ```

### Azure Deployment

1. **Use Azure Key Vault for Production:**
   - Store all secrets in Key Vault
   - Reference in code: `@Microsoft.KeyVault(SecretUri=...)`
   - Never store secrets in config files

2. **Use Azure Functions Application Settings:**
   - Set in Azure Portal or via Azure CLI
   - Settings are encrypted at rest
   - Reference in code: `Environment.GetEnvironmentVariable("KEY_NAME")`

3. **Use Managed Identities:**
   - Eliminate need for credential storage
   - Function App identity authenticates to services
   - No key rotation needed for authentication

## Best Practices

### ✅ DO

- ✅ Use `.gitignore` to prevent accidental commits
- ✅ Store secrets in Azure Key Vault (production)
- ✅ Use User Secrets for local development
- ✅ Rotate credentials regularly
- ✅ Audit access to sensitive files
- ✅ Use environment variables for configuration
- ✅ Review `.gitignore` before commits
- ✅ Use Managed Identities in Azure
- ✅ Enable Azure Key Vault logging and monitoring
- ✅ Follow principle of least privilege for service accounts

### ❌ DON'T

- ❌ Commit configuration files with secrets
- ❌ Hardcode credentials in source code
- ❌ Log sensitive data (keys, passwords, connection strings)
- ❌ Share secrets in chat, email, or documents
- ❌ Use the same credentials across environments
- ❌ Store secrets in repository documentation
- ❌ Commit `.env` files or similar
- ❌ Leave default or weak credentials
- ❌ Store secrets in version history
- ❌ Use personal/shared service accounts

## Example Configuration Files

### Example: local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_SQL_CONNECTION_STRING": "Server=localhost;Database=NovelCSAM;Trusted_Connection=yes;",
    "AZURE_STORAGE_CONNECTION_STRING": "UseDevelopmentStorage=true",
    "CONTENT_SAFETY_CONNECTION_STRING": "Endpoint=https://myservice.cognitiveservices.azure.com/;Key=YOUR_KEY_HERE"
  }
}
```

**File Status:** Ignored by `.gitignore` - Safe to commit locally ✅

### Example: appsettings.json

```json
{
  "Azure": {
    "SqlConnectionString": "Server=localhost;Database=NovelCSAM;Trusted_Connection=yes;",
    "ContentSafety": {
      "ContentSafetyConnectionString1": "Endpoint=https://myservice.cognitiveservices.azure.com/;Key=YOUR_KEY_HERE"
    }
  }
}
```

**File Status:** Ignored by `.gitignore` - Safe to use locally ✅

## Secure File Setup

### Step 1: Create from Examples

```bash
# For Azure Functions
cp NovelCsam.Functions/local.settings.example.json NovelCsam.Functions/local.settings.json

# For Console App
cp NovelCsam.UI.Console/appsettings.example.json NovelCsam.UI.Console/appsettings.json
```

### Step 2: Update with Real Values

Edit each file and replace placeholder values with actual credentials:
- Connection strings
- API keys
- Service endpoints
- Authentication credentials

### Step 3: Verify .gitignore

Ensure files are in `.gitignore`:

```bash
# Check if file is ignored
git check-ignore -v NovelCsam.Functions/local.settings.json
# Output: .gitignore:407:	NovelCsam.Functions/local.settings.json

# Check status before committing
git status
# Should NOT list local.settings.json or appsettings.json
```

### Step 4: Verify Before Pushing

```bash
# Double-check no secrets are staged
git diff --cached | grep -i "key\|password\|secret\|token" || echo "✓ No secrets found"

# View what will be pushed
git diff origin/develop

# Ensure sensitive files aren't included
git ls-files | grep -E "local.settings|appsettings" || echo "✓ No config files will be pushed"
```

## Credentials Management

### Azure Services Credentials

**Storage Account:**
- Connection String: `DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...`
- Access Key: 64-character base64-encoded string
- Location: Azure Portal > Storage Account > Access Keys

**SQL Database:**
- Connection String: `Server=tcp:server.database.windows.net,1433;Initial Catalog=db;User ID=user;Password=pass;`
- Credentials: Server admin username and password
- Location: Azure Portal > SQL Server > Connection strings

**Content Safety API:**
- Connection String: `Endpoint=https://service.cognitiveservices.azure.com/;Key=...`
- API Key: 32-character hex string
- Location: Azure Portal > Content Safety > Keys and Endpoint

**OpenAI:**
- API Key: Organization and resource-specific key
- Deployment Name: Name of deployed model
- Location: Azure Portal > OpenAI > Keys and Endpoint

## Secret Rotation

### When to Rotate

- Monthly for production
- After key compromise
- After team member departure
- After security audit findings

### Rotation Process

1. **Generate new credential** in Azure Portal
2. **Update in Key Vault** (if using)
3. **Restart services** to pick up new credential
4. **Disable old credential** after verification
5. **Delete old credential** after retention period

## Incident Response

### If Secrets Are Committed

1. **Immediately rotate** all compromised credentials
2. **Force push to remove** from history:
   ```bash
   git reset --soft HEAD~1  # Unstage last commit
   git checkout -- .        # Restore state
   # Remove secrets from staged files
   git commit "Revert: Remove secrets (file was committed with secrets)"
   git push -f
   ```
3. **Audit logs** for unauthorized access
4. **Review repository history** for other commits with secrets
5. **Notify team** of security incident

### If Credentials Are Compromised

1. **Immediately disable** the compromised credential in Azure Portal
2. **Generate new credential** immediately
3. **Update** all systems using the old credential
4. **Monitor logs** for unauthorized access attempts
5. **Review access patterns** for suspicious activity
6. **Document incident** with timeline and remediation

## Compliance & Auditing

### What Gets Logged

✅ Safe to log:
- Operation names and types
- Success/failure status
- Timestamps
- Service names
- User identities (masked)

❌ Never log:
- Connection strings
- API keys
- Passwords
- Access tokens
- Sensitive business data

### Log Sanitization

All logs should sanitize sensitive data:

```csharp
// ❌ BAD: Logs the connection string
LogHelper.LogInformation($"Connecting to: {connectionString}");

// ✅ GOOD: Logs service without credentials
LogHelper.LogInformation($"Connecting to SQL Database");

// ✅ GOOD: Masks sensitive parts
string maskedConnStr = Regex.Replace(
    connectionString, 
    @"Password=[^;]*", 
    "Password=***");
LogHelper.LogInformation($"Connection: {maskedConnStr}");
```

## Resources

- [Azure Key Vault Documentation](https://learn.microsoft.com/en-us/azure/key-vault/)
- [User Secrets in .NET](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Azure Security Best Practices](https://learn.microsoft.com/en-us/azure/security/fundamentals/best-practices-and-patterns)
- [OWASP Secrets Management](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)
- [Git Security Best Practices](https://www.atlassian.com/git/tutorials/security)

## Questions?

For security questions or to report vulnerabilities, please contact the security team or open a confidential security advisory on GitHub.

---

**Last Updated:** October 30, 2025  
**Version:** 1.0.0
