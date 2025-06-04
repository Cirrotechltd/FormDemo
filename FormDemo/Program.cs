using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using FormDemo.Services;

var builder = WebApplication.CreateBuilder(args);

// Only set up Azure Key Vault if we're not using local secrets (in production)
bool useLocalSecrets = builder.Environment.IsDevelopment() && 
    builder.Configuration.GetValue<bool>("KeyVault:UseLocalSecrets", true); // Default to true in development

if (!useLocalSecrets)
{
    try
    {
        string? keyVaultUri = builder.Configuration["KeyVault:VaultUri"] ?? 
            Environment.GetEnvironmentVariable("VaultUri");
            
        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            Console.WriteLine($"Configuring Azure Key Vault with URI: {keyVaultUri}");
            
            // Try to get credentials from configuration
            string? clientId = builder.Configuration["AzureAd:ClientId"];
            string? clientSecret = builder.Configuration["AzureAd:ClientSecret"];
            string? tenantId = builder.Configuration["AzureAd:TenantId"];
            
            Azure.Core.TokenCredential credential;
            
            // Use ClientSecretCredential if all values are available
            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret) && !string.IsNullOrEmpty(tenantId))
            {
                Console.WriteLine("Using ClientSecretCredential for Key Vault access");
                credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            }
            else 
            {
                // Create DefaultAzureCredential with specific options to ALWAYS avoid ManagedIdentityCredential
                var credentialOptions = new DefaultAzureCredentialOptions
                {
                    ExcludeManagedIdentityCredential = true, // Always exclude to avoid the error
                    ExcludeSharedTokenCacheCredential = true,
                    ExcludeVisualStudioCodeCredential = false,
                    ExcludeAzureCliCredential = false,
                    ExcludeVisualStudioCredential = false,
                    ExcludeInteractiveBrowserCredential = false
                };
                
                Console.WriteLine("Using DefaultAzureCredential with ManagedIdentity explicitly excluded");
                credential = new DefaultAzureCredential(credentialOptions);
            }
            
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                credential,
                new AzureKeyVaultConfigurationOptions
                {
                    ReloadInterval = TimeSpan.FromMinutes(5)
                });
            
            Console.WriteLine("Azure Key Vault configuration added successfully");
        }
        else
        {
            Console.WriteLine("Warning: Key Vault URI not found. Using configuration from appsettings.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error configuring Azure Key Vault: {ex.Message}");
        
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
        
        // Continue without Key Vault - will use local configuration
        Console.WriteLine("Continuing with local configuration...");
    }
}
else
{
    Console.WriteLine("Using local development secrets instead of Azure Key Vault");
}

// Register service for logging with enhanced debug output
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    
    if (builder.Environment.IsDevelopment())
    {
        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug); // More detailed logging in development
    }
});

// Register KeyVaultDiagnostics service
builder.Services.AddTransient<KeyVaultDiagnostics>();

// Register FormTemplateService
builder.Services.AddSingleton<IFormTemplateService, FormTemplateService>();

// Register SharePoint service
builder.Services.AddScoped<ISharePointService, SharePointService>();

// Configure Azure AD authentication
var initialScopes = builder.Configuration["MicrosoftGraph:Scopes"]?.Split(' ');

// Add services to the container.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
    .AddMicrosoftGraph(builder.Configuration.GetSection("MicrosoftGraph"))
    .AddInMemoryTokenCaches();

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

var app = builder.Build();

// Verify Key Vault access during startup, but don't crash if it fails
if (!useLocalSecrets)
{
    try
    {
        var diagnostics = app.Services.GetRequiredService<KeyVaultDiagnostics>();
        var success = diagnostics.VerifyKeyVaultAccess();
        
        if (success)
        {
            app.Logger.LogInformation("Key Vault access verified successfully.");
        }
        else
        {
            app.Logger.LogWarning("Key Vault access check failed. The application will use local configuration.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error verifying Key Vault access - will use local configuration");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
