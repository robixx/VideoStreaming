using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Streaming.Application.Interface;
using System.Net.NetworkInformation;

namespace WebStreaming.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideoHandleController : Controller
    {

        private readonly IVideoStream _stream;

        public VideoHandleController(IVideoStream stream)
        {
            _stream = stream;
        }

        [HttpPost("upload-video")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var data=await _stream.SaveVideoAsync(file);

            return Ok(new
            {
                data.Status,
                data.Message,
            });
        }

        [HttpGet("file/{sixDigitId}")]
        public async Task<IActionResult> GetFile(int sixDigitId, bool download = false)
        {
            // Get the latest file for the given 6-digit ID
            var files = await _stream.GetVideoAsync(sixDigitId);

            if (files == null || files.Count == 0)
                return NotFound(new { status = false, message = "File not found" });

            // Pick the latest file (first in the list)
            var file = files.First();

            if (file.FileStream == null)
                return NotFound(new { status = false, message = "File missing on disk" });

            if (download)
            {
                // Force download
                return File(file.FileStream, file.ContentType, file.FileName);
            }
            else
            {
                // Stream/view in browser
                return File(file.FileStream, file.ContentType, enableRangeProcessing: true);
            }
        }
    }
}
