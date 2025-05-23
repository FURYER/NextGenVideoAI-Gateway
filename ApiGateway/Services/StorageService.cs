using ApiGateway.GraphQL;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using NextGenVideoAI.StorageService.Api;
using System.IO;

namespace ApiGateway.Services
{
    public class StorageService : IStorageService
    {
        private readonly Storage.StorageClient _client;
        private readonly ILogger<StorageService> _logger;

        public StorageService(ILogger<StorageService> logger)
        {
            var storageServiceUrl = Environment.GetEnvironmentVariable("STORAGE_SERVICE_URL");

            var channel = GrpcChannel.ForAddress(storageServiceUrl!, new GrpcChannelOptions
            {
                HttpHandler = new System.Net.Http.SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true
                },
                Credentials = ChannelCredentials.Insecure,
                MaxReceiveMessageSize = null,
                MaxSendMessageSize = null
            });
            _client = new Storage.StorageClient(channel);
            _logger = logger;
        }

        public async Task<FileMetadata> GetFileMetadata(string fileId, string userId)
        {
            try
            {
                var request = new GetFileInfoRequest
                {
                    FileId = fileId
                };

                var response = await _client.GetFileInfoAsync(request);

                if (response.UserId != userId)
                {
                    throw new UnauthorizedAccessException("У вас нет доступа к этому файлу");
                }

                return new FileMetadata
                {
                    FileName = response.FileName,
                    ContentType = response.ContentType,
                    Size = response.FileSize
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error getting file metadata: {Detail}", ex.Status.Detail);
                throw;
            }
        }

        public async Task<Stream> DownloadFile(string fileId, string userId)
        {
            try
            {
                var request = new DownloadFileRequest
                {
                    FileId = fileId
                };

                _logger.LogInformation("Starting streaming download of file {FileId}", fileId);

                var call = _client.DownloadFileStream(request);

                // Создаем MemoryStream для буферизации
                var memoryStream = new MemoryStream();

                try
                {
                    await foreach (var chunk in call.ResponseStream.ReadAllAsync())
                    {
                        if (!chunk.Success)
                        {
                            _logger.LogWarning("Error in downloading file {FileId}: {Error}", fileId, chunk.ErrorMessage);
                            throw new Exception(chunk.ErrorMessage);
                        }

                        await memoryStream.WriteAsync(chunk.ChunkData.ToByteArray());
                        _logger.LogDebug("Downloaded chunk {Index} of file {FileId}", chunk.ChunkIndex, fileId);

                        if (chunk.IsLastChunk)
                        {
                            _logger.LogInformation("File {FileId} download completed", fileId);
                            break;
                        }
                    }

                    memoryStream.Position = 0;
                    return memoryStream;
                }
                catch
                {
                    memoryStream.Dispose();
                    throw;
                }
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error downloading file: {Detail}", ex.Status.Detail);
                throw;
            }
        }
    }
}