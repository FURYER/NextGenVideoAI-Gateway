using System.Text.Json;
using StackExchange.Redis;

namespace ApiGateway.Services
{
    public interface IUploadStateService
    {
        Task<UploadState?> GetUploadState(string uploadId);
        Task SaveUploadState(string uploadId, UploadState state);
        Task DeleteUploadState(string uploadId);
        Task<bool> HasUploadState(string uploadId);
    }

    public class UploadState
    {
        public string UploadId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? ChatId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public int TotalChunks { get; set; }
        public List<int> UploadedChunks { get; set; } = new List<int>();
        public bool IsComplete { get; set; }
        public string? FileId { get; set; }
        public string Status { get; set; } = "PENDING";
        public string? Error { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        public List<ApiGateway.GraphQL.ChunkEtagInfo>? ChunkEtags { get; set; }
    }

    // Временная реализация с использованием памяти (для тестирования)
    // В боевой среде лучше использовать Redis или другое распределенное хранилище
    public class InMemoryUploadStateService : IUploadStateService
    {
        private readonly Dictionary<string, UploadState> _states = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger<InMemoryUploadStateService> _logger;

        public InMemoryUploadStateService(ILogger<InMemoryUploadStateService> logger)
        {
            _logger = logger;
        }

        public async Task<UploadState?> GetUploadState(string uploadId)
        {
            await _lock.WaitAsync();
            try
            {
                if (_states.TryGetValue(uploadId, out var state))
                {
                    return state;
                }
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveUploadState(string uploadId, UploadState state)
        {
            await _lock.WaitAsync();
            try
            {
                state.LastUpdatedAt = DateTime.UtcNow;
                _states[uploadId] = state;
                _logger.LogDebug("Saved upload state for uploadId {UploadId}", uploadId);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task DeleteUploadState(string uploadId)
        {
            await _lock.WaitAsync();
            try
            {
                if (_states.Remove(uploadId))
                {
                    _logger.LogDebug("Deleted upload state for uploadId {UploadId}", uploadId);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> HasUploadState(string uploadId)
        {
            await _lock.WaitAsync();
            try
            {
                return _states.ContainsKey(uploadId);
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    // Реализация с использованием Redis
    public class RedisUploadStateService : IUploadStateService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisUploadStateService> _logger;
        private const string KeyPrefix = "upload:";

        public RedisUploadStateService(IConnectionMultiplexer redis, ILogger<RedisUploadStateService> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task<UploadState?> GetUploadState(string uploadId)
        {
            var db = _redis.GetDatabase();
            var key = KeyPrefix + uploadId;
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<UploadState>(value!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing upload state for uploadId {UploadId}", uploadId);
                return null;
            }
        }

        public async Task SaveUploadState(string uploadId, UploadState state)
        {
            var db = _redis.GetDatabase();
            var key = KeyPrefix + uploadId;
            state.LastUpdatedAt = DateTime.UtcNow;

            var value = JsonSerializer.Serialize(state);
            await db.StringSetAsync(key, value);

            // Устанавливаем время жизни записи (например, 24 часа)
            await db.KeyExpireAsync(key, TimeSpan.FromHours(24));

            _logger.LogDebug("Saved upload state for uploadId {UploadId}", uploadId);
        }

        public async Task DeleteUploadState(string uploadId)
        {
            var db = _redis.GetDatabase();
            var key = KeyPrefix + uploadId;

            if (await db.KeyDeleteAsync(key))
            {
                _logger.LogDebug("Deleted upload state for uploadId {UploadId}", uploadId);
            }
        }

        public async Task<bool> HasUploadState(string uploadId)
        {
            var db = _redis.GetDatabase();
            var key = KeyPrefix + uploadId;

            return await db.KeyExistsAsync(key);
        }
    }
}