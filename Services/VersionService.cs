using EasyPatchy3.Data;
using EasyPatchy3.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace EasyPatchy3.Services
{
    public class VersionService : IVersionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IStorageService _storageService;
        private readonly IPatchService _patchService;

        public VersionService(ApplicationDbContext context, IStorageService storageService, IPatchService patchService)
        {
            _context = context;
            _storageService = storageService;
            _patchService = patchService;
        }

        public async Task<AppVersion?> GetVersionAsync(int id)
        {
            return await _context.Versions.FindAsync(id);
        }

        public async Task<AppVersion?> GetVersionByNameAsync(string name)
        {
            return await _context.Versions.FirstOrDefaultAsync(v => v.Name == name);
        }

        public async Task<List<AppVersion>> GetAllVersionsAsync()
        {
            return await _context.Versions.OrderByDescending(v => v.CreatedAt).ToListAsync();
        }

        public async Task<AppVersion> CreateVersionAsync(string name, string description, string folderPath)
        {
            var existingVersion = await GetVersionByNameAsync(name);
            if (existingVersion != null)
            {
                throw new InvalidOperationException($"Version '{name}' already exists.");
            }

            var storagePath = await _storageService.SaveVersionAsync(folderPath, name);
            var hash = await CalculateFolderHashAsync(folderPath);
            var size = GetFolderSize(folderPath);

            var version = new AppVersion
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                Hash = hash,
                Size = size,
                StoragePath = storagePath
            };

            _context.Versions.Add(version);
            await _context.SaveChangesAsync();

            await _patchService.GenerateAllPatchesForVersionAsync(version.Id);

            return version;
        }

        public async Task DeleteVersionAsync(int id)
        {
            var version = await GetVersionAsync(id);
            if (version == null)
                return;

            await _storageService.DeleteVersionAsync(version.StoragePath);

            _context.Versions.Remove(version);
            await _context.SaveChangesAsync();
        }

        private async Task<string> CalculateFolderHashAsync(string folderPath)
        {
            using var sha256 = SHA256.Create();
            var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
                .OrderBy(p => p)
                .ToList();

            var combinedHash = new byte[32];
            foreach (var file in files)
            {
                var fileHash = sha256.ComputeHash(await File.ReadAllBytesAsync(file));
                for (int i = 0; i < combinedHash.Length; i++)
                {
                    combinedHash[i] ^= fileHash[i % fileHash.Length];
                }
            }

            return Convert.ToHexString(combinedHash).ToLowerInvariant();
        }

        private long GetFolderSize(string folderPath)
        {
            return Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
        }
    }
}