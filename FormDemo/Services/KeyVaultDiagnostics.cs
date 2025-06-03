using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FormDemo.Services
{
    /// <summary>
    /// Helper class to diagnose issues with Azure Key Vault integration
    /// </summary>
    public class KeyVaultDiagnostics
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<KeyVaultDiagnostics> _logger;
        private readonly IWebHostEnvironment _environment;

        public KeyVaultDiagnostics(
            IConfiguration configuration, 
            ILogger<KeyVaultDiagnostics> logger,
            IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Verifies Key Vault access by attempting to retrieve a secret and logs diagnostics information
        /// </summary>
        /// <returns>True if Key Vault can be accessed, false otherwise</returns>
        public bool VerifyKeyVaultAccess()
        {
            try
            {
                string? vaultUri = _configuration["KeyVault:VaultUri"] ?? 
                    Environment.GetEnvironmentVariable("VaultUri");

                if (string.IsNullOrEmpty(vaultUri))
                {
                    _logger.LogWarning("Key Vault URI not found in configuration or environment variables");
                    return false;
                }

                _logger.LogInformation("Environment: {Environment}", _environment.EnvironmentName);
                _logger.LogInformation("Using Key Vault URI: {VaultUri}", vaultUri);
                _logger.LogInformation("UseLocalSecrets: {UseLocalSecrets}", 
                    _configuration.GetValue<bool>("KeyVault:UseLocalSecrets", false));

                // Log configuration keys to help with debugging
                _logger.LogInformation("Configuration keys available:");
                foreach (var key in _configuration.AsEnumerable()
                    .Where(k => !k.Key.Contains("Password", StringComparison.OrdinalIgnoreCase) && 
                           !k.Key.Contains("Secret", StringComparison.OrdinalIgnoreCase) && 
                           !string.IsNullOrEmpty(k.Value))
                    .OrderBy(k => k.Key))
                {
                    _logger.LogInformation("{Key} = {Value}", key.Key, key.Value);
                }

                // Try to access Key Vault
                _logger.LogInformation("Creating DefaultAzureCredential for Key Vault access...");
                
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions 
                {
                    ExcludeSharedTokenCacheCredential = true,
                    ExcludeManagedIdentityCredential = _environment.IsDevelopment()
                });
                
                _logger.LogInformation("Creating SecretClient for Key Vault...");
                var client = new SecretClient(new Uri(vaultUri), credential);
                
                // Try to get a test secret or just list secrets
                Response<KeyVaultSecret>? secretResponse = null;
                
                try
                {
                    _logger.LogInformation("Attempting to retrieve AzureAd:TenantId from Key Vault...");
                    secretResponse = client.GetSecret("AzureAd:TenantId");
                    _logger.LogInformation("Successfully retrieved AzureAd:TenantId from Key Vault");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not retrieve AzureAd:TenantId: {Message}", ex.Message);
                    
                    // Try listing secrets instead
                    try
                    {
                        _logger.LogInformation("Attempting to list secrets from Key Vault...");
                        var secrets = client.GetPropertiesOfSecrets();
                        var secretsList = secrets.ToList();
                        var count = secretsList.Count;
                        _logger.LogInformation("Successfully listed {Count} secrets from Key Vault", count);
                        
                        if (count > 0)
                        {
                            _logger.LogInformation("Available secret names in Key Vault:");
                            foreach (var secret in secretsList)
                            {
                                _logger.LogInformation("- {SecretName}", secret.Name);
                            }
                        }
                    }
                    catch (Exception listEx)
                    {
                        _logger.LogError("Failed to list secrets: {Message}", listEx.Message);
                        
                        if (listEx.InnerException != null)
                        {
                            _logger.LogError("Inner exception: {InnerMessage}", listEx.InnerException.Message);
                        }
                        
                        if (_environment.IsDevelopment())
                        {
                            _logger.LogInformation("For local development, make sure to:");
                            _logger.LogInformation("1. Run 'az login' to authenticate with Azure CLI");
                            _logger.LogInformation("2. Verify you have access to the Key Vault: {VaultUri}", vaultUri);
                            _logger.LogInformation("3. Check if the VaultUri is correct");
                        }
                        
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Key Vault");
                
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
                }
                
                return false;
            }
        }
    }
}