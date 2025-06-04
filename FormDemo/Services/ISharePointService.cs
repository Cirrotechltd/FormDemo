using Microsoft.SharePoint.Client;
using FormDemo.Models;

namespace FormDemo.Services
{
    public interface ISharePointService
    {
        Task<ClientContext> GetClientContextAsync(string siteUrl);
        Task<List<string>> GetDocumentLibrariesAsync(string siteUrl);
        Task<List<Dictionary<string, object>>> GetListItemsAsync(string siteUrl, string listTitle, int maxItems = 100);
        Task<bool> UploadDocumentAsync(string siteUrl, string libraryName, string fileName, Stream fileContent);
        Task<bool> CreateListItemFromFormAsync(UserFormModel formData, IDictionary<string, object>? additionalFields = null);
    }
}