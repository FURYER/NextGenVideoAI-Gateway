// ApiGateway/GraphQL/Query.cs
namespace ApiGateway.GraphQL
{
    public class Query
    {
        public async Task<User> GetUser([Service] UserResolver resolver, string id)
        {
            return await resolver.GetUserById(id);
        }
    }
}