using Microsoft.AspNetCore.Mvc;
using EasyPatchy3.Services;
using EasyPatchy3.Data.Models;

namespace EasyPatchy3.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatchesController : ControllerBase
{
    private readonly IPatchService _patchService;
    private readonly IVersionService _versionService;

    public PatchesController(IPatchService patchService, IVersionService versionService)
    {
        _patchService = patchService;
        _versionService = versionService;
    }

    [HttpGet("{sourceVersionId}/{targetVersionId}")]
    public async Task<ActionResult<PatchDto>> GetPatch(int sourceVersionId, int targetVersionId)
    {
        try
        {
            var patch = await _patchService.GetPatchAsync(sourceVersionId, targetVersionId);
            if (patch == null)
            {
                // Try to generate the patch if it doesn't exist
                try
                {
                    patch = await _patchService.GeneratePatchAsync(sourceVersionId, targetVersionId);
                }
                catch
                {
                    return NotFound(new { error = "Patch not available and cannot be generated" });
                }
            }

            // Load source and target versions for the DTO
            var sourceVersion = await _versionService.GetVersionAsync(sourceVersionId);
            var targetVersion = await _versionService.GetVersionAsync(targetVersionId);

            if (sourceVersion == null || targetVersion == null)
                return NotFound(new { error = "Source or target version not found" });

            var patchDto = new PatchDto
            {
                Id = patch.Id,
                SourceVersionId = patch.SourceVersionId,
                TargetVersionId = patch.TargetVersionId,
                PatchSize = patch.PatchSize,
                CreatedAt = patch.CreatedAt,
                Status = patch.Status.ToString(),
                SourceVersion = new AppVersionDto
                {
                    Id = sourceVersion.Id,
                    Name = sourceVersion.Name,
                    Description = sourceVersion.Description,
                    CreatedAt = sourceVersion.CreatedAt,
                    Size = sourceVersion.Size,
                    Hash = sourceVersion.Hash
                },
                TargetVersion = new AppVersionDto
                {
                    Id = targetVersion.Id,
                    Name = targetVersion.Name,
                    Description = targetVersion.Description,
                    CreatedAt = targetVersion.CreatedAt,
                    Size = targetVersion.Size,
                    Hash = targetVersion.Hash
                }
            };

            return Ok(patchDto);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve patch", details = ex.Message });
        }
    }

    [HttpGet("{patchId}/download")]
    public async Task<IActionResult> DownloadPatch(int patchId)
    {
        try
        {
            var patchBytes = await _patchService.GetPatchFileAsync(patchId);
            if (patchBytes == null)
                return NotFound(new { error = "Patch file not found" });

            return File(patchBytes, "application/octet-stream", $"patch_{patchId}.hdiff");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to download patch", details = ex.Message });
        }
    }

    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<PatchDto>>> GetPatchesForVersion(int versionId)
    {
        try
        {
            var patches = await _patchService.GetPatchesForVersionAsync(versionId);
            var version = await _versionService.GetVersionAsync(versionId);

            if (version == null)
                return NotFound(new { error = "Version not found" });

            var patchDtos = new List<PatchDto>();
            foreach (var patch in patches)
            {
                var sourceVersion = await _versionService.GetVersionAsync(patch.SourceVersionId);
                var targetVersion = await _versionService.GetVersionAsync(patch.TargetVersionId);

                if (sourceVersion != null && targetVersion != null)
                {
                    patchDtos.Add(new PatchDto
                    {
                        Id = patch.Id,
                        SourceVersionId = patch.SourceVersionId,
                        TargetVersionId = patch.TargetVersionId,
                        PatchSize = patch.PatchSize,
                        CreatedAt = patch.CreatedAt,
                        Status = patch.Status.ToString(),
                        SourceVersion = new AppVersionDto
                        {
                            Id = sourceVersion.Id,
                            Name = sourceVersion.Name,
                            Description = sourceVersion.Description,
                            CreatedAt = sourceVersion.CreatedAt,
                            Size = sourceVersion.Size,
                            Hash = sourceVersion.Hash
                        },
                        TargetVersion = new AppVersionDto
                        {
                            Id = targetVersion.Id,
                            Name = targetVersion.Name,
                            Description = targetVersion.Description,
                            CreatedAt = targetVersion.CreatedAt,
                            Size = targetVersion.Size,
                            Hash = targetVersion.Hash
                        }
                    });
                }
            }

            return Ok(patchDtos);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve patches", details = ex.Message });
        }
    }
}

public class PatchDto
{
    public int Id { get; set; }
    public int SourceVersionId { get; set; }
    public int TargetVersionId { get; set; }
    public long PatchSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public AppVersionDto SourceVersion { get; set; } = null!;
    public AppVersionDto TargetVersion { get; set; } = null!;
}