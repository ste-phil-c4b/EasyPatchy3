using EasyPatchy3.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EasyPatchy3.Services
{
    public interface IPatchService
    {
        Task<Patch?> GetPatchAsync(int sourceVersionId, int targetVersionId);
        Task<List<Patch>> GetPatchesForVersionAsync(int versionId);
        Task<Patch> GeneratePatchAsync(int sourceVersionId, int targetVersionId);
        Task<List<Patch>> GenerateAllPatchesForVersionAsync(int newVersionId);
        Task<byte[]> GetPatchFileAsync(int patchId);
    }
}