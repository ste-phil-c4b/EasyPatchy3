using EasyPatchy3.Launcher.Models;

namespace EasyPatchy3.Launcher.Services;

public class UpdateService
{
    private readonly ApiService _apiService;
    private readonly LocalAppService _localAppService;
    private readonly PatchApplicationService _patchService;

    public UpdateService(ApiService apiService, LocalAppService localAppService, PatchApplicationService patchService)
    {
        _apiService = apiService;
        _localAppService = localAppService;
        _patchService = patchService;
    }

    public async Task<UpdateStrategy> DetermineUpdateStrategyAsync(string targetVersionName)
    {
        var currentVersion = await _localAppService.GetCurrentVersionAsync();
        var availableVersions = await _apiService.GetAvailableVersionsAsync();
        var targetVersion = availableVersions.FirstOrDefault(v => v.Name == targetVersionName);

        if (targetVersion == null)
        {
            throw new ArgumentException($"Version '{targetVersionName}' not found on server");
        }

        // If no current version, must download full app
        if (currentVersion == null)
        {
            return new UpdateStrategy
            {
                Type = UpdateType.FullDownload,
                TargetVersion = targetVersion,
                ReasonCode = "No current version installed"
            };
        }

        // If current version matches target, no update needed
        if (currentVersion.Name == targetVersionName)
        {
            return new UpdateStrategy
            {
                Type = UpdateType.NoUpdateNeeded,
                TargetVersion = targetVersion,
                ReasonCode = "Already on target version"
            };
        }

        // Try to find a patch from current to target version
        var currentVersionDto = availableVersions.FirstOrDefault(v => v.Name == currentVersion.Name);
        if (currentVersionDto == null)
        {
            return new UpdateStrategy
            {
                Type = UpdateType.FullDownload,
                TargetVersion = targetVersion,
                ReasonCode = "Current version not found on server, full download required"
            };
        }

        var patch = await _apiService.GetPatchAsync(currentVersionDto.Id, targetVersion.Id);
        if (patch == null)
        {
            return new UpdateStrategy
            {
                Type = UpdateType.FullDownload,
                TargetVersion = targetVersion,
                ReasonCode = "No patch available, full download required"
            };
        }

        // Check if patch is smaller than full download (with some threshold)
        var patchEfficiencyThreshold = 0.8; // If patch is >80% of full size, prefer full download
        if (patch.PatchSize > (targetVersion.Size * patchEfficiencyThreshold))
        {
            return new UpdateStrategy
            {
                Type = UpdateType.FullDownload,
                TargetVersion = targetVersion,
                ReasonCode = "Patch too large, full download more efficient"
            };
        }

        return new UpdateStrategy
        {
            Type = UpdateType.PatchUpdate,
            TargetVersion = targetVersion,
            Patch = patch,
            CurrentVersion = currentVersion,
            ReasonCode = "Patch update available"
        };
    }

    public async Task<bool> ExecuteUpdateAsync(UpdateStrategy strategy, IProgress<UpdateProgress>? progress = null)
    {
        progress?.Report(new UpdateProgress { Stage = "Starting update", Percentage = 0 });

        switch (strategy.Type)
        {
            case UpdateType.FullDownload:
                return await ExecuteFullDownloadAsync(strategy.TargetVersion, progress);

            case UpdateType.PatchUpdate:
                return await ExecutePatchUpdateAsync(strategy, progress);

            case UpdateType.NoUpdateNeeded:
                progress?.Report(new UpdateProgress { Stage = "No update needed", Percentage = 100 });
                return true;

            default:
                throw new InvalidOperationException($"Unknown update type: {strategy.Type}");
        }
    }

    private async Task<bool> ExecuteFullDownloadAsync(AppVersionDto targetVersion, IProgress<UpdateProgress>? progress)
    {
        try
        {
            progress?.Report(new UpdateProgress { Stage = "Downloading version", Percentage = 10 });

            using var downloadStream = await _apiService.DownloadVersionAsync(targetVersion.Id);

            progress?.Report(new UpdateProgress { Stage = "Installing version", Percentage = 70 });

            await _localAppService.InstallVersionAsync(targetVersion.Name, downloadStream, setAsCurrent: true);

            progress?.Report(new UpdateProgress { Stage = "Update complete", Percentage = 100 });

            return true;
        }
        catch (Exception ex)
        {
            progress?.Report(new UpdateProgress { Stage = $"Update failed: {ex.Message}", Percentage = 0 });
            return false;
        }
    }

    private async Task<bool> ExecutePatchUpdateAsync(UpdateStrategy strategy, IProgress<UpdateProgress>? progress)
    {
        try
        {
            if (strategy.Patch == null || strategy.CurrentVersion == null)
                return false;

            progress?.Report(new UpdateProgress { Stage = "Downloading patch", Percentage = 10 });

            using var patchStream = await _apiService.DownloadPatchAsync(strategy.Patch.Id);

            progress?.Report(new UpdateProgress { Stage = "Applying patch", Percentage = 50 });

            var success = await _patchService.ApplyPatchAsync(
                strategy.CurrentVersion,
                patchStream,
                strategy.TargetVersion.Name);

            if (!success)
            {
                progress?.Report(new UpdateProgress { Stage = "Patch failed, trying full download", Percentage = 60 });
                return await ExecuteFullDownloadAsync(strategy.TargetVersion, progress);
            }

            progress?.Report(new UpdateProgress { Stage = "Update complete", Percentage = 100 });

            return true;
        }
        catch (Exception ex)
        {
            progress?.Report(new UpdateProgress { Stage = $"Patch failed: {ex.Message}, trying full download", Percentage = 60 });
            return await ExecuteFullDownloadAsync(strategy.TargetVersion, progress);
        }
    }
}

public class UpdateStrategy
{
    public UpdateType Type { get; set; }
    public AppVersionDto TargetVersion { get; set; } = null!;
    public PatchDto? Patch { get; set; }
    public LocalAppVersion? CurrentVersion { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
}

public enum UpdateType
{
    NoUpdateNeeded,
    FullDownload,
    PatchUpdate
}

public class UpdateProgress
{
    public string Stage { get; set; } = string.Empty;
    public int Percentage { get; set; }
}