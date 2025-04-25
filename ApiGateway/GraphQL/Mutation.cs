// ApiGateway/GraphQL/Mutation.cs
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Authorization;
using HotChocolate.Types;

namespace ApiGateway.GraphQL
{
    public class Mutation
    {
        [HotChocolate.Authorization.Authorize(Policy = "AllowAnonymous")]
        public async Task<AuthResponse> RegisterUser([Service] UserResolver resolver, RegisterUserInput input)
        {
            return await resolver.RegisterUser(input);
        }

        [HotChocolate.Authorization.Authorize(Policy = "AllowAnonymous")]
        public async Task<AuthResponse> Login([Service] UserResolver resolver, LoginInput input)
        {
            return await resolver.Login(input);
        }

        [HotChocolate.Authorization.Authorize(Policy = "AllowAnonymous")]
        public async Task<AuthResponse> RefreshToken([Service] UserResolver resolver, string refreshToken)
        {
            return await resolver.RefreshToken(refreshToken);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<Chat> CreateChat([Service] ChatResolver resolver, string userId)
        {
            return await resolver.CreateChat(userId);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<ChatMessage> SendMessage([Service] ChatResolver resolver, string chatId, string content, string userId)
        {
            return await resolver.SendMessage(chatId, content, userId);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<bool> Logout([Service] UserResolver resolver, [GlobalState] string? accessToken)
        {
            return await resolver.Logout(accessToken);
        }

        [HotChocolate.Authorization.Authorize]
        public async IAsyncEnumerable<ChatStreamResponse> StreamChat([Service] ChatResolver resolver, StreamChatInput input)
        {
            await foreach (var response in resolver.StreamChat(input))
            {
                yield return response;
            }
        }
    }
}