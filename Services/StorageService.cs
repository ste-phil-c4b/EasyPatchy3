using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace EasyPatchy3.Services
{
    public class StorageService : IStorageService
    {
        private readonly string _storageRoot;
        private readonly string _versionsPath;
        private readonly string _patchesPath;

        public StorageService()
        {
            _storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
            _versionsPath = Path.Combine(_storageRoot, "Versions");
            _patchesPath = Path.Combine(_storageRoot, "Patches");

            Directory.CreateDirectory(_versionsPath);
            Directory.CreateDirectory(_patchesPath);
        }

        public async Task<string> SaveVersionAsync(string folderPath, string versionName)
        {
            var versionPath = GetVersionPath(versionName);
            var zipPath = versionPath + ".zip";

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            await Task.Run(() => ZipFile.CreateFromDirectory(folderPath, zipPath));
            return zipPath;
        }

        public async Task<string> SavePatchAsync(byte[] patchData, string sourceVersion, string targetVersion)
        {
            var patchPath = GetPatchPath(sourceVersion, targetVersion);
            await File.WriteAllBytesAsync(patchPath, patchData);
            return patchPath;
        }

        public async Task<byte[]> GetVersionArchiveAsync(string storagePath)
        {
            if (!File.Exists(storagePath))
            {
                throw new FileNotFoundException($"Version archive not found: {storagePath}");
            }

            return await File.ReadAllBytesAsync(storagePath);
        }

        public async Task<byte[]> GetPatchAsync(string patchPath)
        {
            if (!File.Exists(patchPath))
            {
                throw new FileNotFoundException($"Patch file not found: {patchPath}");
            }

            return await File.ReadAllBytesAsync(patchPath);
        }

        public Task DeleteVersionAsync(string storagePath)
        {
            if (File.Exists(storagePath))
            {
                File.Delete(storagePath);
            }

            return Task.CompletedTask;
        }

        public Task DeletePatchAsync(string patchPath)
        {
            if (File.Exists(patchPath))
            {
                File.Delete(patchPath);
            }

            return Task.CompletedTask;
        }

        public string GetVersionPath(string versionName)
        {
            var safeName = Path.GetInvalidFileNameChars()
                .Aggregate(versionName, (current, c) => current.Replace(c, '_'));
            return Path.Combine(_versionsPath, safeName);
        }

        public string GetPatchPath(string sourceVersion, string targetVersion)
        {
            var safeName = $"{sourceVersion}_to_{targetVersion}.patch";
            safeName = Path.GetInvalidFileNameChars()
                .Aggregate(safeName, (current, c) => current.Replace(c, '_'));
            return Path.Combine(_patchesPath, safeName);
        }
    }
}