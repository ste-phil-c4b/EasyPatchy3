using EasyPatchy3.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EasyPatchy3.Services
{
    public interface IVersionService
    {
        Task<AppVersion?> GetVersionAsync(int id);
        Task<AppVersion?> GetVersionByNameAsync(string name);
        Task<List<AppVersion>> GetAllVersionsAsync();
        Task<AppVersion> CreateVersionAsync(string name, string description, string folderPath);
        Task DeleteVersionAsync(int id);
    }
}