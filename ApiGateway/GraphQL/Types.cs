using HotChocolate.Types;

namespace ApiGateway.GraphQL
{
    // User types
    public class UserType : ObjectType<User>
    {
        protected override void Configure(IObjectTypeDescriptor<User> descriptor)
        {
            descriptor.Description("Пользователь системы");
            descriptor.Field(f => f.Id).Type<NonNullType<StringType>>().Description("ID пользователя");
            descriptor.Field(f => f.Username).Type<NonNullType<StringType>>().Description("Имя пользователя");
            descriptor.Field(f => f.Email).Type<NonNullType<StringType>>().Description("Email пользователя");
            descriptor.Field(f => f.Credits).Type<NonNullType<FloatType>>().Description("Количество кредитов");
        }
    }

    public class User
    {
        public string? Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public double Credits { get; set; }
    }

    // Registration types
    public class RegisterUserInputType : InputObjectType<RegisterUserInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<RegisterUserInput> descriptor)
        {
            descriptor.Description("Входные данные для регистрации пользователя");
            descriptor.Field(f => f.Username).Type<NonNullType<StringType>>().Description("Имя пользователя");
            descriptor.Field(f => f.Email).Type<NonNullType<StringType>>().Description("Email пользователя");
            descriptor.Field(f => f.Password).Type<NonNullType<StringType>>().Description("Пароль пользователя");
        }
    }

    public class RegisterUserInput
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    // Login types
    public class LoginInputType : InputObjectType<LoginInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<LoginInput> descriptor)
        {
            descriptor.Description("Входные данные для входа в систему");
            descriptor.Field(f => f.Email).Type<NonNullType<StringType>>().Description("Email пользователя");
            descriptor.Field(f => f.Password).Type<NonNullType<StringType>>().Description("Пароль пользователя");
            descriptor.Field(f => f.RememberMe).Type<BooleanType>().Description("Запомнить пользователя");
            descriptor.Field(f => f.TwoFactorCode).Type<StringType>().Description("Код двухфакторной аутентификации");
            descriptor.Field(f => f.TwoFactorSecret).Type<StringType>().Description("Секретный ключ для 2FA");
        }
    }

    public class LoginInput
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? TwoFactorCode { get; set; }
        public string? TwoFactorSecret { get; set; }
        public bool RememberMe { get; set; }
    }

    // Authentication types
    public class AuthResponseType : ObjectType<AuthResponse>
    {
        protected override void Configure(IObjectTypeDescriptor<AuthResponse> descriptor)
        {
            descriptor.Description("Ответ на операции аутентификации");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Успешность операции");
            descriptor.Field(f => f.Message).Type<StringType>().Description("Сообщение об операции");
            descriptor.Field(f => f.AccessToken).Type<StringType>().Description("JWT токен доступа");
            descriptor.Field(f => f.RefreshToken).Type<StringType>().Description("JWT токен обновления");
            descriptor.Field(f => f.UserId).Type<StringType>().Description("ID пользователя");
            descriptor.Field(f => f.TwoFactorSecret).Type<StringType>().Description("Секретный код для 2FA");
            descriptor.Field(f => f.Username).Type<StringType>().Description("Имя пользователя");
        }
    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? UserId { get; set; }
        public string? TwoFactorSecret { get; set; }
        public string? Username { get; set; }
    }
}