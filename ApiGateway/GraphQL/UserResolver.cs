// ApiGateway/GraphQL/UserResolver.cs
using Grpc.Net.Client;
using HotChocolate;
using UserService.Protos;
using static UserService.Protos.UserService; // Добавляем using static

namespace ApiGateway.GraphQL
{
    public class UserResolver
    {
        private readonly UserServiceClient _client;

        public UserResolver()
        {
            var channel = GrpcChannel.ForAddress("https://localhost:5009");
            _client = new UserServiceClient(channel);
        }

        public async Task<string> RegisterUser([Argument] RegisterUserInput input)
        {
            var response = await _client.RegisterUserAsync(new RegisterUserRequest
            {
                Username = input.Username,
                Email = input.Email,
                Password = input.Password
            });
            return response.UserId;
        }

        public async Task<User> GetUserById([Argument] string userId)
        {
            // Валидация userId
            if (!Guid.TryParse(userId, out _))
            {
                throw new ArgumentException("Invalid user_id format. Must be a valid UUID.");
            }

            var response = await _client.GetUserByIdAsync(new GetUserByIdRequest { UserId = userId });
            return new User
            {
                Id = response.UserId,
                Username = response.Username,
                Email = response.Email,
                Credits = response.Credits
            };
        }

        public async Task<double> UpdateCredits([Argument] UpdateCreditsInput input)
        {
            // Валидация userId
            if (!Guid.TryParse(input.UserId, out _))
            {
                throw new ArgumentException("Invalid user_id format. Must be a valid UUID.");
            }

            var response = await _client.UpdateCreditsAsync(new UpdateCreditsRequest
            {
                UserId = input.UserId,
                Amount = (float)input.Amount
            });
            return response.NewBalance;
        }
    }
}