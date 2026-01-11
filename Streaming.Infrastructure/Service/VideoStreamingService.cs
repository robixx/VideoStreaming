using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using Streaming.Application.Interface;
using Streaming.Application.ViewModel;
using Streaming.Infrastructure.Data;
using Streaming.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Streaming.Infrastructure.Service
{
    public class VideoStreamingService : IVideoStream
    {
        private readonly IConfiguration _configuration;
        private readonly DatabaseConnection _connection;
        private readonly string _imagePath;
        public VideoStreamingService(DatabaseConnection connection, IConfiguration configuration)
        {
            _connection = connection;
            _configuration = configuration;
            _imagePath = configuration["FileStorage:StorageType"]
                    ?? throw new ArgumentNullException("FileStorage:StorageType not configured");
        }
        public async Task<(string Message, bool Status)> SaveVideoAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return ("No file uploaded", false);

            try
            {
                // ----------- Step 4.1: Detect file type -----------
                var extension = Path.GetExtension(file.FileName)?.ToLower() ?? string.Empty;

                string fileType = extension switch
                {
                    ".mp3" or ".wav" => "Audio",
                    ".mp4" or ".mkv" or ".avi" => "Video",
                    ".jpg" or ".jpeg" or ".png" or ".gif" => "Image",
                    ".pdf" or ".txt" or ".csv" or ".doc" or ".docx"
                    or ".xls" or ".xlsx" or ".ppt" or ".pptx" => "Document",
                    _ => "Other"
                };

                // ----------- Step 4.2: Generate unique filename -----------
                int sixDigitId = Random.Shared.Next(100000, 999999);
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var storedFileName = $"{sixDigitId}_{timestamp}{extension}";

                string finalPath; // path to save (Local or SFTP)

                var storageType = _configuration["FileStorage:StorageType"];

                // ================= Step 4.3: Local Storage =================
                if (storageType == "Local")
                {
                    var basePath = _configuration["FileStorage:LocalBasePath"];
                    if (string.IsNullOrEmpty(basePath))
                        return ("LocalBasePath is not configured", false);

                    var folderPath = Path.Combine(basePath, fileType);

                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    finalPath = Path.Combine(folderPath, storedFileName);

                    await using var stream = new FileStream(finalPath, FileMode.Create);
                    await file.CopyToAsync(stream);
                }

                // ================= Step 4.4: SFTP Storage =================
                else if (storageType == "SFTP")
                {
                    var host = _configuration["FileStorage:SftpHost"];
                    var port = int.Parse(_configuration["FileStorage:SftpPort"] ?? "22");
                    var username = _configuration["FileStorage:SftpUsername"];
                    var password = _configuration["FileStorage:SftpPassword"];
                    var basePath = _configuration["FileStorage:SftpBasePath"];

                    if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                        return ("SFTP configuration missing", false);

                    var remoteFolder = $"{basePath}/{fileType}";
                    finalPath = $"{remoteFolder}/{storedFileName}";

                    using var sftp = new SftpClient(host, port, username, password);

                    try
                    {
                        sftp.Connect();

                        if (!sftp.Exists(remoteFolder))
                            sftp.CreateDirectory(remoteFolder);

                        using var fileStream = file.OpenReadStream();
                        sftp.UploadFile(fileStream, finalPath, true);

                        sftp.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        return ($"SFTP Error: {ex.Message}", false);
                    }
                }

                else
                {
                    return ("Invalid StorageType configured", false);
                }

                // ================= Step 4.5: Save metadata to DB =================
                var uploadedFile = new UploadedFile
                {
                    OriginalFileName = file.FileName,
                    StoredFileName = storedFileName,
                    FileExtension = extension,
                    FileType = fileType,
                    FilePath = finalPath,
                    UploadedAt = DateTime.Now
                };

                await _connection.UploadedFile.AddAsync(uploadedFile);
                await _connection.SaveChangesAsync();

                return ($"File uploaded successfully: {storedFileName}", true);
            }
            catch (Exception ex)
            {
                return ($"Unexpected Error: {ex.Message}", false);
            }
        }

        public async Task<List<(Stream FileStream, string ContentType, string FileName)>> GetVideoAsync(int sixDigitId)
        {
            var result = new List<(Stream FileStream, string ContentType, string FileName)>();
            var storageType = _configuration["FileStorage:StorageType"];

            try
            {
                // Get all matching files from DB
                var files = await _connection.UploadedFile
                    .Where(f => !string.IsNullOrEmpty(f.StoredFileName))
                    .Where(f => f.StoredFileName.StartsWith(sixDigitId.ToString() + "_"))
                    .OrderByDescending(f => f.StoredFileName)
                    .ToListAsync();

                foreach (var file in files)
                {
                    Stream stream;
                    // --------- LOCAL STORAGE ---------
                    if (storageType == "Local")
                    {
                        if (!System.IO.File.Exists(file.FilePath))
                            continue;

                        stream = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    // --------- SFTP STORAGE ---------
                    else if (storageType == "SFTP")
                    {
                        var host = _configuration["FileStorage:SftpHost"];
                        var port = int.Parse(_configuration["FileStorage:SftpPort"] ?? "22");
                        var username = _configuration["FileStorage:SftpUsername"];
                        var password = _configuration["FileStorage:SftpPassword"];

                        var sftp = new SftpClient(host, port, username, password);
                        sftp.Connect();

                        if (!sftp.Exists(file.FilePath))
                        {
                            sftp.Disconnect();
                            continue;
                        }

                        // Open stream from SFTP
                        stream = sftp.OpenRead(file.FilePath);
                        // ⚠️ Note: Caller must dispose both stream and sftp
                    }
                    else
                    {
                        continue;
                    }

                    var extension = string.IsNullOrWhiteSpace(file.FileExtension)
                        ? Path.GetExtension(file.OriginalFileName) ?? string.Empty
                        : file.FileExtension;

                    string contentType = extension.ToLower() switch
                    {
                        ".mp4" => "video/mp4",
                        ".mp3" => "audio/mpeg",
                        ".jpg" => "image/jpeg",
                        ".jpeg" => "image/jpeg",
                        ".png" => "image/png",
                        ".gif" => "image/gif",
                        ".pdf" => "application/pdf",
                        ".txt" => "text/plain",
                        ".csv" => "text/csv",
                        _ => "application/octet-stream"
                    };

                    result.Add((stream, contentType, file.OriginalFileName));
                }

                return result;
            }
            catch
            {
                return result; // return empty list if exception
            }
        }
    }
}
