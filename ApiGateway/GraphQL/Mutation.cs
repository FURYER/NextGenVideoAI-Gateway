// ApiGateway/GraphQL/Mutation.cs
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Authorization;
using HotChocolate.Types;

namespace ApiGateway.GraphQL
{
    public class Mutation
    {
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AllowAnonymous")]
        public async Task<AuthResponse> RegisterUser([Service] UserResolver resolver, RegisterUserInput input)
        {
            return await resolver.RegisterUser(input);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AllowAnonymous")]
        public async Task<AuthResponse> Login([Service] UserResolver resolver, LoginInput input)
        {
            return await resolver.Login(input);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<double> UpdateCredits([Service] UserResolver resolver, UpdateCreditsInput input)
        {
            return await resolver.UpdateCredits(input);
        }
    }
}