using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using System.Security.Cryptography.X509Certificates;

namespace FormDemo.Services
{
    public class AzureKeyVaultCertificateProvider
    {
        private readonly string _keyVaultUrl;
        private readonly ILogger<AzureKeyVaultCertificateProvider> _logger;
        private readonly IWebHostEnvironment? _environment;
        private readonly IConfiguration? _configuration;

        public AzureKeyVaultCertificateProvider(
            string keyVaultUrl, 
            ILogger<AzureKeyVaultCertificateProvider> logger,
            IWebHostEnvironment? environment = null,
            IConfiguration? configuration = null)
        {
            _keyVaultUrl = keyVaultUrl;
            _logger = logger;
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<X509Certificate2> GetCertificateAsync(string certificateName)
        {
            try
            {
                _logger.LogInformation($"Retrieving certificate '{certificateName}' from Key Vault '{_keyVaultUrl}'");
                
                // Try multiple credential options
                var credential = GetAppropriateCredential();
                
                // Create secret client with credential
                var secretClient = new SecretClient(new Uri(_keyVaultUrl), credential);
                
                // Get the certificate as a secret (contains the private key)
                _logger.LogInformation($"Requesting secret '{certificateName}' from Key Vault");
                KeyVaultSecret secret;
                
                try 
                {
                    secret = await secretClient.GetSecretAsync(certificateName);
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 401)
                {
                    _logger.LogError(ex, "Authentication failed when accessing Key Vault. Trying with client credentials.");
                    
                    // Try client credentials if available
                    string? clientId = _configuration?["AzureAd:ClientId"];
                    string? clientSecret = _configuration?["AzureAd:ClientSecret"];
                    string? tenantId = _configuration?["AzureAd:TenantId"];
                    
                    if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret) && !string.IsNullOrEmpty(tenantId))
                    {
                        _logger.LogInformation("Trying Key Vault access with client credentials");
                        var clientCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                        secretClient = new SecretClient(new Uri(_keyVaultUrl), clientCredential);
                        secret = await secretClient.GetSecretAsync(certificateName);
                    }
                    else
                    {
                        throw;
                    }
                }
                
                if (secret == null || string.IsNullOrEmpty(secret.Value))
                {
                    throw new InvalidOperationException($"Certificate '{certificateName}' not found in Key Vault");
                }
                
                // Convert the secret value to a certificate
                _logger.LogInformation($"Converting secret to X509Certificate2");
                byte[] certBytes = Convert.FromBase64String(secret.Value);
                var certificate = new X509Certificate2(
                    certBytes, 
                    (string)null!, 
                    X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable
                );
                
                _logger.LogInformation($"Certificate '{certificateName}' retrieved successfully. Thumbprint: {certificate.Thumbprint}");
                return certificate;
            }
            catch (Azure.Identity.AuthenticationFailedException ex)
            {
                _logger.LogError(ex, $"Authentication failure retrieving certificate '{certificateName}' from Key Vault. " +
                                     $"Make sure you are authenticated via Azure CLI (az login) or have valid environment variables.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving certificate '{certificateName}' from Key Vault");
                throw;
            }
        }
        
        private Azure.Core.TokenCredential GetAppropriateCredential()
        {
            _logger.LogInformation("Creating credential for Azure Key Vault access");
            
            // Try to get credentials from environment variables first
            string? clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            string? clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
            string? tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            
            // Check if configuration has credentials
            if (_configuration != null)
            {
                if (string.IsNullOrEmpty(clientId))
                    clientId = _configuration["AzureAd:ClientId"];
                
                if (string.IsNullOrEmpty(clientSecret))
                    clientSecret = _configuration["AzureAd:ClientSecret"];
                
                if (string.IsNullOrEmpty(tenantId))
                    tenantId = _configuration["AzureAd:TenantId"];
            }
            
            // If we have client ID and secret, use client credential
            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret) && !string.IsNullOrEmpty(tenantId))
            {
                _logger.LogInformation("Using ClientSecretCredential for Key Vault access");
                return new ClientSecretCredential(tenantId, clientId, clientSecret);
            }
            
            // For developers - always exclude ManagedIdentity as it causes the error
            var credentialOptions = new DefaultAzureCredentialOptions
            {
                ExcludeManagedIdentityCredential = true,     // ALWAYS exclude managed identity to avoid the error
                ExcludeSharedTokenCacheCredential = true,    // Often causes issues
                ExcludeVisualStudioCredential = false,       // Good for developer experience
                ExcludeAzureCliCredential = false,           // Good for developer experience
                ExcludeInteractiveBrowserCredential = false, // Allow interactive login as fallback
                ExcludeVisualStudioCodeCredential = false    // Good for developer experience
            };
            
            _logger.LogInformation("Using DefaultAzureCredential with ManagedIdentity explicitly excluded");
            return new DefaultAzureCredential(credentialOptions);
        }
    }
}