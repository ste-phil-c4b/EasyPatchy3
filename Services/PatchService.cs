using EasyPatchy3.Data;
using EasyPatchy3.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EasyPatchy3.Services
{
    public class PatchService : IPatchService
    {
        private readonly ApplicationDbContext _context;
        private readonly IStorageService _storageService;
        private readonly ILogger<PatchService> _logger;
        private readonly string _hdiffPatchPath = "/usr/local/bin/hdiffz";

        public PatchService(ApplicationDbContext context, IStorageService storageService, ILogger<PatchService> logger)
        {
            _context = context;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<Patch?> GetPatchAsync(int sourceVersionId, int targetVersionId)
        {
            return await _context.Patches
                .Include(p => p.SourceVersion)
                .Include(p => p.TargetVersion)
                .FirstOrDefaultAsync(p => p.SourceVersionId == sourceVersionId && p.TargetVersionId == targetVersionId);
        }

        public async Task<List<Patch>> GetPatchesForVersionAsync(int versionId)
        {
            return await _context.Patches
                .Include(p => p.SourceVersion)
                .Include(p => p.TargetVersion)
                .Where(p => p.SourceVersionId == versionId || p.TargetVersionId == versionId)
                .ToListAsync();
        }

        public async Task<Patch> GeneratePatchAsync(int sourceVersionId, int targetVersionId)
        {
            var existingPatch = await GetPatchAsync(sourceVersionId, targetVersionId);
            if (existingPatch != null && existingPatch.Status == PatchStatus.Completed)
            {
                return existingPatch;
            }

            var sourceVersion = await _context.Versions.FindAsync(sourceVersionId);
            var targetVersion = await _context.Versions.FindAsync(targetVersionId);

            if (sourceVersion == null || targetVersion == null)
            {
                throw new InvalidOperationException("Source or target version not found.");
            }

            var patch = existingPatch ?? new Patch
            {
                SourceVersionId = sourceVersionId,
                TargetVersionId = targetVersionId,
                CreatedAt = DateTime.UtcNow,
                Status = PatchStatus.Pending
            };

            if (existingPatch == null)
            {
                _context.Patches.Add(patch);
                await _context.SaveChangesAsync();
            }

            try
            {
                patch.Status = PatchStatus.Processing;
                await _context.SaveChangesAsync();

                var patchData = await GeneratePatchUsingHDiff(sourceVersion.StoragePath, targetVersion.StoragePath);
                var patchPath = await _storageService.SavePatchAsync(patchData, sourceVersion.Name, targetVersion.Name);

                patch.PatchFilePath = patchPath;
                patch.PatchSize = patchData.Length;
                patch.Status = PatchStatus.Completed;
            }
            catch (Exception ex)
            {
                patch.Status = PatchStatus.Failed;
                patch.ErrorMessage = ex.Message;
            }

            await _context.SaveChangesAsync();
            return patch;
        }

        public async Task<List<Patch>> GenerateAllPatchesForVersionAsync(int newVersionId)
        {
            var patches = new List<Patch>();
            var allVersions = await _context.Versions.Where(v => v.Id != newVersionId).ToListAsync();

            foreach (var version in allVersions)
            {
                patches.Add(await GeneratePatchAsync(version.Id, newVersionId));
                patches.Add(await GeneratePatchAsync(newVersionId, version.Id));
            }

            return patches;
        }

        public async Task<byte[]> GetPatchFileAsync(int patchId)
        {
            var patch = await _context.Patches.FindAsync(patchId);
            if (patch == null || patch.Status != PatchStatus.Completed)
            {
                throw new InvalidOperationException("Patch not found or not ready.");
            }

            return await _storageService.GetPatchAsync(patch.PatchFilePath);
        }

        private async Task<byte[]> GeneratePatchUsingHDiff(string sourcePath, string targetPath)
        {
            var tempPatchFile = Path.GetTempFileName();
            try
            {
                _logger.LogInformation($"Generating patch from {sourcePath} to {targetPath}");

                // Verify source and target files exist
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"Source file not found: {sourcePath}");
                }
                if (!File.Exists(targetPath))
                {
                    throw new FileNotFoundException($"Target file not found: {targetPath}");
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _hdiffPatchPath,
                        Arguments = $"\"{sourcePath}\" \"{targetPath}\" \"{tempPatchFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                _logger.LogInformation($"Running HDiffPatch command: {_hdiffPatchPath} {process.StartInfo.Arguments}");

                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                _logger.LogInformation($"HDiffPatch output: {output}");

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"HDiffPatch failed with exit code {process.ExitCode}. Error: {error}");
                    throw new InvalidOperationException($"HDiff failed with exit code {process.ExitCode}: {error}");
                }

                if (!File.Exists(tempPatchFile))
                {
                    throw new InvalidOperationException("HDiffPatch completed but no patch file was generated");
                }

                var patchData = await File.ReadAllBytesAsync(tempPatchFile);
                _logger.LogInformation($"Generated patch file of size {patchData.Length} bytes");

                return patchData;
            }
            finally
            {
                if (File.Exists(tempPatchFile))
                {
                    File.Delete(tempPatchFile);
                }
            }
        }
    }
}