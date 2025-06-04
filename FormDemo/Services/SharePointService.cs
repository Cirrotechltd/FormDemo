using Microsoft.SharePoint.Client;
using System.Security.Cryptography.X509Certificates;
using FormDemo.Models;
using System.IO;
using Microsoft.Identity.Client;
using MSALLogLevel = Microsoft.Identity.Client.LogLevel;
using MSLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace FormDemo.Services
{
    public class SharePointService : ISharePointService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SharePointService> _logger;
        private readonly AzureKeyVaultCertificateProvider _certificateProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IWebHostEnvironment _environment;

        public SharePointService(
            IConfiguration configuration, 
            ILogger<SharePointService> logger, 
            ILoggerFactory loggerFactory,
            IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _environment = environment;
            
            string keyVaultUrl = _configuration["KeyVault:VaultUri"] ??
                throw new ArgumentNullException("KeyVault:VaultUri configuration is missing");
                
            // Pass all dependencies to certificate provider including configuration
            _certificateProvider = new AzureKeyVaultCertificateProvider(
                keyVaultUrl, 
                _loggerFactory.CreateLogger<AzureKeyVaultCertificateProvider>(),
                environment,
                configuration);
            
            _logger.LogInformation("SharePointService initialized with KeyVault URL: {KeyVaultUrl}", keyVaultUrl);
        }

        public async Task<ClientContext> GetClientContextAsync(string siteUrl)
        {
            try
            {
                _logger.LogInformation($"Getting SharePoint client context for site: {siteUrl}");
                
                // Get certificate name from configuration
                string certificateName = _configuration["SharePoint:CertificateName"] ?? 
                    throw new ArgumentNullException("SharePoint:CertificateName configuration is missing");
                
                // Get certificate path from configuration, if provided
                string certificatePath = _configuration["SharePoint:CertificatePath"];
                string certificatePassword = _configuration["SharePoint:CertificatePassword"] ?? "";
                
                X509Certificate2 certificate;
                
                // Check if we should use a local certificate file first
                if (!string.IsNullOrEmpty(certificatePath) && System.IO.File.Exists(certificatePath))
                {
                    _logger.LogInformation($"Loading certificate from path: {certificatePath}");
                    certificate = new X509Certificate2(certificatePath, certificatePassword, 
                        X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
                }
                else 
                {
                    // If no explicit path or file doesn't exist, try Certificates folder in development
                    if (_environment.IsDevelopment())
                    {
                        string localCertPath = Path.Combine(_environment.ContentRootPath, "Certificates", $"{certificateName}.pfx");
                        
                        if (System.IO.File.Exists(localCertPath))
                        {
                            _logger.LogInformation($"Loading local development certificate from {localCertPath}");
                            certificate = new X509Certificate2(localCertPath, certificatePassword, 
                                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
                        }
                        else
                        {
                            // Finally try to get from Key Vault
                            _logger.LogInformation($"Attempting to retrieve certificate '{certificateName}' from Key Vault");
                            certificate = await _certificateProvider.GetCertificateAsync(certificateName);
                        }
                    }
                    else
                    {
                        // In production, prioritize KeyVault
                        _logger.LogInformation($"Attempting to retrieve certificate '{certificateName}' from Key Vault");
                        certificate = await _certificateProvider.GetCertificateAsync(certificateName);
                    }
                }
                
                // Get Azure AD application details from configuration
                string tenantId = _configuration["SharePoint:TenantId"] ?? 
                    throw new ArgumentNullException("SharePoint:TenantId configuration is missing");
                string clientId = _configuration["SharePoint:ClientId"] ?? 
                    throw new ArgumentNullException("SharePoint:ClientId configuration is missing");
                
                // Get access token using the certificate
                var accessToken = await GetAccessTokenWithCertificate(tenantId, clientId, certificate, siteUrl);
                
                // Create client context with the access token
                var clientContext = new ClientContext(siteUrl);
                clientContext.ExecutingWebRequest += (sender, e) => {
                    e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + accessToken;
                };
                
                _logger.LogInformation($"Successfully created SharePoint client context for site: {siteUrl}");
                
                return clientContext;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get SharePoint client context for site: {siteUrl}");
                throw;
            }
        }
        
        private async Task<string> GetAccessTokenWithCertificate(string tenantId, string clientId, X509Certificate2 certificate, string siteUrl)
        {
            try
            {
                var authority = $"https://login.microsoftonline.com/{tenantId}/";
                var resource = "https://" + new Uri(siteUrl).Host;
                
                _logger.LogInformation($"Acquiring token for resource: {resource}");
                
                // Use Microsoft.Identity.Client to get an access token with specific options
                var app = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithAuthority(authority)
                    .WithCertificate(certificate)
                    // Add logging for troubleshooting
                    .WithLogging((level, message, pii) => 
                    {
                        var logLevel = level switch
                        {
                            MSALLogLevel.Error => MSLogLevel.Error,
                            MSALLogLevel.Warning => MSLogLevel.Warning,
                            MSALLogLevel.Info => MSLogLevel.Information,
                            MSALLogLevel.Verbose => MSLogLevel.Debug,
                            _ => MSLogLevel.Trace
                        };
                        _logger.Log(logLevel, message);
                    }, MSALLogLevel.Verbose, enablePiiLogging: false)
                    .Build();
                
                var scopes = new[] { $"{resource}/.default" };
                
                _logger.LogInformation($"Requesting token with client credentials using certificate");
                var result = await app.AcquireTokenForClient(scopes)
                    .ExecuteAsync()
                    .ConfigureAwait(false);
                    
                _logger.LogInformation($"Successfully acquired token. Expires on: {result.ExpiresOn}");
                
                return result.AccessToken;
            }
            catch (MsalServiceException ex) 
            {
                // This exception is thrown when there's an error in the Azure AD service
                _logger.LogError(ex, $"MSAL Service Exception: {ex.Message}, Error Code: {ex.ErrorCode}");
                
                if (ex.ErrorCode == "invalid_client")
                {
                    _logger.LogError("This could indicate an issue with the certificate, client ID, or permissions");
                }
                
                throw;
            }
            catch (MsalClientException ex)
            {
                // This exception is thrown for client-side errors (no internet, no certificate, etc.)
                _logger.LogError(ex, $"MSAL Client Exception: {ex.Message}, Error Code: {ex.ErrorCode}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error acquiring access token for SharePoint");
                throw;
            }
        }

        public async Task<List<string>> GetDocumentLibrariesAsync(string siteUrl)
        {
            try
            {
                var clientContext = await GetClientContextAsync(siteUrl);
                var web = clientContext.Web;
                
                clientContext.Load(web);
                clientContext.Load(web.Lists, lists => lists.Where(l => l.BaseTemplate == 101));
                await clientContext.ExecuteQueryAsync();
                
                return web.Lists.Select(l => l.Title).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get document libraries from site: {siteUrl}");
                throw;
            }
        }

        public async Task<List<Dictionary<string, object>>> GetListItemsAsync(string siteUrl, string listTitle, int maxItems = 100)
        {
            try
            {
                var clientContext = await GetClientContextAsync(siteUrl);
                var web = clientContext.Web;
                var list = web.Lists.GetByTitle(listTitle);
                
                var query = new CamlQuery
                {
                    ViewXml = $"<View><RowLimit>{maxItems}</RowLimit></View>"
                };
                
                var items = list.GetItems(query);
                clientContext.Load(items);
                await clientContext.ExecuteQueryAsync();
                
                var result = new List<Dictionary<string, object>>();
                
                foreach (var item in items)
                {
                    var fieldValues = new Dictionary<string, object>();
                    foreach (var fieldName in item.FieldValues.Keys)
                    {
                        fieldValues[fieldName] = item[fieldName];
                    }
                    result.Add(fieldValues);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get items from list '{listTitle}' in site: {siteUrl}");
                throw;
            }
        }

        public async Task<bool> UploadDocumentAsync(string siteUrl, string libraryName, string fileName, Stream fileContent)
        {
            try
            {
                var clientContext = await GetClientContextAsync(siteUrl);
                var web = clientContext.Web;
                var library = web.Lists.GetByTitle(libraryName);
                
                // Convert stream to byte array
                using var memoryStream = new MemoryStream();
                await fileContent.CopyToAsync(memoryStream);
                byte[] fileBytes = memoryStream.ToArray();
                
                var fileCreationInfo = new FileCreationInformation
                {
                    Content = fileBytes,
                    Url = fileName,
                    Overwrite = true
                };
                
                var uploadedFile = library.RootFolder.Files.Add(fileCreationInfo);
                clientContext.Load(uploadedFile);
                await clientContext.ExecuteQueryAsync();
                
                _logger.LogInformation($"Successfully uploaded file '{fileName}' to library '{libraryName}' in site: {siteUrl}");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to upload file '{fileName}' to library '{libraryName}' in site: {siteUrl}");
                return false;
            }
        }

        // New method to create a list item from form submission
        public async Task<bool> CreateListItemFromFormAsync(UserFormModel formData, IDictionary<string, object>? additionalFields = null)
        {
            try
            {
                // Get SharePoint site and list info from configuration
                string siteUrl = _configuration["SharePoint:SiteUrl"] ?? 
                    throw new ArgumentNullException("SharePoint:SiteUrl configuration is missing");
                string listName = _configuration["SharePoint:ListName"] ?? 
                    throw new ArgumentNullException("SharePoint:ListName configuration is missing");
                    
                _logger.LogInformation($"Creating list item in '{listName}' from form submission");
                
                var clientContext = await GetClientContextAsync(siteUrl);
                var web = clientContext.Web;
                var list = web.Lists.GetByTitle(listName);
                
                // Create a new item
                ListItemCreationInformation itemCreateInfo = new ListItemCreationInformation();
                ListItem newItem = list.AddItem(itemCreateInfo);
                
                // Set standard form fields from UserFormModel properties
                newItem["Title"] = formData.Name;  // Usually "Title" is a required field in SharePoint
                newItem["Email"] = formData.Email;
                
                // Set any additional fields that were passed
                if (additionalFields != null)
                {
                    foreach (var field in additionalFields)
                    {
                        newItem[field.Key] = field.Value;
                    }
                }
                
                // Add submission timestamp
                newItem["SubmittedOn"] = DateTime.Now;
                
                // Update the list item
                newItem.Update();
                await clientContext.ExecuteQueryAsync();
                
                _logger.LogInformation($"Successfully created list item in '{listName}' for submission from {formData.Name}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create SharePoint list item from form submission");
                return false;
            }
        }
    }
}