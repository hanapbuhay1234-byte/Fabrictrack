using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FabriTrack.Services
{
    public interface IImageStorage
    {
        Task<string> SaveAsync(string category, IFormFile file, CancellationToken cancellationToken = default);
        Task<StoredImage?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
        Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    }

    public sealed record StoredImage(Stream Content, string ContentType);

    public sealed class ImageStorageException : Exception
    {
        public ImageStorageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class ImageStorage : IImageStorage
    {
        private static readonly Regex ImageKitKeyPattern = new(
            @"\Aimagekit:([a-zA-Z0-9_-]+):/(profiles|fabrics)/([a-f0-9]{32}\.(?:jpg|jpeg|png|webp))\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex LocalObjectKeyPattern = new(
            @"\A(?:profiles|fabrics)/[a-f0-9]{32}\.(?:jpg|jpeg|png|webp)\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex LegacyProfileFileNamePattern = new(
            @"\A[a-f0-9]{32}\.(?:jpg|jpeg|png|webp)\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex LegacyFabricPathPattern = new(
            @"\A/uploads/fabrics/[a-f0-9]{32}\.(?:jpg|jpeg|png|webp)\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.Ordinal)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageStorage> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _provider;
        private readonly string? _imageKitId;
        private readonly string? _privateKey;

        public ImageStorage(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<ImageStorage> logger,
            IHttpClientFactory httpClientFactory)
        {
            _environment = environment;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _provider = configuration["ImageStorage:Provider"] ??
                (environment.IsDevelopment()
                    ? "Local"
                    : throw new InvalidOperationException("ImageStorage:Provider must be set outside Development."));

            if (string.Equals(_provider, "Local", StringComparison.OrdinalIgnoreCase))
            {
                if (!environment.IsDevelopment())
                    throw new InvalidOperationException("Local image storage is only available in Development.");
                return;
            }

            if (!string.Equals(_provider, "ImageKit", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ImageStorage:Provider must be Local or ImageKit.");

            _imageKitId = configuration["ImageStorage:ImageKitId"];
            _privateKey = configuration["ImageStorage:PrivateKey"];
            if (string.IsNullOrWhiteSpace(_imageKitId) ||
                !Regex.IsMatch(_imageKitId, @"\A[a-zA-Z0-9_-]+\z", RegexOptions.CultureInvariant) ||
                string.IsNullOrWhiteSpace(_privateKey))
            {
                throw new InvalidOperationException(
                    "ImageStorage:ImageKitId and ImageStorage:PrivateKey must be configured when using ImageKit.");
            }
        }

        public async Task<string> SaveAsync(
            string category,
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (category is not ("profiles" or "fabrics") || !AllowedExtensions.Contains(extension))
                throw new ArgumentException("The image category or file extension is not supported.");

            var fileName = $"{Guid.NewGuid():N}{extension}";

            try
            {
                await using var content = file.OpenReadStream();
                if (string.Equals(_provider, "ImageKit", StringComparison.OrdinalIgnoreCase))
                    return await UploadToImageKitAsync(category, fileName, content, cancellationToken);

                var key = $"{category}/{fileName}";
                var path = GetLocalPath(key);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using var output = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                await content.CopyToAsync(output, cancellationToken);
                _logger.LogInformation("Stored image object {ImageKey} using {StorageProvider}.", key, _provider);
                return key;
            }
            catch (ImageStorageException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or HttpRequestException or JsonException)
            {
                throw new ImageStorageException("The uploaded image could not be stored.", exception);
            }
        }

        public async Task<StoredImage?> OpenReadAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            if (!TryNormalizeLocalKey(key, out var localKey) && !ImageKitKeyPattern.IsMatch(key))
                return null;

            try
            {
                if (ImageKitKeyPattern.IsMatch(key))
                {
                    var match = ImageKitKeyPattern.Match(key);
                    var filePath = $"/{match.Groups[2].Value}/{match.Groups[3].Value}";
                    var signedUrl = BuildSignedImageUrl(filePath);
                    using var response = await _httpClientFactory.CreateClient().GetAsync(
                        signedUrl,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return null;

                    response.EnsureSuccessStatusCode();
                    var content = new MemoryStream();
                    try
                    {
                        await response.Content.CopyToAsync(content, cancellationToken);
                        content.Position = 0;
                        var contentType = response.Content.Headers.ContentType?.MediaType ??
                            GetContentType(filePath);
                        return new StoredImage(content, contentType);
                    }
                    catch
                    {
                        await content.DisposeAsync();
                        throw;
                    }
                }

                var path = GetLocalPath(localKey);
                if (!File.Exists(path))
                    return null;

                return new StoredImage(
                    new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read),
                    GetContentType(localKey));
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or HttpRequestException or JsonException)
            {
                throw new ImageStorageException("The requested image could not be read from storage.", exception);
            }
        }

        public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            if (ImageKitKeyPattern.IsMatch(key))
            {
                var fileId = ImageKitKeyPattern.Match(key).Groups[1].Value;
                try
                {
                    using var request = new HttpRequestMessage(
                        HttpMethod.Delete,
                        $"https://api.imagekit.io/v1/files/{Uri.EscapeDataString(fileId)}");
                    AddImageKitAuthentication(request);
                    using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
                    if (response.StatusCode != HttpStatusCode.NotFound)
                        response.EnsureSuccessStatusCode();
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException)
                {
                    throw new ImageStorageException("The image object could not be deleted.", exception);
                }
                return;
            }

            if (!TryNormalizeLocalKey(key, out var localKey))
                return;

            try
            {
                var path = GetLocalPath(localKey);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new ImageStorageException("The image object could not be deleted.", exception);
            }
        }

        private async Task<string> UploadToImageKitAsync(
            string category,
            string fileName,
            Stream content,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://upload.imagekit.io/api/v1/files/upload");
            AddImageKitAuthentication(request);

            using var multipart = new MultipartFormDataContent();
            var fileContent = new StreamContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(fileName));
            multipart.Add(fileContent, "file", fileName);
            multipart.Add(new StringContent(fileName), "fileName");
            multipart.Add(new StringContent($"/{category}"), "folder");
            multipart.Add(new StringContent("true"), "isPrivateFile");
            multipart.Add(new StringContent("false"), "useUniqueFileName");
            request.Content = multipart;

            using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var uploadedFile = await JsonSerializer.DeserializeAsync<ImageKitUploadResponse>(
                responseStream,
                JsonOptions,
                cancellationToken);
            if (uploadedFile == null || string.IsNullOrWhiteSpace(uploadedFile.FileId))
            {
                throw new JsonException("ImageKit's upload response did not include a file ID.");
            }

            var expectedFilePath = $"/{category}/{fileName}";
            if (uploadedFile.FilePath != null &&
                !string.Equals(uploadedFile.FilePath, expectedFilePath, StringComparison.Ordinal))
            {
                throw new JsonException("ImageKit returned an unexpected file path.");
            }

            if (uploadedFile.IsPrivateFile == false)
                throw new JsonException("ImageKit did not mark the uploaded file as private.");

            var filePath = uploadedFile.FilePath ?? expectedFilePath;
            var key = $"imagekit:{uploadedFile.FileId}:{filePath}";
            _logger.LogInformation("Stored image object in ImageKit for category {ImageCategory}.", category);
            return key;
        }

        private string BuildSignedImageUrl(string filePath)
        {
            var expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 300;
            var relativePath = filePath.TrimStart('/');
            var signatureData = relativePath + expiry.ToString(System.Globalization.CultureInfo.InvariantCulture);
            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_privateKey!));
            var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureData)))
                .ToLowerInvariant();
            return $"https://ik.imagekit.io/{_imageKitId}/{filePath.TrimStart('/')}?ik-t={expiry}&ik-s={signature}";
        }

        private void AddImageKitAuthentication(HttpRequestMessage request)
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_privateKey}:"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        private bool TryNormalizeLocalKey(string key, out string normalizedKey)
        {
            normalizedKey = key;
            if (LocalObjectKeyPattern.IsMatch(key))
                return true;

            if (LegacyProfileFileNamePattern.IsMatch(key))
            {
                normalizedKey = $"profiles/{key}";
                return true;
            }

            if (LegacyFabricPathPattern.IsMatch(key))
            {
                normalizedKey = $"fabrics/{Path.GetFileName(key)}";
                return true;
            }

            return false;
        }

        private string GetLocalPath(string key)
        {
            var fileName = Path.GetFileName(key);
            return key.StartsWith("profiles/", StringComparison.Ordinal)
                ? Path.Combine(_environment.ContentRootPath, "App_Data", "ProfilePhotos", fileName)
                : Path.Combine(_environment.WebRootPath, "uploads", "fabrics", fileName);
        }

        private static string GetContentType(string key) =>
            Path.GetExtension(key).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

        private sealed record ImageKitUploadResponse(string? FileId, string? FilePath, bool? IsPrivateFile);
    }
}
