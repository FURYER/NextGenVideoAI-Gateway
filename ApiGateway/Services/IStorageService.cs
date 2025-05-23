using ApiGateway.GraphQL;

namespace ApiGateway.Services
{
    public interface IStorageService
    {
        Task<FileMetadata> GetFileMetadata(string fileId, string userId);
        Task<Stream> DownloadFile(string fileId, string userId);
    }
}