using Microsoft.AspNetCore.Mvc;
using EasyPatchy3.Services;
using EasyPatchy3.Data.Models;

namespace EasyPatchy3.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersionsController : ControllerBase
{
    private readonly IVersionService _versionService;
    private readonly IStorageService _storageService;

    public VersionsController(IVersionService versionService, IStorageService storageService)
    {
        _versionService = versionService;
        _storageService = storageService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AppVersionDto>>> GetVersions()
    {
        try
        {
            var versions = await _versionService.GetAllVersionsAsync();
            var versionDtos = versions.Select(v => new AppVersionDto
            {
                Id = v.Id,
                Name = v.Name,
                Description = v.Description,
                CreatedAt = v.CreatedAt,
                Size = v.Size,
                Hash = v.Hash
            }).ToList();

            return Ok(versionDtos);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve versions", details = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AppVersionDto>> GetVersion(int id)
    {
        try
        {
            var version = await _versionService.GetVersionAsync(id);
            if (version == null)
                return NotFound(new { error = "Version not found" });

            var versionDto = new AppVersionDto
            {
                Id = version.Id,
                Name = version.Name,
                Description = version.Description,
                CreatedAt = version.CreatedAt,
                Size = version.Size,
                Hash = version.Hash
            };

            return Ok(versionDto);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve version", details = ex.Message });
        }
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadVersion(int id)
    {
        try
        {
            var version = await _versionService.GetVersionAsync(id);
            if (version == null)
                return NotFound(new { error = "Version not found" });

            var archiveBytes = await _storageService.GetVersionArchiveAsync(version.Name, useVersionName: true);
            if (archiveBytes == null)
                return NotFound(new { error = "Version archive not found" });

            return File(archiveBytes, "application/zip", $"{version.Name}.zip");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to download version", details = ex.Message });
        }
    }
}

public class AppVersionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public long Size { get; set; }
    public string Hash { get; set; } = string.Empty;
}