using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Streaming.Application.ViewModel
{
    public class UploadedFileDto
    {
        public int Id { get; set; }
        public string? OriginalFileName { get; set; }
        public string? StoredFileName { get; set; }
        public string? FileType { get; set; }   // Audio / Video / Other
        public string? FileExtension { get; set; }
        public string? FilePath { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
