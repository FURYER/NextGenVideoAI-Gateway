using HotChocolate.Types;
using HotChocolate;
using HotChocolate.Execution;
using System.Reactive.Linq;
using System.Linq;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;

namespace ApiGateway.GraphQL
{
    public class Subscription : ObjectType
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Field("streamChat")
                .Argument("input", a => a.Type<NonNullType<StreamChatInputType>>())
                .Type<NonNullType<ChatStreamResponseType>>()
                .Resolve(context => context.GetEventMessage<ChatStreamResponse>())
                .Subscribe(context =>
                {
                    var input = context.ArgumentValue<StreamChatInput>("input");
                    var resolver = context.Service<ChatResolver>();
                    return resolver.SubscribeToChat(input);
                })
                .Authorize(policy: "AllowAnonymous");

            // Подписка на обновления прогресса загрузки файла
            descriptor.Field("fileUploadProgress")
                .Argument("uploadId", a => a.Type<NonNullType<StringType>>())
                .Type<NonNullType<FileUploadProgressType>>()
                .Resolve(context => context.GetEventMessage<FileUploadProgress>())
                .Subscribe(async context =>
                {
                    var uploadId = context.ArgumentValue<string>("uploadId");
                    var receiver = context.Service<ITopicEventReceiver>();
                    return await receiver.SubscribeAsync<FileUploadProgress>($"fileUpload:{uploadId}");
                })
                .Authorize(policy: "AllowAnonymous");
        }
    }
}