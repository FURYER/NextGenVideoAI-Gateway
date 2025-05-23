using HotChocolate.Types;
using NextGenVideoAI.StorageService.Api;

namespace ApiGateway.GraphQL
{
    // Тип для метаданных файла
    public class FileMetadataType : ObjectType<FileMetadata>
    {
        protected override void Configure(IObjectTypeDescriptor<FileMetadata> descriptor)
        {
            descriptor
                .Field(f => f.FileName)
                .Name("fileName")
                .Type<NonNullType<StringType>>();

            descriptor
                .Field(f => f.ContentType)
                .Name("contentType")
                .Type<NonNullType<StringType>>();

            descriptor
                .Field(f => f.Size)
                .Name("size")
                .Type<NonNullType<LongType>>();
        }
    }

    public class FileMetadata
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    // Тип файла
    public class FileInfoType : ObjectType<FileModel>
    {
        protected override void Configure(IObjectTypeDescriptor<FileModel> descriptor)
        {
            descriptor.Description("Информация о файле");
            descriptor.Field(f => f.FileId).Type<NonNullType<StringType>>().Description("ID файла");
            descriptor.Field(f => f.FileName).Type<NonNullType<StringType>>().Description("Имя файла");
            descriptor.Field(f => f.ContentType).Type<NonNullType<StringType>>().Description("MIME-тип файла");
            descriptor.Field(f => f.FileSize).Type<NonNullType<LongType>>().Description("Размер файла в байтах");
            descriptor.Field(f => f.UserId).Type<NonNullType<StringType>>().Description("ID пользователя, владельца файла");
            descriptor.Field(f => f.ChatId).Type<StringType>().Description("ID чата, к которому относится файл (опционально)");
            descriptor.Field(f => f.CreatedAt).Type<NonNullType<StringType>>().Description("Дата создания файла");
            descriptor.Field(f => f.LastAccessedAt).Type<NonNullType<StringType>>().Description("Дата последнего доступа к файлу");
        }
    }

    // Модель файла
    public class FileModel
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? ChatId { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string LastAccessedAt { get; set; } = string.Empty;
    }

    // Тип для результата загрузки файла
    public class UploadFileResultType : ObjectType<UploadFileResult>
    {
        protected override void Configure(IObjectTypeDescriptor<UploadFileResult> descriptor)
        {
            descriptor.Description("Результат загрузки файла");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
            descriptor.Field(f => f.FileId).Type<StringType>().Description("ID загруженного файла");
            descriptor.Field(f => f.FileSize).Type<LongType>().Description("Размер файла в байтах");
        }
    }

