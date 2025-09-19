using System.Threading.Tasks;

namespace EasyPatchy3.Services
{
    public interface IStorageService
    {
        Task<string> SaveVersionAsync(string folderPath, string versionName);
        Task<string> SavePatchAsync(byte[] patchData, string sourceVersion, string targetVersion);
        Task<byte[]> GetVersionArchiveAsync(string storagePath);
        Task<byte[]> GetPatchAsync(string patchPath);
        Task DeleteVersionAsync(string storagePath);
        Task DeletePatchAsync(string patchPath);
        string GetVersionPath(string versionName);
        string GetPatchPath(string sourceVersion, string targetVersion);
    }
}