using EasyPatchy3.Launcher.Models;
using System.Diagnostics;
using System.IO.Compression;

namespace EasyPatchy3.Launcher.Services;

public class PatchApplicationService
{
    private readonly LocalAppService _localAppService;
    private readonly IConfiguration _configuration;
    private readonly string _tempPath;

    public PatchApplicationService(LocalAppService localAppService, IConfiguration configuration)
    {
        _localAppService = localAppService;
        _configuration = configuration;
        _tempPath = configuration.GetValue<string>("LocalAppSettings:TempPath") ?? "/app/Temp";
        Directory.CreateDirectory(_tempPath);
    }

    public async Task<bool> ApplyPatchAsync(LocalAppVersion currentVersion, Stream patchStream, string targetVersionName)
    {
        var tempWorkDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var sourceArchivePath = Path.Combine(tempWorkDir, "source.zip");
        var patchFilePath = Path.Combine(tempWorkDir, "patch.hdiff");
        var outputArchivePath = Path.Combine(tempWorkDir, "target.zip");

        try
        {
            Directory.CreateDirectory(tempWorkDir);

            // Create ZIP archive of current version
            await CreateArchiveFromDirectoryAsync(currentVersion.Path, sourceArchivePath);

            // Save patch to temporary file
            using (var patchFileStream = File.Create(patchFilePath))
            {
                await patchStream.CopyToAsync(patchFileStream);
            }

            // Apply patch using HDiffPatch
            var success = await ApplyHDiffPatchAsync(sourceArchivePath, patchFilePath, outputArchivePath);
            if (!success)
            {
                return false;
            }

            // Extract patched archive to new version directory
            using (var outputStream = File.OpenRead(outputArchivePath))
            {
                await _localAppService.InstallVersionAsync(targetVersionName, outputStream, setAsCurrent: true);
            }

            return true;
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Failed to apply patch: {ex.Message}", ex);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempWorkDir))
            {
                try
                {
                    Directory.Delete(tempWorkDir, true);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }
    }

    private async Task CreateArchiveFromDirectoryAsync(string sourceDir, string archivePath)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            // Skip metadata files
            if (Path.GetFileName(file) == "metadata.json")
                continue;

            var relativePath = Path.GetRelativePath(sourceDir, file);
            var entry = archive.CreateEntry(relativePath);

            using var entryStream = entry.Open();
            using var fileStream = File.OpenRead(file);
            await fileStream.CopyToAsync(entryStream);
        }
    }

    private async Task<bool> ApplyHDiffPatchAsync(string sourceArchive, string patchFile, string outputArchive)
    {
        try
        {
            // For now, we'll use a mock implementation since HDiffPatch may not be available
            // In production, this should use the actual HDiffPatch binary
            return await ApplyMockPatchAsync(sourceArchive, patchFile, outputArchive);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ApplyMockPatchAsync(string sourceArchive, string patchFile, string outputArchive)
    {
        // This is a mock implementation that simulates patch application
        // In a real implementation, this would call HDiffPatch
        try
        {
            // For testing purposes, we'll just copy the source to output
            // This should be replaced with actual HDiffPatch integration
            File.Copy(sourceArchive, outputArchive);

            // Simulate patch application delay
            await Task.Delay(1000);

            return true;
        }
        catch
        {
            return false;
        }
    }
}