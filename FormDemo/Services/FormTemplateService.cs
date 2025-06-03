using System.Text.Json;

namespace FormDemo.Services
{
    public interface IFormTemplateService
    {
        string GetCurrentFormTemplate();
        void SetCurrentFormTemplate(string templateName);
        IEnumerable<string> GetAvailableTemplates();
        string GetTemplatePath(string templateName);
    }

    public class FormTemplateService : IFormTemplateService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FormTemplateService> _logger;
        private static readonly string SettingsFileName = "form-template-settings.json";
        private readonly string _settingsFilePath;
        private readonly string _formsDirectory;

        public FormTemplateService(IWebHostEnvironment environment, ILogger<FormTemplateService> logger)
        {
            _environment = environment;
            _logger = logger;
            _formsDirectory = Path.Combine(_environment.ContentRootPath, "forms");
            _settingsFilePath = Path.Combine(_environment.ContentRootPath, SettingsFileName);

            // Ensure the settings file exists
            if (!File.Exists(_settingsFilePath))
            {
                SaveSettings(new FormTemplateSettings
                {
                    CurrentTemplate = "user-information-form.json" // Default template
                });
            }
        }

        public string GetCurrentFormTemplate()
        {
            try
            {
                var settings = LoadSettings();
                return settings.CurrentTemplate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading form template settings");
                return "user-information-form.json"; // Fallback to default
            }
        }

        public void SetCurrentFormTemplate(string templateName)
        {
            if (string.IsNullOrEmpty(templateName) || !TemplateExists(templateName))
            {
                _logger.LogWarning($"Invalid template name provided: {templateName}");
                return;
            }

            try
            {
                var settings = LoadSettings();
                settings.CurrentTemplate = templateName;
                SaveSettings(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving form template settings for {templateName}");
            }
        }

        public IEnumerable<string> GetAvailableTemplates()
        {
            try
            {
                return Directory.GetFiles(_formsDirectory, "*.json")
                    .Select(f => Path.GetFileName(f) ?? string.Empty)
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available templates");
                return Enumerable.Empty<string>();
            }
        }

        public string GetTemplatePath(string templateName)
        {
            return Path.Combine(_formsDirectory, templateName);
        }

        private bool TemplateExists(string templateName)
        {
            return File.Exists(GetTemplatePath(templateName));
        }

        private FormTemplateSettings LoadSettings()
        {
            string json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<FormTemplateSettings>(json) 
                ?? new FormTemplateSettings { CurrentTemplate = "user-information-form.json" };
        }

        private void SaveSettings(FormTemplateSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }

        private class FormTemplateSettings
        {
            public string CurrentTemplate { get; set; } = "user-information-form.json";
        }
    }
}