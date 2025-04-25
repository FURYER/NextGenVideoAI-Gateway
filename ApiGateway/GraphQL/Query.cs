using HotChocolate.Authorization; // Добавляем пространство имён
using HotChocolate.Types;

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
        }
    }
}