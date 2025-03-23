// ApiGateway/GraphQL/UserTypes.cs
namespace ApiGateway.GraphQL
{
    public class RegisterUserInput
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    public class UpdateCreditsInput
    {
        public string? UserId { get; set; } // Возвращаем string вместо Guid
        public double Amount { get; set; }
    }

    public class User
    {
        public string? Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public double Credits { get; set; }
    }
}