using HotChocolate.Types;
using HotChocolate;
using HotChocolate.Execution;
using System.Reactive.Linq;
using System.Linq;
using HotChocolate.Authorization;

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
        }
    }
}