    // Модель результата загрузки файла
    public class UploadFileResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FileId { get; set; }
        public long? FileSize { get; set; }
    }

    // Тип для результата удаления файла
    public class DeleteFileResultType : ObjectType<DeleteFileResult>
    {
        protected override void Configure(IObjectTypeDescriptor<DeleteFileResult> descriptor)
        {
            descriptor.Description("Результат удаления файла");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
        }
    }

    // Модель результата удаления файла
    public class DeleteFileResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // Тип для входных данных при загрузке файла
    public class UploadFileInputType : InputObjectType<UploadFileInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<UploadFileInput> descriptor)
        {
            descriptor.Description("Входные данные для загрузки файла");
            descriptor.Field(f => f.UserId).Type<NonNullType<StringType>>().Description("ID пользователя");
            descriptor.Field(f => f.ChatId).Type<StringType>().Description("ID чата (опционально)");
            descriptor.Field(f => f.FileName).Type<NonNullType<StringType>>().Description("Имя файла");
            descriptor.Field(f => f.ContentType).Type<NonNullType<StringType>>().Description("MIME-тип файла");
            descriptor.Field(f => f.FileData).Type<NonNullType<ByteArrayType>>().Description("Данные файла в base64");
        }
    }

    // Модель входных данных при загрузке файла
    public class UploadFileInput
    {
        public string UserId { get; set; } = string.Empty;
        public string? ChatId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] FileData { get; set; } = Array.Empty<byte>();
    }

    // Тип файла для скачивания
    public class DownloadFileResultType : ObjectType<DownloadFileResult>
    {
        protected override void Configure(IObjectTypeDescriptor<DownloadFileResult> descriptor)
        {
            descriptor.Description("Результат скачивания файла");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
            descriptor.Field(f => f.FileData).Type<ByteArrayType>().Description("Данные файла в base64");
            descriptor.Field(f => f.FileName).Type<StringType>().Description("Имя файла");
            descriptor.Field(f => f.ContentType).Type<StringType>().Description("MIME-тип файла");
            descriptor.Field(f => f.FileSize).Type<LongType>().Description("Размер файла в байтах");
        }
    }

    // Модель результата скачивания файла
    public class DownloadFileResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public byte[]? FileData { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? FileSize { get; set; }
    }

    // Тип для инициализации фрагментированной загрузки
    public class InitChunkedUploadInputType : InputObjectType<InitChunkedUploadInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<InitChunkedUploadInput> descriptor)
        {
            descriptor.Description("Входные данные для инициализации фрагментированной загрузки");
            descriptor.Field(f => f.UserId).Type<NonNullType<StringType>>().Description("ID пользователя");
            descriptor.Field(f => f.ChatId).Type<StringType>().Description("ID чата (опционально)");
            descriptor.Field(f => f.FileName).Type<NonNullType<StringType>>().Description("Имя файла");
            descriptor.Field(f => f.ContentType).Type<NonNullType<StringType>>().Description("MIME-тип файла");
            descriptor.Field(f => f.TotalSize).Type<NonNullType<LongType>>().Description("Общий размер файла в байтах");
            descriptor.Field(f => f.FileSize).Type<NonNullType<LongType>>().Description("Общий размер файла в байтах (алиас для TotalSize)");
            descriptor.Field(f => f.TotalChunks).Type<NonNullType<IntType>>().Description("Общее количество фрагментов");
        }
    }

    // Модель входных данных для инициализации фрагментированной загрузки
    public class InitChunkedUploadInput
    {
        public string UserId { get; set; } = string.Empty;
        public string? ChatId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long FileSize
        {
            get => TotalSize;
            set => TotalSize = value;
        }
        public int TotalChunks { get; set; }
    }

    // Тип для предподписанной ссылки с номером части
    public class PresignedUrl
    {
        public string Url { get; set; } = string.Empty;
        public int PartNumber { get; set; }
    }

    public class PresignedUrlType : ObjectType<PresignedUrl>
    {
        protected override void Configure(IObjectTypeDescriptor<PresignedUrl> descriptor)
        {
            descriptor.Description("Предподписанная ссылка для загрузки части файла");
            descriptor.Field(f => f.Url).Type<NonNullType<StringType>>().Description("URL для загрузки части файла");
            descriptor.Field(f => f.PartNumber).Type<NonNullType<IntType>>().Description("Номер части (начиная с 1)");
        }
    }

    // Модель результата инициализации фрагментированной загрузки
    public class InitChunkedUploadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? UploadId { get; set; }
        public List<PresignedUrl>? PresignedUrls { get; set; }
        public List<PresignedUrl>? PresignedUrlInfos { get; set; }
    }

    // Тип для результата инициализации фрагментированной загрузки
    public class InitChunkedUploadResultType : ObjectType<InitChunkedUploadResult>
    {
        protected override void Configure(IObjectTypeDescriptor<InitChunkedUploadResult> descriptor)
        {
            descriptor.Description("Результат инициализации фрагментированной загрузки");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
            descriptor.Field(f => f.UploadId).Type<StringType>().Description("ID загрузки для последующих операций");
            descriptor.Field(f => f.PresignedUrls).Type<ListType<NonNullType<PresignedUrlType>>>().Description("Предподписанные URL для прямой загрузки");
            descriptor.Field(f => f.PresignedUrlInfos).Type<ListType<NonNullType<PresignedUrlType>>>().Description("Предподписанные URL для прямой загрузки с номерами частей");
        }
    }

    // Модель входных данных для загрузки фрагмента файла
    public class ChunkUploadInput
    {
        public string UploadId { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string Etag { get; set; } = string.Empty;
    }

    // Тип для загрузки фрагмента файла
    public class ChunkUploadInputType : InputObjectType<ChunkUploadInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<ChunkUploadInput> descriptor)
        {
            descriptor.Description("Входные данные для загрузки фрагмента файла");
            descriptor.Field(f => f.UploadId).Type<NonNullType<StringType>>().Description("ID загрузки");
            descriptor.Field(f => f.ChunkIndex).Type<NonNullType<IntType>>().Description("Индекс фрагмента (начиная с 0)");
            descriptor.Field(f => f.Etag).Type<NonNullType<StringType>>().Description("ETag от загрузки части");
        }
    }

    // Модель результата загрузки фрагмента файла
    public class ChunkUploadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? ChunkIndex { get; set; }
        public string? UploadId { get; set; }
        public double? Progress { get; set; }
    }

    // Тип для результата загрузки фрагмента файла
    public class ChunkUploadResultType : ObjectType<ChunkUploadResult>
    {
        protected override void Configure(IObjectTypeDescriptor<ChunkUploadResult> descriptor)
        {
            descriptor.Description("Результат загрузки фрагмента файла");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
            descriptor.Field(f => f.ChunkIndex).Type<IntType>().Description("Индекс загруженного фрагмента");
            descriptor.Field(f => f.UploadId).Type<StringType>().Description("ID загрузки");
            descriptor.Field(f => f.Progress).Type<FloatType>().Description("Текущий прогресс загрузки в процентах");
        }
    }

    // Модель результата завершения фрагментированной загрузки
    public class CompleteChunkedUploadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FileId { get; set; }
        public long? FileSize { get; set; }
    }

    // Тип для результата завершения фрагментированной загрузки
    public class CompleteChunkedUploadResultType : ObjectType<CompleteChunkedUploadResult>
    {
        protected override void Configure(IObjectTypeDescriptor<CompleteChunkedUploadResult> descriptor)
        {
            descriptor.Description("Результат завершения фрагментированной загрузки");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
            descriptor.Field(f => f.FileId).Type<StringType>().Description("ID загруженного файла");
            descriptor.Field(f => f.FileSize).Type<LongType>().Description("Размер файла в байтах");
        }
    }

    // Модель результата отмены фрагментированной загрузки
    public class CancelChunkedUploadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // Тип для результата отмены фрагментированной загрузки
    public class CancelChunkedUploadResultType : ObjectType<CancelChunkedUploadResult>
    {
        protected override void Configure(IObjectTypeDescriptor<CancelChunkedUploadResult> descriptor)
        {
            descriptor.Description("Результат отмены фрагментированной загрузки");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
        }
    }

    // Тип для статуса загрузки
    public class UploadStatusType : ObjectType<UploadStatus>
    {
        protected override void Configure(IObjectTypeDescriptor<UploadStatus> descriptor)
        {
            descriptor.Description("Статус фрагментированной загрузки");
            descriptor.Field(f => f.UploadId).Type<NonNullType<StringType>>().Description("ID загрузки");
            descriptor.Field(f => f.FileName).Type<StringType>().Description("Имя файла");
            descriptor.Field(f => f.ContentType).Type<StringType>().Description("MIME-тип файла");
            descriptor.Field(f => f.TotalSize).Type<LongType>().Description("Общий размер файла в байтах");
            descriptor.Field(f => f.TotalChunks).Type<IntType>().Description("Общее количество фрагментов");
            descriptor.Field(f => f.UploadedChunks).Type<ListType<IntType>>().Description("Список загруженных фрагментов");
            descriptor.Field(f => f.IsComplete).Type<BooleanType>().Description("Завершена ли загрузка");
            descriptor.Field(f => f.FileId).Type<StringType>().Description("ID файла (после завершения загрузки)");
            descriptor.Field(f => f.CreatedAt).Type<StringType>().Description("Дата создания загрузки");
            descriptor.Field(f => f.LastUpdatedAt).Type<StringType>().Description("Дата последнего обновления");
            descriptor.Field(f => f.Status).Type<StringType>().Description("Статус загрузки (PENDING, IN_PROGRESS, COMPLETED, ERROR)");
            descriptor.Field(f => f.Error).Type<StringType>().Description("Сообщение об ошибке (если есть)");
        }
    }

    // Модель статуса загрузки
    public class UploadStatus
    {
        public string UploadId { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? TotalSize { get; set; }
        public int? TotalChunks { get; set; }
        public List<int> UploadedChunks { get; set; } = new List<int>();
        public bool IsComplete { get; set; }
        public string? FileId { get; set; }
        public string? CreatedAt { get; set; }
        public string? LastUpdatedAt { get; set; }
        public string? Status { get; set; }
        public string? Error { get; set; }
    }

    // Тип для прогресса загрузки (для подписки)
    public class FileUploadProgressType : ObjectType<FileUploadProgress>
    {
        protected override void Configure(IObjectTypeDescriptor<FileUploadProgress> descriptor)
        {
            descriptor.Description("Прогресс загрузки файла");
            descriptor.Field(f => f.UploadId).Type<NonNullType<StringType>>().Description("ID загрузки");
            descriptor.Field(f => f.Progress).Type<NonNullType<FloatType>>().Description("Прогресс загрузки в процентах");
            descriptor.Field(f => f.Status).Type<NonNullType<StringType>>().Description("Статус загрузки");
            descriptor.Field(f => f.Message).Type<StringType>().Description("Сообщение (если есть)");
            descriptor.Field(f => f.FileName).Type<StringType>().Description("Имя файла");
            descriptor.Field(f => f.FileSize).Type<LongType>().Description("Размер файла в байтах");
            descriptor.Field(f => f.ProcessedChunks).Type<IntType>().Description("Количество обработанных фрагментов");
            descriptor.Field(f => f.TotalChunks).Type<IntType>().Description("Общее количество фрагментов");
            descriptor.Field(f => f.CreatedAt).Type<StringType>().Description("Дата создания");
            descriptor.Field(f => f.LastUpdatedAt).Type<StringType>().Description("Дата последнего обновления");
        }
    }

    // Модель прогресса загрузки
    public class FileUploadProgress
    {
        public string UploadId { get; set; } = string.Empty;
        public double Progress { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public int? ProcessedChunks { get; set; }
        public int? TotalChunks { get; set; }
        public string? CreatedAt { get; set; }
        public string? LastUpdatedAt { get; set; }
    }

    // Тип для информации о квоте пользователя
    public class UserQuotaInfoType : ObjectType<UserQuotaInfo>
    {
        protected override void Configure(IObjectTypeDescriptor<UserQuotaInfo> descriptor)
        {
            descriptor.Description("Информация о квоте пользователя");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
            descriptor.Field(f => f.StorageQuota).Type<LongType>().Description("Общая квота пользователя в байтах");
            descriptor.Field(f => f.UsedStorage).Type<LongType>().Description("Использованное пространство в байтах");
        }
    }

    // Модель информации о квоте пользователя
    public class UserQuotaInfo
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public long? StorageQuota { get; set; }
        public long? UsedStorage { get; set; }
    }

    public class FileChunkResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public byte[]? ChunkData { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long TotalSize { get; set; }
    }

    public class FileChunkResponseType : ObjectType<FileChunkResponse>
    {
        protected override void Configure(IObjectTypeDescriptor<FileChunkResponse> descriptor)
        {
            descriptor.Field(f => f.Success)
                .Type<NonNullType<BooleanType>>()
                .Description("Успешность операции");

            descriptor.Field(f => f.ErrorMessage)
                .Type<StringType>()
                .Description("Сообщение об ошибке");

            descriptor.Field(f => f.ChunkData)
                .Type<ByteArrayType>()
                .Description("Данные части файла");

            descriptor.Field(f => f.FileName)
                .Type<StringType>()
                .Description("Имя файла");

            descriptor.Field(f => f.ContentType)
                .Type<StringType>()
                .Description("Тип содержимого");

            descriptor.Field(f => f.TotalSize)
                .Type<NonNullType<LongType>>()
                .Description("Общий размер файла");
        }
    }

    // Тип для инициализации фрагментированной скачки
    public class InitChunkedDownloadInputType : InputObjectType<InitChunkedDownloadInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<InitChunkedDownloadInput> descriptor)
        {
            descriptor.Description("Входные данные для инициализации фрагментированной скачки");
            descriptor.Field(f => f.FileId).Type<NonNullType<StringType>>().Description("ID файла");
        }
    }

    public class InitChunkedDownloadInput
    {
        public string FileId { get; set; } = string.Empty;
    }

    public class InitChunkedDownloadResultType : ObjectType<InitChunkedDownloadResult>
    {
        protected override void Configure(IObjectTypeDescriptor<InitChunkedDownloadResult> descriptor)
        {
            descriptor.Description("Результат инициализации фрагментированной скачки");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>();
            descriptor.Field(f => f.ErrorMessage).Type<StringType>();
            descriptor.Field(f => f.DownloadId).Type<StringType>();
            descriptor.Field(f => f.FileName).Type<StringType>();
            descriptor.Field(f => f.ContentType).Type<StringType>();
            descriptor.Field(f => f.TotalSize).Type<LongType>();
            descriptor.Field(f => f.TotalChunks).Type<IntType>();
        }
    }

    public class InitChunkedDownloadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DownloadId { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? TotalSize { get; set; }
        public int? TotalChunks { get; set; }
    }

    public class GetChunkResultType : ObjectType<GetChunkResult>
    {
        protected override void Configure(IObjectTypeDescriptor<GetChunkResult> descriptor)
        {
            descriptor.Description("Результат получения фрагмента файла");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>();
            descriptor.Field(f => f.ErrorMessage).Type<StringType>();
            descriptor.Field(f => f.ChunkData).Type<ByteArrayType>();
            descriptor.Field(f => f.ChunkIndex).Type<IntType>();
            descriptor.Field(f => f.IsLastChunk).Type<BooleanType>();
        }
    }

    public class GetChunkResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public byte[]? ChunkData { get; set; }
        public int? ChunkIndex { get; set; }
        public bool IsLastChunk { get; set; }
    }

    public class DownloadFileChunkResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ChunkData { get; set; } // base64 строка
        public int ChunkIndex { get; set; }
        public bool IsLastChunk { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? TotalSize { get; set; }
    }

    public class DownloadFileChunkResultType : ObjectType<DownloadFileChunkResult>
    {
        protected override void Configure(IObjectTypeDescriptor<DownloadFileChunkResult> descriptor)
        {
            descriptor.Description("Результат получения фрагмента файла");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
            descriptor.Field(f => f.ChunkData).Type<StringType>().Description("Данные фрагмента в формате base64");
            descriptor.Field(f => f.ChunkIndex).Type<NonNullType<IntType>>().Description("Индекс фрагмента");
            descriptor.Field(f => f.IsLastChunk).Type<NonNullType<BooleanType>>().Description("Флаг последнего фрагмента");
            descriptor.Field(f => f.FileName).Type<StringType>().Description("Имя файла");
            descriptor.Field(f => f.ContentType).Type<StringType>().Description("MIME-тип файла");
            descriptor.Field(f => f.TotalSize).Type<LongType>().Description("Общий размер файла в байтах");
        }
    }

    // Тип для результата получения URL для скачивания файла
    public class GetFileDownloadUrlResultType : ObjectType<GetFileDownloadUrlResult>
    {
        protected override void Configure(IObjectTypeDescriptor<GetFileDownloadUrlResult> descriptor)
        {
            descriptor.Description("Результат получения URL для скачивания файла");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Флаг успешности операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке (если есть)");
            descriptor.Field(f => f.Url).Type<StringType>().Description("URL для скачивания файла");
            descriptor.Field(f => f.FileName).Type<StringType>().Description("Имя файла");
            descriptor.Field(f => f.ContentType).Type<StringType>().Description("MIME-тип файла");
        }
    }

    // Модель результата получения URL для скачивания файла
    public class GetFileDownloadUrlResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Url { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
    }
}