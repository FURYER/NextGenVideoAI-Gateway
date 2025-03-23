// ApiGateway/GraphQL/Mutation.cs
namespace ApiGateway.GraphQL
{
    public class Mutation
    {
        public async Task<string> RegisterUser([Service] UserResolver resolver, RegisterUserInput input)
        {
            return await resolver.RegisterUser(input);
        }

        public async Task<double> UpdateCredits([Service] UserResolver resolver, UpdateCreditsInput input)
        {
            return await resolver.UpdateCredits(input);
        }
    }
}