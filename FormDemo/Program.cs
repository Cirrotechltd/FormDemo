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
    builder.Configuration.GetValue<bool>("KeyVault:UseLocalSecrets", false);

if (!useLocalSecrets)
{
    try
    {
        string? keyVaultUri = builder.Configuration["KeyVault:VaultUri"] ?? 
            Environment.GetEnvironmentVariable("VaultUri");
            
        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            Console.WriteLine($"Configuring Azure Key Vault with URI: {keyVaultUri}");
            
            // Create DefaultAzureCredential with specific options for local development
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeSharedTokenCacheCredential = true,
                ExcludeManagedIdentityCredential = builder.Environment.IsDevelopment()
            });
            
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
    }
}
else
{
    Console.WriteLine("Using local development secrets instead of Azure Key Vault");
}

// Register KeyVaultDiagnostics service
builder.Services.AddTransient<KeyVaultDiagnostics>();

// Register FormTemplateService
builder.Services.AddSingleton<IFormTemplateService, FormTemplateService>();

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

// Verify Key Vault access during startup
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
            app.Logger.LogWarning("Key Vault access check failed. The application might not have access to secrets.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error verifying Key Vault access");
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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
