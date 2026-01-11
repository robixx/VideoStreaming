using Streaming.Application.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Streaming.Application.Interface
{
    public interface IVideoStream
    {
        Task<(String Message, bool Status)> SaveVideoAsync(IFormFile file);
        Task<List<(Stream FileStream, string ContentType, string FileName)>> GetVideoAsync(int id);
    }
}
