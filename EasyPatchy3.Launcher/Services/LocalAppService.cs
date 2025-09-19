using EasyPatchy3.Launcher.Models;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace EasyPatchy3.Launcher.Services;

public class LocalAppService
{
    private readonly IConfiguration _configuration;
    private readonly string _storagePath;
    private readonly string _tempPath;

    public LocalAppService(IConfiguration configuration)
    {
        _configuration = configuration;
        _storagePath = configuration.GetValue<string>("LocalAppSettings:StoragePath") ?? "/app/LocalApps";
        _tempPath = configuration.GetValue<string>("LocalAppSettings:TempPath") ?? "/app/Temp";

        Directory.CreateDirectory(_storagePath);
        Directory.CreateDirectory(_tempPath);
    }

    public async Task<LocalAppVersion?> GetCurrentVersionAsync()
    {
        var currentVersionFile = Path.Combine(_storagePath, "current.json");
        if (!File.Exists(currentVersionFile))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(currentVersionFile);
            return System.Text.Json.JsonSerializer.Deserialize<LocalAppVersion>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<LocalAppVersion>> GetInstalledVersionsAsync()
    {
        var versions = new List<LocalAppVersion>();
        var versionsDir = Path.Combine(_storagePath, "versions");

        if (!Directory.Exists(versionsDir))
            return versions;

        foreach (var versionDir in Directory.GetDirectories(versionsDir))
        {
            var metadataFile = Path.Combine(versionDir, "metadata.json");
            if (File.Exists(metadataFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(metadataFile);
                    var version = System.Text.Json.JsonSerializer.Deserialize<LocalAppVersion>(json);
                    if (version != null)
                    {
                        versions.Add(version);
                    }
                }
                catch
                {
                    // Skip invalid metadata files
                }
            }
        }

        return versions.OrderByDescending(v => v.InstalledAt).ToList();
    }

    public async Task InstallVersionAsync(string versionName, Stream zipStream, bool setAsCurrent = true)
    {
        var versionDir = Path.Combine(_storagePath, "versions", versionName);
        var tempExtractPath = Path.Combine(_tempPath, Guid.NewGuid().ToString());

        try
        {
            // Create directories
            Directory.CreateDirectory(versionDir);
            Directory.CreateDirectory(tempExtractPath);

            // Save ZIP file temporarily
            var tempZipPath = Path.Combine(_tempPath, $"{versionName}.zip");
            using (var fileStream = File.Create(tempZipPath))
            {
                await zipStream.CopyToAsync(fileStream);
            }

            // Extract ZIP
            ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);

            // Move extracted contents to version directory
            foreach (var file in Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(tempExtractPath, file);
                var destPath = Path.Combine(versionDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Move(file, destPath);
            }

            // Calculate hash and size
            var hash = await CalculateDirectoryHashAsync(versionDir);
            var size = CalculateDirectorySize(versionDir);

            // Create metadata
            var metadata = new LocalAppVersion
            {
                Name = versionName,
                Path = versionDir,
                Hash = hash,
                InstalledAt = DateTime.UtcNow,
                Size = size
            };

            var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(versionDir, "metadata.json"), metadataJson);

            // Set as current if requested
            if (setAsCurrent)
            {
                await SetCurrentVersionAsync(metadata);
            }

            // Cleanup
            File.Delete(tempZipPath);
            Directory.Delete(tempExtractPath, true);
        }
        catch
        {
            // Cleanup on failure
            if (Directory.Exists(versionDir))
                Directory.Delete(versionDir, true);
            throw;
        }
    }

    public async Task SetCurrentVersionAsync(LocalAppVersion version)
    {
        var currentVersionFile = Path.Combine(_storagePath, "current.json");
        var json = System.Text.Json.JsonSerializer.Serialize(version, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(currentVersionFile, json);
    }

    public async Task DeleteVersionAsync(string versionName)
    {
        var versionDir = Path.Combine(_storagePath, "versions", versionName);
        if (Directory.Exists(versionDir))
        {
            Directory.Delete(versionDir, true);
        }

        // If this was the current version, clear it
        var current = await GetCurrentVersionAsync();
        if (current?.Name == versionName)
        {
            var currentVersionFile = Path.Combine(_storagePath, "current.json");
            if (File.Exists(currentVersionFile))
                File.Delete(currentVersionFile);
        }
    }

    private async Task<string> CalculateDirectoryHashAsync(string directoryPath)
    {
        using var sha256 = SHA256.Create();
        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                            .OrderBy(f => f)
                            .ToList();

        var combinedHash = new StringBuilder();
        foreach (var file in files)
        {
            var fileBytes = await File.ReadAllBytesAsync(file);
            var fileHash = sha256.ComputeHash(fileBytes);
            combinedHash.Append(Convert.ToHexString(fileHash));
        }

        var finalHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedHash.ToString()));
        return Convert.ToHexString(finalHash);
    }

    private long CalculateDirectorySize(string directoryPath)
    {
        return Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                       .Sum(file => new FileInfo(file).Length);
    }
}