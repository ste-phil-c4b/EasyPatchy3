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
        private readonly ILogger<VersionService> _logger;

        public VersionService(ApplicationDbContext context, IStorageService storageService, ILogger<VersionService> logger)
        {
            _context = context;
            _storageService = storageService;
            _logger = logger;
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

        public async Task<AppVersion> CreateVersionAsync(string name, string? description, string folderPath)
        {
            // Validate input parameters
            ValidateVersionName(name);
            ValidateFolderPath(folderPath);

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

            _logger.LogInformation($"Version '{name}' created successfully with ID {version.Id}");

            // Note: Patch generation will be done separately to avoid circular dependencies
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

        private void ValidateVersionName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Version name cannot be null or empty", nameof(name));
            }

            if (name.Length > 200)
            {
                throw new ArgumentException("Version name cannot exceed 200 characters", nameof(name));
            }

            // Check for invalid characters that could cause issues in file systems or databases
            var invalidChars = Path.GetInvalidFileNameChars().Concat(new char[] { '<', '>', '|', '"', '*', '?', ':', '\\', '/' });
            if (name.IndexOfAny(invalidChars.ToArray()) >= 0)
            {
                throw new ArgumentException($"Version name contains invalid characters: {name}", nameof(name));
            }
        }

        private void ValidateFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("Folder path cannot be null or empty", nameof(folderPath));
            }

            try
            {
                // Get the full path to resolve any relative path components and prevent traversal
                var fullPath = Path.GetFullPath(folderPath);

                // Ensure the path exists and is a directory
                if (!Directory.Exists(fullPath))
                {
                    throw new DirectoryNotFoundException($"Directory not found: {folderPath}");
                }

                // Basic path traversal protection
                if (fullPath.Contains(".."))
                {
                    throw new ArgumentException($"Path traversal detected in folder path: {folderPath}", nameof(folderPath));
                }

                // Check if directory is accessible and contains files
                try
                {
                    var files = Directory.GetFiles(fullPath, "*", SearchOption.TopDirectoryOnly);
                    if (files.Length == 0)
                    {
                        _logger.LogWarning($"Warning: Directory '{folderPath}' is empty");
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    throw new UnauthorizedAccessException($"Access denied to directory: {folderPath}");
                }
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is DirectoryNotFoundException || ex is UnauthorizedAccessException))
            {
                throw new ArgumentException($"Invalid folder path: {folderPath}", nameof(folderPath), ex);
            }
        }
    }
}