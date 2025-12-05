using Amazon.S3;
using Amazon.S3.Model;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.S3;

public class S3Client : IS3Client, IDisposable
{
    private readonly AmazonS3Client _s3Client;
    private readonly S3Options _options;
    private readonly ILogger<S3Client> _logger;

    public S3Client(IOptions<S3Options> options, ILogger<S3Client> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = _options.ForcePathStyle
        };

        _s3Client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
        
        // Ensure bucket exists
        EnsureBucketExistsAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            await _s3Client.PutBucketAsync(_options.BucketName);
            _logger.LogInformation("Created or verified bucket {Bucket}", _options.BucketName);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "BucketAlreadyOwnedByYou")
        {
            _logger.LogDebug("Bucket {Bucket} already exists", _options.BucketName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create bucket {Bucket}", _options.BucketName);
        }
    }

    public async Task UploadFileAsync(string objectKey, Stream fileStream, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = contentType
            };

            var response = await _s3Client.PutObjectAsync(request, cancellationToken);
            _logger.LogInformation("Uploaded object {ObjectKey} to bucket {Bucket}", objectKey, _options.BucketName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload object {ObjectKey} to bucket {Bucket}", objectKey, _options.BucketName);
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey
            };

            var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            _logger.LogInformation("Downloaded object {ObjectKey} from bucket {Bucket}", objectKey, _options.BucketName);
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download object {ObjectKey} from bucket {Bucket}", objectKey, _options.BucketName);
            throw;
        }
    }

    public async Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey
            };

            await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
