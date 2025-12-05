namespace Infrastructure.S3;

public interface IS3Client
{
    Task UploadFileAsync(string objectKey, Stream fileStream, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadFileAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default);
}
