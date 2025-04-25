using ApiGateway.Models;
using HotChocolate.Types;

namespace ApiGateway.GraphQL
{
    public class NotificationType : ObjectType<NotificationMessage>
    {
        protected override void Configure(IObjectTypeDescriptor<NotificationMessage> descriptor)
        {
            descriptor.Name("Notification");

            descriptor.Field(f => f.Type)
                .Description("Тип уведомления");

            descriptor.Field(f => f.Message)
                .Description("Текст уведомления");

            descriptor.Field(f => f.Timestamp)
                .Description("Временная метка создания уведомления");

            descriptor.Field(f => f.Data)
                .Type<AnyType>()
                .Description("Дополнительные данные уведомления")
                .Resolve(context =>
                {
                    var data = context.Parent<NotificationMessage>().Data;
                    return data != null ? data : null;
                });
        }
    }
}