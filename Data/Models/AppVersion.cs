using System;
using System.Collections.Generic;

namespace EasyPatchy3.Data.Models
{
    public class AppVersion
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;

        public virtual ICollection<Patch> SourcePatches { get; set; } = new List<Patch>();
        public virtual ICollection<Patch> TargetPatches { get; set; } = new List<Patch>();
    }
}