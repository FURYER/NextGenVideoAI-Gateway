using HotChocolate.Types;

namespace ApiGateway.GraphQL
{
    public class ChatMessageType : ObjectType<ChatMessage>
    {
        protected override void Configure(IObjectTypeDescriptor<ChatMessage> descriptor)
        {
            descriptor.Description("Сообщение в чате");
            descriptor.Field(f => f.Id).Type<NonNullType<StringType>>().Description("ID сообщения");
            descriptor.Field(f => f.Content).Type<NonNullType<StringType>>().Description("Содержимое сообщения");
            descriptor.Field(f => f.CreatedAt).Type<NonNullType<DateTimeType>>().Description("Время создания");
            descriptor.Field(f => f.SenderType).Type<NonNullType<EnumType<SenderType>>>().Description("Тип отправителя");
            descriptor.Field(f => f.SenderId).Type<NonNullType<StringType>>().Description("ID отправителя");
            descriptor.Field(f => f.ChatId).Type<NonNullType<StringType>>().Description("ID чата");
            descriptor.Field(f => f.RequiresEditing).Type<NonNullType<BooleanType>>().Description("Требует ли сообщение монтажа");
            descriptor.Field(f => f.IsEditingStage).Type<NonNullType<BooleanType>>().Description("Находится ли сообщение на этапе монтажа");
            descriptor.Field(f => f.CommandId).Type<StringType>().Description("ID команды");
            descriptor.Field(f => f.IsCompleted).Type<NonNullType<BooleanType>>().Description("Завершено ли выполнение");
            descriptor.Field(f => f.Canceled).Type<NonNullType<BooleanType>>().Description("Сообщение отменено");
            descriptor.Field(f => f.Attachments).Type<ListType<NonNullType<AttachmentType>>>().Description("Прикрепленные файлы");
        }
    }

    public class ChatMessage
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public SenderType SenderType { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string ChatId { get; set; } = string.Empty;
        public bool RequiresEditing { get; set; }
        public bool IsEditingStage { get; set; }
        public string? CommandId { get; set; }
        public bool IsCompleted { get; set; }
        public bool Canceled { get; set; }
        public List<Attachment> Attachments { get; set; } = new();
    }

    public enum SenderType
    {
        User,
        AI,
        AI2
    }

    public class SenderTypeType : EnumType<SenderType>
    {
        protected override void Configure(IEnumTypeDescriptor<SenderType> descriptor)
        {
            descriptor.Description("Тип отправителя сообщения");
            descriptor.Value(SenderType.User).Description("Пользователь");
            descriptor.Value(SenderType.AI).Description("Искусственный интеллект");
            descriptor.Value(SenderType.AI2).Description("Второй ИИ");
        }
    }

    public class ChatType : ObjectType<Chat>
    {
        protected override void Configure(IObjectTypeDescriptor<Chat> descriptor)
        {
            descriptor.Description("Чат");
            descriptor.Field(f => f.Id).Type<NonNullType<StringType>>().Description("ID чата");
            descriptor.Field(f => f.Title).Type<NonNullType<StringType>>().Description("Название чата");
            descriptor.Field(f => f.LastUpdate).Type<NonNullType<DateTimeType>>().Description("Время последнего обновления");
            descriptor.Field(f => f.IsBusy).Type<NonNullType<BooleanType>>().Description("Занят ли чат обработкой");
        }
    }

    public class Chat
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
        public bool IsBusy { get; set; }
    }

    public class AttachmentType : ObjectType<Attachment>
    {
        protected override void Configure(IObjectTypeDescriptor<Attachment> descriptor)
        {
            descriptor.Description("Прикрепленный файл");
            descriptor.Field(f => f.FileId).Type<NonNullType<StringType>>().Description("ID файла");
            descriptor.Field(f => f.FileName).Type<NonNullType<StringType>>().Description("Имя файла");
            descriptor.Field(f => f.ContentType).Type<NonNullType<StringType>>().Description("Тип содержимого файла");
        }
    }

    public class Attachment
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }

    public class ChatStreamResponseType : ObjectType<ChatStreamResponse>
    {
        protected override void Configure(IObjectTypeDescriptor<ChatStreamResponse> descriptor)
        {
            descriptor.Description("Ответ стриминга чата");
            descriptor.Field(f => f.Content).Type<NonNullType<StringType>>().Description("Содержимое ответа");
            descriptor.Field(f => f.IsFinal).Type<NonNullType<BooleanType>>().Description("Флаг завершения стриминга");
            descriptor.Field(f => f.Error).Type<StringType>().Description("Сообщение об ошибке");
        }
    }

    public class ChatStreamResponse
    {
        public string Content { get; set; } = string.Empty;
        public bool IsFinal { get; set; }
        public string? Error { get; set; }
    }

    public class StreamChatInput
    {
        public string ChatId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public List<Attachment>? Attachments { get; set; }
    }

    public class StreamChatInputType : InputObjectType<StreamChatInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<StreamChatInput> descriptor)
        {
            descriptor.Description("Входные данные для стриминга чата");
            descriptor.Field(f => f.ChatId).Type<NonNullType<StringType>>().Description("ID чата");
            descriptor.Field(f => f.Content).Type<NonNullType<StringType>>().Description("Содержимое сообщения");
            descriptor.Field(f => f.UserId).Type<NonNullType<StringType>>().Description("ID пользователя");
            descriptor.Field(f => f.Attachments).Type<ListType<AttachmentInputType>>().Description("Прикрепленные файлы");
        }
    }

    public class AttachmentInputType : InputObjectType<Attachment>
    {
        protected override void Configure(IInputObjectTypeDescriptor<Attachment> descriptor)
        {
            descriptor.Description("Входные данные для прикрепленного файла");
            descriptor.Field(f => f.FileId).Type<NonNullType<StringType>>().Description("ID файла");
            descriptor.Field(f => f.FileName).Type<NonNullType<StringType>>().Description("Имя файла");
            descriptor.Field(f => f.ContentType).Type<NonNullType<StringType>>().Description("Тип содержимого файла");
        }
    }

    public class UpdateChatInput
    {
        public string ChatId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? ChatContext { get; set; }
        public bool? IsBusy { get; set; }
    }

    public class UpdateChatInputType : InputObjectType<UpdateChatInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<UpdateChatInput> descriptor)
        {
            descriptor.Description("Входные данные для обновления чата (контекста, названия и статуса занятости)");
            descriptor.Field(f => f.ChatId).Type<NonNullType<StringType>>().Description("ID чата");
            descriptor.Field(f => f.Name).Type<StringType>().Description("Новое имя чата (опционально)");
            descriptor.Field(f => f.ChatContext).Type<StringType>().Description("Новый контекст чата (опционально)");
            descriptor.Field(f => f.IsBusy).Type<BooleanType>().Description("Установить статус занятости (опционально)");
        }
    }

    public class UpdateChatPayload
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Chat? Chat { get; set; }
    }

    public class UpdateChatPayloadType : ObjectType<UpdateChatPayload>
    {
        protected override void Configure(IObjectTypeDescriptor<UpdateChatPayload> descriptor)
        {
            descriptor.Description("Результат обновления чата");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Успешность операции");
            descriptor.Field(f => f.ErrorMessage).Type<StringType>().Description("Сообщение об ошибке");
            descriptor.Field(f => f.Chat).Type<ChatType>().Description("Обновлённый чат");
        }
    }
}