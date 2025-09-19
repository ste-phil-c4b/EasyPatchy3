using System;

namespace EasyPatchy3.Data.Models
{
    public class Download
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DownloadType Type { get; set; }
        public int? VersionId { get; set; }
        public int? PatchId { get; set; }
        public DateTime DownloadedAt { get; set; }
        public string ClientIp { get; set; } = string.Empty;

        public virtual AppVersion? Version { get; set; }
        public virtual Patch? Patch { get; set; }
    }

    public enum DownloadType
    {
        Version,
        Patch
    }
}