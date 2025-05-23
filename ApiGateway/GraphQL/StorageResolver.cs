using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using NextGenVideoAI.StorageService.Api;
using System.IO;
using ApiGateway.Services;
using HotChocolate.Subscriptions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ApiGateway.GraphQL
{
    public class StorageResolver
    {
        private readonly Storage.StorageClient _client;
        private readonly ILogger<StorageResolver> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUploadStateService _uploadStateService;
        private readonly ITopicEventSender _topicEventSender;

        public StorageResolver(
            ILogger<StorageResolver> logger,
            IHttpContextAccessor httpContextAccessor,
            IUploadStateService uploadStateService,
            ITopicEventSender topicEventSender)
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
            _httpContextAccessor = httpContextAccessor;
            _uploadStateService = uploadStateService;
            _topicEventSender = topicEventSender;
        }

        public async Task<UploadFileResult> UploadFile(string userId, string fileName, string contentType, byte[] fileData, string? chatId = null)
        {
            try
            {
                _logger.LogInformation("Uploading file {FileName} for user {UserId}", fileName, userId);

                // Создаем поток для загрузки файла
                using var call = _client.UploadFileStream();

                // Определяем размер чанка (1 МБ)
                const int chunkSize = 1024 * 1024;
                var totalSize = fileData.Length;
                var totalChunks = (int)Math.Ceiling((double)totalSize / chunkSize);

                _logger.LogInformation("Starting streaming upload of file {FileName}, size: {Size}, chunks: {Chunks}",
                    fileName, totalSize, totalChunks);

                // Отправляем файл по частям
                for (int i = 0; i < totalChunks; i++)
                {
                    int currentChunkSize = Math.Min(chunkSize, totalSize - i * chunkSize);
                    byte[] chunkData = new byte[currentChunkSize];
                    Array.Copy(fileData, i * chunkSize, chunkData, 0, currentChunkSize);

                    var chunk = new UploadFileChunkRequest
                    {
                        ChunkData = Google.Protobuf.ByteString.CopyFrom(chunkData),
                        ChunkIndex = i,
                        IsLastChunk = (i == totalChunks - 1)
                    };

                    // Для первого чанка добавляем информацию о файле
                    if (i == 0)
                    {
                        chunk.UserId = userId;
                        chunk.FileName = fileName;
                        chunk.ContentType = contentType;
                        chunk.TotalSize = totalSize;

                        if (!string.IsNullOrEmpty(chatId))
                        {
                            chunk.ChatId = chatId;
                        }
                    }

                    await call.RequestStream.WriteAsync(chunk);
                    _logger.LogDebug("Uploaded chunk {Index}/{Total} of file {FileName}", i + 1, totalChunks, fileName);
                }

                // Завершаем поток отправки и получаем ответ
                await call.RequestStream.CompleteAsync();
                var response = await call;

                _logger.LogInformation("File {FileName} upload completed with success: {Success}", fileName, response.Success);

                return new UploadFileResult
                {
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage,
                    FileId = response.FileId,
                    FileSize = response.FileSize
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error uploading file: {Detail}", ex.Status.Detail);
                return new UploadFileResult
                {
                    Success = false,
                    ErrorMessage = ex.Status.Detail ?? "Ошибка при загрузке файла"
                };
            }
        }

        public async Task<FileModel> GetFileInfo(string fileId)
        {
            try
            {
                var request = new GetFileInfoRequest
                {
                    FileId = fileId
                };

                var response = await _client.GetFileInfoAsync(request);

                return new FileModel
                {
                    FileId = response.FileId,
                    FileName = response.FileName,
                    ContentType = response.ContentType,
                    FileSize = response.FileSize,
                    UserId = response.UserId,
                    ChatId = response.ChatId,
                    CreatedAt = response.CreatedAt,
                    LastAccessedAt = response.LastAccessedAt
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error getting file info: {Detail}", ex.Status.Detail);
                throw;
            }
        }

        [Authorize]
        public async Task<FileMetadata> GetFileMetadata(string fileId, [Service] ICurrentUserService currentUserService)
        {
            try
            {
                var userId = currentUserService.GetCurrentUserId();
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

        public async Task<DownloadFileResult> DownloadFile(string fileId)
        {
            try
            {
                var request = new DownloadFileRequest
                {
                    FileId = fileId
                };

                _logger.LogInformation("Starting streaming download of file {FileId}", fileId);

                using var call = _client.DownloadFileStream(request);

                // Используем MemoryStream для хранения данных
                using var memoryStream = new MemoryStream();

                string fileName = string.Empty;
                string contentType = string.Empty;
                long fileSize = 0;

                await foreach (var chunk in call.ResponseStream.ReadAllAsync())
                {
                    if (!chunk.Success)
                    {
                        _logger.LogWarning("Error in downloading file {FileId}: {Error}", fileId, chunk.ErrorMessage);
                        return new DownloadFileResult
                        {
                            Success = false,
                            ErrorMessage = chunk.ErrorMessage
                        };
                    }

                    if (chunk.ChunkIndex == 0)
                    {
                        fileName = chunk.FileName;
                        contentType = chunk.ContentType;
                        fileSize = chunk.TotalSize;
                        _logger.LogInformation("Download started for file {FileName}, type: {ContentType}, size: {Size}",
                            fileName, contentType, fileSize);
                    }

                    await memoryStream.WriteAsync(chunk.ChunkData.ToByteArray());
                    _logger.LogDebug("Downloaded chunk {Index} of file {FileId}", chunk.ChunkIndex, fileId);

                    if (chunk.IsLastChunk)
                    {
                        _logger.LogInformation("File {FileId} download completed", fileId);
                        break;
                    }
                }

                // Получаем данные файла
                var fileData = memoryStream.ToArray();

                return new DownloadFileResult
                {
                    Success = true,
                    FileData = fileData,
                    FileName = fileName,
                    ContentType = contentType,
                    FileSize = fileSize
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error downloading file: {Detail}", ex.Status.Detail);
                return new DownloadFileResult
                {
                    Success = false,
                    ErrorMessage = ex.Status.Detail ?? "Ошибка при скачивании файла"
                };
            }
        }

        public async Task<DeleteFileResult> DeleteFile(string fileId)
        {
            try
            {
                var request = new DeleteFileRequest
                {
                    FileId = fileId
                };

                var response = await _client.DeleteFileAsync(request);

                return new DeleteFileResult
                {
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error deleting file: {Detail}", ex.Status.Detail);
                return new DeleteFileResult
                {
                    Success = false,
                    ErrorMessage = ex.Status.Detail ?? "Ошибка при удалении файла"
                };
            }
        }

        public async Task<List<FileModel>> GetUserFiles(string userId, string? chatId = null, int skip = 0, int limit = 20)
        {
            try
            {
                var request = new GetUserFilesRequest
                {
                    UserId = userId,
                    Skip = skip,
                    Limit = limit
                };

                if (!string.IsNullOrEmpty(chatId))
                {
                    request.ChatId = chatId;
                }

                var response = await _client.GetUserFilesAsync(request);

                return response.Files.Select(file => new FileModel
                {
                    FileId = file.FileId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.FileSize,
                    UserId = file.UserId,
                    ChatId = file.ChatId,
                    CreatedAt = file.CreatedAt,
                    LastAccessedAt = file.LastAccessedAt
                }).ToList();
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error getting user files: {Detail}", ex.Status.Detail);
                throw;
            }
        }

        // Инициализация фрагментированной загрузки
        public async Task<InitChunkedUploadResult> InitChunkedUpload(InitChunkedUploadInput input)
        {
            try
            {
                _logger.LogInformation("Инициализация фрагментированной загрузки для пользователя {UserId} файла {FileName}",
                    input.UserId, input.FileName);

                // Добавляем проверку наличия ContentType и устанавливаем значение по умолчанию
                if (string.IsNullOrEmpty(input.ContentType))
                {
                    _logger.LogWarning("ContentType не указан для файла {FileName}, определяем по расширению", input.FileName);
                    input.ContentType = GetContentTypeFromFileName(input.FileName);
                }

                // Общее количество фрагментов, которое будет вычислено на основе размера файла 
                // и оптимального размера чанка
                var chunkSize = 5 * 1024 * 1024; // Размер чанка: 5 МБ
                int totalChunks = (int)Math.Ceiling((double)input.TotalSize / chunkSize);

                var request = new InitChunkedUploadRequest
                {
                    UserId = input.UserId,
                    ChatId = input.ChatId,
                    FileName = input.FileName,
                    ContentType = input.ContentType,
                    TotalSize = input.TotalSize,
                    TotalChunks = totalChunks
                };

                var response = await _client.InitChunkedUploadAsync(request);

                if (!response.Success)
                {
                    _logger.LogWarning("Ошибка при инициализации фрагментированной загрузки: {Error}", response.ErrorMessage);
                    return new InitChunkedUploadResult
                    {
                        Success = false,
                        ErrorMessage = response.ErrorMessage
                    };
                }

                // Преобразуем список старых пресайн URL в новый формат, если они есть
                var presignedUrlInfos = response.PresignedUrls != null
                    ? response.PresignedUrls.Select((url, index) => new PresignedUrl
                    {
                        Url = url,
                        PartNumber = index + 1
                    }).ToList()
                    : null;

                // Если в новом поле есть PresignedUrlInfos, используем их вместо старых
                if (response.PresignedUrlInfos?.Count > 0)
                {
                    presignedUrlInfos = response.PresignedUrlInfos.Select(p => new PresignedUrl
                    {
                        Url = p.Url,
                        PartNumber = p.PartNumber
                    }).ToList();
                }

                _logger.LogInformation("Фрагментированная загрузка инициализирована, uploadId: {UploadId}, totalChunks: {TotalChunks}",
                    response.UploadId, totalChunks);

                return new InitChunkedUploadResult
                {
                    Success = true,
                    UploadId = response.UploadId,
                    PresignedUrls = response.PresignedUrls?.Select((url, index) => new PresignedUrl
                    {
                        Url = url,
                        PartNumber = index + 1
                    }).ToList() ?? new List<PresignedUrl>(),
                    PresignedUrlInfos = presignedUrlInfos
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации фрагментированной загрузки");
                return new InitChunkedUploadResult
                {
                    Success = false,
                    ErrorMessage = "Внутренняя ошибка при инициализации загрузки: " + ex.Message
                };
            }
        }

        // Загрузка фрагмента файла
        public async Task<ChunkUploadResult> UploadFileChunk(ChunkUploadInput input)
        {
            try
            {
                _logger.LogInformation("Обработка загрузки фрагмента {ChunkIndex} для загрузки {UploadId}",
                    input.ChunkIndex, input.UploadId);

                var request = new UploadChunkRequest
                {
                    UploadId = input.UploadId,
                    ChunkIndex = input.ChunkIndex,
                    Etag = input.Etag
                };

                var response = await _client.UploadChunkAsync(request);

                if (!response.Success)
                {
                    _logger.LogWarning("Ошибка при загрузке фрагмента {ChunkIndex}: {Error}",
                        input.ChunkIndex, response.ErrorMessage);
                    return new ChunkUploadResult
                    {
                        Success = false,
                        ErrorMessage = response.ErrorMessage
                    };
                }

                _logger.LogInformation("Фрагмент {ChunkIndex} успешно загружен, прогресс: {Progress}%",
                    response.ChunkIndex, response.Progress);

                return new ChunkUploadResult
                {
                    Success = true,
                    ChunkIndex = response.ChunkIndex,
                    UploadId = response.UploadId,
                    Progress = response.Progress
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке фрагмента {ChunkIndex} для загрузки {UploadId}",
                    input.ChunkIndex, input.UploadId);
                return new ChunkUploadResult
                {
                    Success = false,
                    ErrorMessage = "Внутренняя ошибка при загрузке фрагмента: " + ex.Message
                };
            }
        }

        // Завершение фрагментированной загрузки
        public async Task<CompleteChunkedUploadResult> CompleteChunkedUpload(string uploadId, string userId)
        {
            try
            {
                _logger.LogInformation("Завершение фрагментированной загрузки {UploadId} для пользователя {UserId}",
                    uploadId, userId);

                var request = new CompleteChunkedUploadRequest
                {
                    UploadId = uploadId,
                    UserId = userId
                };

                var response = await _client.CompleteChunkedUploadAsync(request);

                if (!response.Success)
                {
                    _logger.LogWarning("Ошибка при завершении загрузки {UploadId}: {Error}",
                        uploadId, response.ErrorMessage);
                    return new CompleteChunkedUploadResult
                    {
                        Success = false,
                        ErrorMessage = response.ErrorMessage
                    };
                }

                _logger.LogInformation("Загрузка {UploadId} успешно завершена, fileId: {FileId}",
                    uploadId, response.FileId);

                return new CompleteChunkedUploadResult
                {
                    Success = true,
                    FileId = response.FileId,
                    FileSize = response.FileSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при завершении фрагментированной загрузки {UploadId}", uploadId);
                return new CompleteChunkedUploadResult
                {
                    Success = false,
                    ErrorMessage = "Внутренняя ошибка при завершении загрузки: " + ex.Message
                };
            }
        }

        // Отмена фрагментированной загрузки
        public async Task<CancelChunkedUploadResult> CancelChunkedUpload(string uploadId, string userId)
        {
            try
            {
                _logger.LogInformation("Отмена фрагментированной загрузки {UploadId} для пользователя {UserId}",
                    uploadId, userId);

                var request = new CancelChunkedUploadRequest
                {
                    UploadId = uploadId,
                    UserId = userId
                };

                var response = await _client.CancelChunkedUploadAsync(request);

                if (!response.Success)
                {
                    _logger.LogWarning("Ошибка при отмене загрузки {UploadId}: {Error}",
                        uploadId, response.ErrorMessage);
                    return new CancelChunkedUploadResult
                    {
                        Success = false,
                        ErrorMessage = response.ErrorMessage
                    };
                }

                _logger.LogInformation("Загрузка {UploadId} успешно отменена", uploadId);

                return new CancelChunkedUploadResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене фрагментированной загрузки {UploadId}", uploadId);
                return new CancelChunkedUploadResult
                {
                    Success = false,
                    ErrorMessage = "Внутренняя ошибка при отмене загрузки: " + ex.Message
                };
            }
        }

        // Получение статуса загрузки
        public async Task<UploadStatus> GetUploadStatus(string uploadId)
        {
            try
            {
                _logger.LogInformation("Getting status for upload {UploadId}", uploadId);

                // Используем новый метод из proto-файла
                var request = new GetUploadStatusRequest
                {
                    UploadId = uploadId
                };

                var response = await _client.GetUploadStatusAsync(request);

                // Если используем внешний статус, то обновляем наш локальный кэш
                if (response.Success)
                {
                    var uploadState = await _uploadStateService.GetUploadState(uploadId);
                    if (uploadState != null)
                    {
                        uploadState.FileName = response.FileName;
                        uploadState.ContentType = response.ContentType;
                        uploadState.TotalSize = response.TotalSize;
                        uploadState.TotalChunks = response.TotalChunks;
                        uploadState.UploadedChunks = response.UploadedChunks.ToList();
                        uploadState.IsComplete = response.IsComplete;
                        uploadState.FileId = response.FileId;
                        uploadState.Status = response.Status;
                        uploadState.LastUpdatedAt = DateTime.Parse(response.LastUpdatedAt);
                        await _uploadStateService.SaveUploadState(uploadId, uploadState);
                    }
                }

                return new UploadStatus
                {
                    UploadId = uploadId,
                    FileName = response.FileName,
                    ContentType = response.ContentType,
                    TotalSize = response.TotalSize,
                    TotalChunks = response.TotalChunks,
                    UploadedChunks = response.UploadedChunks.ToList(),
                    IsComplete = response.IsComplete,
                    FileId = response.FileId,
                    CreatedAt = response.CreatedAt,
                    LastUpdatedAt = response.LastUpdatedAt,
                    Status = response.Status,
                    Error = response.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting upload status");

                // Пытаемся вернуть локальную информацию, если доступна
                var uploadState = await _uploadStateService.GetUploadState(uploadId);
                if (uploadState != null)
                {
                    return new UploadStatus
                    {
                        UploadId = uploadId,
                        FileName = uploadState.FileName,
                        ContentType = uploadState.ContentType,
                        TotalSize = uploadState.TotalSize,
                        TotalChunks = uploadState.TotalChunks,
                        UploadedChunks = uploadState.UploadedChunks,
                        IsComplete = uploadState.IsComplete,
                        FileId = uploadState.FileId,
                        CreatedAt = uploadState.CreatedAt.ToString("o"),
                        LastUpdatedAt = uploadState.LastUpdatedAt.ToString("o"),
                        Status = uploadState.Status,
                        Error = ex.Message
                    };
                }

                throw;
            }
        }

        // Получение информации о квоте пользователя
        public async Task<UserQuotaInfo> GetUserQuotaInfo(string userId)
        {
            try
            {
                _logger.LogInformation("Getting quota info for user {UserId}", userId);

                // Используем метод из proto-файла
                var request = new GetUserQuotaInfoRequest
                {
                    UserId = userId
                };

                var response = await _client.GetUserQuotaInfoAsync(request);

                return new UserQuotaInfo
                {
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage,
                    StorageQuota = response.StorageQuota,
                    UsedStorage = response.UsedStorage
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error getting user quota info: {Detail}", ex.Status.Detail);
                return new UserQuotaInfo
                {
                    Success = false,
                    ErrorMessage = ex.Status.Detail ?? "Ошибка при получении информации о квоте"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user quota info");
                return new UserQuotaInfo
                {
                    Success = false,
                    ErrorMessage = ex.Message ?? "Ошибка при получении информации о квоте"
                };
            }
        }

        public async Task<FileChunkResponse> GetFileChunk(string fileId, long start, long end)
        {
            try
            {
                _logger.LogInformation("Getting file chunk for file {FileId} from {Start} to {End}", fileId, start, end);

                var request = new GetFileChunkRequest
                {
                    FileId = fileId,
                    Start = start,
                    End = end
                };

                var response = await _client.GetFileChunkAsync(request);

                return new FileChunkResponse
                {
                    Success = true,
                    ChunkData = response.ChunkData.ToByteArray(),
                    FileName = response.FileName,
                    ContentType = response.ContentType,
                    TotalSize = response.TotalSize
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error getting file chunk: {Detail}", ex.Status.Detail);
                return new FileChunkResponse
                {
                    Success = false,
                    ErrorMessage = ex.Status.Detail ?? "Ошибка при получении части файла"
                };
            }
        }

        public async Task<InitChunkedDownloadResult> InitChunkedDownload(string fileId)
        {
            try
            {
                var request = new InitChunkedDownloadRequest
                {
                    FileId = fileId,
                    UserId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty
                };

                var response = await _client.InitChunkedDownloadAsync(request);

                return new InitChunkedDownloadResult
                {
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage,
                    DownloadId = response.DownloadId,
                    FileName = response.FileName,
                    ContentType = response.ContentType,
                    TotalSize = response.TotalSize,
                    TotalChunks = response.TotalChunks
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error initializing chunked download");
                return new InitChunkedDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Status.Detail ?? "Ошибка при инициализации скачивания"
                };
            }
        }

        public async Task<GetChunkResult> GetChunk(string downloadId, int chunkIndex)
        {
            try
            {
                var request = new GetChunkRequest
                {
                    DownloadId = downloadId,
                    ChunkIndex = chunkIndex
                };

                var response = await _client.GetChunkAsync(request);

                return new GetChunkResult
                {
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage,
                    ChunkData = response.ChunkData.ToByteArray(),
                    ChunkIndex = response.ChunkIndex,
                    IsLastChunk = response.IsLastChunk
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error getting chunk");
                return new GetChunkResult
                {
                    Success = false,
                    ErrorMessage = ex.Status.Detail ?? "Ошибка при получении фрагмента"
                };
            }
        }

        public async Task<DownloadFileChunkResult> DownloadFileChunk(string fileId, int chunkIndex)
        {
            try
            {
                _logger.LogInformation("Получение фрагмента файла: {FileId}, фрагмент {ChunkIndex}", fileId, chunkIndex);

                // Получаем информацию о файле
                var fileInfo = await GetFileInfo(fileId);
                if (fileInfo == null)
                {
                    return new DownloadFileChunkResult
                    {
                        Success = false,
                        ErrorMessage = "Файл не найден"
                    };
                }

                // Вычисляем границы чанка
                const int chunkSize = 4 * 1024 * 1024; // 4 МБ
                long start = chunkIndex * chunkSize;
                long end = Math.Min(start + chunkSize, fileInfo.FileSize);

                // Получаем чанк файла через gRPC
                var chunkResponse = await GetFileChunk(fileId, start, end);
                if (!chunkResponse.Success)
                {
                    return new DownloadFileChunkResult
                    {
                        Success = false,
                        ErrorMessage = chunkResponse.ErrorMessage
                    };
                }

                _logger.LogInformation("Получен фрагмент {ChunkIndex} для файла {FileName}",
                    chunkIndex, fileInfo.FileName);

                // Проверяем, что данные не null
                if (chunkResponse.ChunkData == null)
                {
                    throw new InvalidOperationException("Получены пустые данные фрагмента файла");
                }

                // Преобразуем массив байтов в base64 строку
                string base64ChunkData = Convert.ToBase64String(chunkResponse.ChunkData);

                return new DownloadFileChunkResult
                {
                    Success = true,
                    ChunkData = base64ChunkData,
                    ChunkIndex = chunkIndex,
                    IsLastChunk = end == fileInfo.FileSize,
                    FileName = fileInfo.FileName,
                    ContentType = fileInfo.ContentType,
                    TotalSize = fileInfo.FileSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении фрагмента файла");
                return new DownloadFileChunkResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        [Authorize]
        public async Task<GetFileDownloadUrlResult> GetFileDownloadUrl(string fileId, [Service] ICurrentUserService currentUserService)
        {
            try
            {
                var userId = currentUserService.GetCurrentUserId();

                // Сначала проверяем права доступа к файлу
                var fileInfo = await GetFileInfo(fileId);
                if (fileInfo.UserId != userId)
                {
                    return new GetFileDownloadUrlResult
                    {
                        Success = false,
                        ErrorMessage = "У вас нет доступа к этому файлу"
                    };
                }

                var request = new GetFileDownloadUrlRequest
                {
                    FileId = fileId
                };

                var response = await _client.GetFileDownloadUrlAsync(request);

                return new GetFileDownloadUrlResult
                {
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage,
                    Url = response.Url,
                    FileName = fileInfo.FileName,
                    ContentType = fileInfo.ContentType
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error getting file download URL: {Detail}", ex.Status.Detail);
                return new GetFileDownloadUrlResult
                {
                    Success = false,
                    ErrorMessage = ex.Status.Detail ?? "Ошибка при получении URL для скачивания файла"
                };
            }
        }

        // Добавляем метод определения ContentType по имени файла
        private string GetContentTypeFromFileName(string fileName)
        {
            // Извлекаем расширение файла
            string extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

            // Определяем MIME-тип по расширению
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".mp3" => "audio/mpeg",
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".zip" => "application/zip",
                ".rar" => "application/vnd.rar",
                ".7z" => "application/x-7z-compressed",
                ".tar" => "application/x-tar",
                ".gz" => "application/gzip",
                ".doc" or ".docx" => "application/msword",
                ".xls" or ".xlsx" => "application/vnd.ms-excel",
                ".ppt" or ".pptx" => "application/vnd.ms-powerpoint",
                ".pt" => "application/octet-stream", // для файлов PyTorch
                // Если расширение неизвестно, возвращаем общий тип
                _ => "application/octet-stream"
            };
        }
    }
}