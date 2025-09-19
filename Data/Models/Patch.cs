using System;

namespace EasyPatchy3.Data.Models
{
    public class Patch
    {
        public int Id { get; set; }
        public int SourceVersionId { get; set; }
        public int TargetVersionId { get; set; }
        public string PatchFilePath { get; set; } = string.Empty;
        public long PatchSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public PatchStatus Status { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public virtual AppVersion SourceVersion { get; set; } = null!;
        public virtual AppVersion TargetVersion { get; set; } = null!;
    }

    public enum PatchStatus
    {
        Pending,
        Processing,
        Completed,
        Failed
    }
}