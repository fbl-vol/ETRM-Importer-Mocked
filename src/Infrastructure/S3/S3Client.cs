using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using Infrastructure.Configuration;
using Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

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
        
        // NOTE: Using GetAwaiter().GetResult() for simplicity in this demo.
        // For production, consider using a factory pattern or lazy initialization to avoid potential deadlocks.
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
        using var activity = Telemetry.ActivitySource.StartActivity("s3.upload", ActivityKind.Client);
        activity?.SetTag("s3.bucket", _options.BucketName);
        activity?.SetTag("s3.key", objectKey);
        activity?.SetTag("s3.content_type", contentType);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var fileSize = fileStream.Length;
            activity?.SetTag("s3.file_size", fileSize);

            var request = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = contentType
            };

            var response = await _s3Client.PutObjectAsync(request, cancellationToken);
            
            stopwatch.Stop();
            Telemetry.FileUploadDuration.Record(stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("bucket", _options.BucketName));
            Telemetry.FileSize.Record(fileSize,
                new KeyValuePair<string, object?>("bucket", _options.BucketName));
            Telemetry.FilesUploaded.Add(1,
                new KeyValuePair<string, object?>("bucket", _options.BucketName));

            _logger.LogInformation("Uploaded object {ObjectKey} to bucket {Bucket} ({Size} bytes) in {Duration}ms", 
                objectKey, _options.BucketName, fileSize, stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Failed to upload object {ObjectKey} to bucket {Bucket} after {Duration}ms", 
                objectKey, _options.BucketName, stopwatch.ElapsedMilliseconds);
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
