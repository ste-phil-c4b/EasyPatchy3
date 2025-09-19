namespace EasyPatchy3.Launcher.Models;

public class AppVersionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public long Size { get; set; }
    public string Hash { get; set; } = string.Empty;
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

public class LocalAppVersion
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public DateTime InstalledAt { get; set; }
    public long Size { get; set; }
}