using FormDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FormDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KeyVaultController : ControllerBase
    {
        private readonly KeyVaultDiagnostics _diagnostics;
        private readonly ILogger<KeyVaultController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public KeyVaultController(
            KeyVaultDiagnostics diagnostics,
            ILogger<KeyVaultController> logger,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _diagnostics = diagnostics;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
        }

        [HttpGet("diagnose")]
        [Authorize(Roles = "Admin")]
        [AllowAnonymous]  // Allow anonymous access in development only
        public IActionResult DiagnoseKeyVaultAccess()
        {
            // In production, require Admin role
            if (!_environment.IsDevelopment() && !User.IsInRole("Admin"))
            {
                return Forbid();
            }
            
            try
            {
                bool success = _diagnostics.VerifyKeyVaultAccess();

                var result = new
                {
                    Status = success ? "Success" : "Failed",
                    KeyVaultUri = _configuration["KeyVault:VaultUri"] ?? Environment.GetEnvironmentVariable("VaultUri") ?? "Not found",
                    UseLocalSecrets = _configuration.GetValue<bool>("KeyVault:UseLocalSecrets", false),
                    EnvironmentName = _environment.EnvironmentName,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in KeyVault diagnostics endpoint");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        // This endpoint only returns non-sensitive configuration values for diagnostics
        [HttpGet("config")]
        [Authorize(Roles = "Admin")]
        [AllowAnonymous]  // Allow anonymous access in development only
        public IActionResult GetConfiguration()
        {
            // In production, require Admin role
            if (!_environment.IsDevelopment() && !User.IsInRole("Admin"))
            {
                return Forbid();
            }
            
            try
            {
                var configValues = _configuration.AsEnumerable()
                    .Where(c => !c.Key.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase) &&
                           !c.Key.Contains("Password", StringComparison.OrdinalIgnoreCase) &&
                           !c.Key.Contains("Secret", StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(c.Value))
                    .OrderBy(c => c.Key)
                    .ToDictionary(c => c.Key, c => c.Value);

                return Ok(new
                {
                    Environment = _environment.EnvironmentName,
                    KeyVaultUri = _configuration["KeyVault:VaultUri"] ?? Environment.GetEnvironmentVariable("VaultUri") ?? "Not found",
                    UseLocalSecrets = _configuration.GetValue<bool>("KeyVault:UseLocalSecrets", false),
                    ConfigurationValues = configValues
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration values");
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}