using HotChocolate.Types;

namespace ApiGateway.GraphQL
{
    public class MutationType : ObjectType
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name("Mutation");
            descriptor.Description("Корневой тип для всех мутаций");
        }
    }
}