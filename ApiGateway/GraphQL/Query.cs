using HotChocolate.Authorization; // Добавляем пространство имён
using HotChocolate.Types;
using System.Threading.Tasks;

namespace ApiGateway.GraphQL
{
    [GraphQLDescription("Корневой тип запросов")]
    public class Query : ObjectType
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Field("getChats")
                .Argument("userId", a => a.Type<NonNullType<StringType>>())
                .Argument("skip", a => a.Type<IntType>())
                .Argument("limit", a => a.Type<IntType>())
                .Type<ListType<NonNullType<ChatType>>>()
                .ResolveWith<ChatResolver>(r => r.GetChats(default!, default, default))
                .Authorize();

            descriptor.Field("getChatById")
                .Argument("chatId", a => a.Type<NonNullType<StringType>>())
                .Type<ChatType>()
                .ResolveWith<ChatResolver>(r => r.GetChatById(default!))
                .Authorize();

            descriptor.Field("getChatMessages")
                .Argument("chatId", a => a.Type<NonNullType<StringType>>())
                .Argument("skip", a => a.Type<IntType>())
                .Argument("limit", a => a.Type<IntType>())
                .Type<ListType<NonNullType<ChatMessageType>>>()
                .ResolveWith<ChatResolver>(r => r.GetChatMessages(default!, default, default))
                .Authorize();

            descriptor.Field("getMessage")
                .Argument("chatId", a => a.Type<NonNullType<StringType>>())
                .Argument("content", a => a.Type<NonNullType<StringType>>())
                .Type<NonNullType<ChatMessageType>>()
                .ResolveWith<ChatResolver>(r => r.GetMessage(default!, default!))
                .Authorize();

            descriptor.Field("streamChat")
                .Argument("input", a => a.Type<NonNullType<StreamChatInputType>>())
                .Type<NonNullType<ListType<NonNullType<ChatStreamResponseType>>>>()
                .ResolveWith<ChatResolver>(r => r.StreamChat(default!))
                .Authorize();

            // Добавляем запросы для работы с файловым хранилищем
            descriptor.Field("getFileInfo")
                .Argument("fileId", a => a.Type<NonNullType<StringType>>())
                .Type<FileInfoType>()
                .ResolveWith<StorageResolver>(r => r.GetFileInfo(default!))
                .Description("Получение информации о файле")
                .Authorize();

            descriptor.Field("getFileMetadata")
                .Argument("fileId", a => a.Type<NonNullType<StringType>>())
                .Type<FileMetadataType>()
                .ResolveWith<StorageResolver>(r => r.GetFileMetadata(default!, default!))
                .Description("Получение метаданных файла")
                .Authorize();

            descriptor.Field("downloadFile")
                .Argument("fileId", a => a.Type<NonNullType<StringType>>())
                .Type<DownloadFileResultType>()
                .ResolveWith<StorageResolver>(r => r.DownloadFile(default!))
                .Description("Скачивание файла")
                .Authorize();

            descriptor.Field("getUserFiles")
                .Argument("userId", a => a.Type<NonNullType<StringType>>())
                .Argument("chatId", a => a.Type<StringType>())
                .Argument("skip", a => a.Type<IntType>())
                .Argument("limit", a => a.Type<IntType>())
                .Type<ListType<NonNullType<FileInfoType>>>()
                .ResolveWith<StorageResolver>(r => r.GetUserFiles(default!, default, default, default))
                .Description("Получение списка файлов пользователя")
                .Authorize();

            // Добавляем запрос для получения профиля пользователя
            descriptor.Field("getUserProfile")
                .Argument("userId", a => a.Type<NonNullType<StringType>>())
                .Type<UserProfileResponseType>()
                .ResolveWith<UserResolver>(r => r.GetUserProfile(default!))
                .Description("Получение профиля пользователя")
                .Authorize();

            // Добавляем запрос для получения статуса фрагментированной загрузки
            descriptor.Field("getUploadStatus")
                .Argument("uploadId", a => a.Type<NonNullType<StringType>>())
                .Type<UploadStatusType>()
                .ResolveWith<StorageResolver>(r => r.GetUploadStatus(default!))
                .Description("Получение статуса фрагментированной загрузки")
                .Authorize();

            // Добавляем запрос для получения информации о квоте пользователя
            descriptor.Field("getUserQuotaInfo")
                .Argument("userId", a => a.Type<NonNullType<StringType>>())
                .Type<UserQuotaInfoType>()
                .ResolveWith<StorageResolver>(r => r.GetUserQuotaInfo(default!))
                .Description("Получение информации о квоте пользователя")
                .Authorize();

            descriptor.Field("getFileChunk")
                .Argument("fileId", a => a.Type<NonNullType<StringType>>())
                .Argument("start", a => a.Type<IntType>())
                .Argument("end", a => a.Type<IntType>())
                .Type<FileChunkResponseType>()
                .ResolveWith<StorageResolver>(r => r.GetFileChunk(default!, default, default))
                .Description("Получение части файла")
                .Authorize();

            descriptor.Field("initChunkedDownload")
                .Argument("fileId", a => a.Type<NonNullType<StringType>>())
                .Type<InitChunkedDownloadResultType>()
                .ResolveWith<StorageResolver>(r => r.InitChunkedDownload(default!))
                .Description("Инициализация фрагментированной скачки")
                .Authorize();

            descriptor.Field("getChunk")
                .Argument("downloadId", a => a.Type<NonNullType<StringType>>())
                .Argument("chunkIndex", a => a.Type<NonNullType<IntType>>())
                .Type<GetChunkResultType>()
                .ResolveWith<StorageResolver>(r => r.GetChunk(default!, default))
                .Description("Получение фрагмента файла")
                .Authorize();

            descriptor.Field("downloadFileChunk")
                .Argument("fileId", a => a.Type<NonNullType<StringType>>())
                .Argument("chunkIndex", a => a.Type<NonNullType<IntType>>())
                .Type<DownloadFileChunkResultType>()
                .ResolveWith<StorageResolver>(r => r.DownloadFileChunk(default!, default))
                .Description("Получение фрагмента файла")
                .Authorize();

            descriptor.Field("getFileDownloadUrl")
                .Argument("fileId", a => a.Type<NonNullType<StringType>>())
                .Type<GetFileDownloadUrlResultType>()
                .ResolveWith<StorageResolver>(r => r.GetFileDownloadUrl(default!, default!))
                .Description("Получение URL для скачивания файла")
                .Authorize();
        }
    }
}