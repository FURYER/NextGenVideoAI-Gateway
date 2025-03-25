using HotChocolate.Authorization; // Добавляем пространство имён
using HotChocolate.Types;

namespace ApiGateway.GraphQL
{
    [GraphQLDescription("Корневой тип запросов")]
    public class Query
    {
        [Authorize] // Требуем авторизацию для этого запроса
        [GraphQLDescription("Получить пользователя по ID")]
        public async Task<User> GetUser([Service] UserResolver resolver, string id)
        {
            return await resolver.GetUserById(id);
        }

        [GraphQLDescription("Получить версию API")]
        public string Version() => "1.0.0";
    }
}