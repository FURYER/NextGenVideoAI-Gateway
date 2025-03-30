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
    }

    public enum SenderType
    {
        User,
        AI
    }

    public class SenderTypeType : EnumType<SenderType>
    {
        protected override void Configure(IEnumTypeDescriptor<SenderType> descriptor)
        {
            descriptor.Description("Тип отправителя сообщения");
            descriptor.Value(SenderType.User).Description("Пользователь");
            descriptor.Value(SenderType.AI).Description("Искусственный интеллект");
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
            descriptor.Field(f => f.Participants).Type<NonNullType<ListType<NonNullType<ParticipantType>>>>().Description("Участники чата");
        }
    }

    public class Chat
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
        public List<Participant> Participants { get; set; } = new();
    }

    public class ParticipantType : ObjectType<Participant>
    {
        protected override void Configure(IObjectTypeDescriptor<Participant> descriptor)
        {
            descriptor.Description("Участник чата");
            descriptor.Field(f => f.Id).Type<NonNullType<StringType>>().Description("ID участника");
            descriptor.Field(f => f.Name).Type<NonNullType<StringType>>().Description("Имя участника");
        }
    }

    public class Participant
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
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
    }

    public class StreamChatInputType : InputObjectType<StreamChatInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<StreamChatInput> descriptor)
        {
            descriptor.Description("Входные данные для стриминга чата");
            descriptor.Field(f => f.ChatId).Type<NonNullType<StringType>>().Description("ID чата");
            descriptor.Field(f => f.Content).Type<NonNullType<StringType>>().Description("Содержимое сообщения");
            descriptor.Field(f => f.UserId).Type<NonNullType<StringType>>().Description("ID пользователя");
        }
    }
}