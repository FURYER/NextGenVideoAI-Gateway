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
            descriptor.Field(f => f.Password).Type<NonNullType<StringType>>().Description("Пароль пользователя");
        }
    }

    public class RegisterUserInput
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    // Login types
    public class LoginInputType : InputObjectType<LoginInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<LoginInput> descriptor)
        {
            descriptor.Description("Входные данные для входа в систему");
            descriptor.Field(f => f.Username).Type<NonNullType<StringType>>().Description("Имя пользователя");
            descriptor.Field(f => f.Password).Type<NonNullType<StringType>>().Description("Пароль пользователя");
            descriptor.Field(f => f.TwoFactorCode).Type<StringType>().Description("Код двухфакторной аутентификации");
            descriptor.Field(f => f.TwoFactorSecret).Type<StringType>().Description("Секретный ключ для 2FA");
        }
    }

    public class LoginInput
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? TwoFactorCode { get; set; }
        public string? TwoFactorSecret { get; set; }
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
            descriptor.Field(f => f.Email).Type<StringType>().Description("Email пользователя");
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
        public string? Email { get; set; }
    }

    // User Profile types
    public class UserProfileDto
    {
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? CreatedAt { get; set; }
    }

    public class UserProfileType : ObjectType<UserProfileDto>
    {
        protected override void Configure(IObjectTypeDescriptor<UserProfileDto> descriptor)
        {
            descriptor.Description("Профиль пользователя");
            descriptor.Field(f => f.UserId).Type<StringType>().Description("ID пользователя");
            descriptor.Field(f => f.Username).Type<StringType>().Description("Имя пользователя");
            descriptor.Field(f => f.Email).Type<StringType>().Description("Email пользователя");
            descriptor.Field(f => f.CreatedAt).Type<StringType>().Description("Дата создания аккаунта");
        }
    }

    public class UserProfileResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public UserProfileDto? Profile { get; set; }
    }

    public class UserProfileResponseType : ObjectType<UserProfileResponse>
    {
        protected override void Configure(IObjectTypeDescriptor<UserProfileResponse> descriptor)
        {
            descriptor.Description("Ответ на запрос профиля пользователя");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Успешность операции");
            descriptor.Field(f => f.Message).Type<StringType>().Description("Сообщение об операции");
            descriptor.Field(f => f.Profile).Type<UserProfileType>().Description("Данные профиля пользователя");
        }
    }

    // Типы для работы с email-кодом
    public class EmailCodeInput
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
    }

    public class ConfirmEmailCodeInput
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? Code { get; set; }
    }

    public class EmailCodePayload
    {
        public bool Success { get; set; }
    }

    // Payment types
    public class CreatePaymentInput
    {
        public string UserId { get; set; } = string.Empty;
        public string SubscriptionTypeId { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class CreatePaymentPayload
    {
        public string PaymentId { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class PaymentWebhookInput
    {
        public string PaymentId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ProviderPayload { get; set; } = string.Empty;
    }

    public class PaymentWebhookPayload
    {
        public bool Success { get; set; }
    }

    public class GetUserSubscriptionPayload
    {
        public string SubscriptionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ExpiresAt { get; set; } = string.Empty;
    }

    public class SubscriptionTypeInfoDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public int DurationDays { get; set; }
    }

    public class GetSubscriptionTypesPayload
    {
        public List<SubscriptionTypeInfoDto> Types { get; set; } = new();
    }

    public class UseDemoQuotaPayload
    {
        public int DemoQuotaLeft { get; set; }
        public bool Allowed { get; set; }
    }

    public class GetUserSubscriptionPayloadType : ObjectType<GetUserSubscriptionPayload>
    {
        protected override void Configure(IObjectTypeDescriptor<GetUserSubscriptionPayload> descriptor)
        {
            descriptor.Description("Информация о подписке пользователя");
            descriptor.Field(f => f.SubscriptionType).Type<StringType>().Description("Тип подписки");
            descriptor.Field(f => f.Status).Type<StringType>().Description("Статус подписки");
            descriptor.Field(f => f.ExpiresAt).Type<StringType>().Description("Дата окончания");
        }
    }

    public class SubscriptionTypeInfoDtoType : ObjectType<SubscriptionTypeInfoDto>
    {
        protected override void Configure(IObjectTypeDescriptor<SubscriptionTypeInfoDto> descriptor)
        {
            descriptor.Description("Тип подписки");
            descriptor.Field(f => f.Id).Type<StringType>().Description("ID типа");
            descriptor.Field(f => f.Name).Type<StringType>().Description("Название");
            descriptor.Field(f => f.Description).Type<StringType>().Description("Описание");
            descriptor.Field(f => f.Price).Type<FloatType>().Description("Цена");
            descriptor.Field(f => f.DurationDays).Type<IntType>().Description("Длительность (дней)");
        }
    }

    public class GetSubscriptionTypesPayloadType : ObjectType<GetSubscriptionTypesPayload>
    {
        protected override void Configure(IObjectTypeDescriptor<GetSubscriptionTypesPayload> descriptor)
        {
            descriptor.Description("Список типов подписок");
            descriptor.Field(f => f.Types).Type<ListType<SubscriptionTypeInfoDtoType>>().Description("Типы подписок");
        }
    }

    public class CreatePaymentInputType : InputObjectType<CreatePaymentInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<CreatePaymentInput> descriptor)
        {
            descriptor.Description("Входные данные для создания платежа");
            descriptor.Field(f => f.UserId).Type<NonNullType<StringType>>().Description("ID пользователя");
            descriptor.Field(f => f.SubscriptionTypeId).Type<NonNullType<StringType>>().Description("ID типа подписки");
            descriptor.Field(f => f.PaymentMethod).Type<NonNullType<StringType>>().Description("Метод оплаты");
        }
    }

    public class PaymentWebhookInputType : InputObjectType<PaymentWebhookInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<PaymentWebhookInput> descriptor)
        {
            descriptor.Description("Входные данные для webhook платежа");
            descriptor.Field(f => f.PaymentId).Type<NonNullType<StringType>>().Description("ID платежа");
            descriptor.Field(f => f.Status).Type<NonNullType<StringType>>().Description("Статус платежа");
            descriptor.Field(f => f.ProviderPayload).Type<NonNullType<StringType>>().Description("Payload провайдера");
        }
    }

    public class GetUserQuotaPayload
    {
        public int QuotaLeft { get; set; }
    }

    public class GetUserQuotaPayloadType : ObjectType<GetUserQuotaPayload>
    {
        protected override void Configure(IObjectTypeDescriptor<GetUserQuotaPayload> descriptor)
        {
            descriptor.Description("Информация о доступной квоте пользователя (платёжная система)");
            descriptor.Field(f => f.QuotaLeft).Type<IntType>().Description("Оставшаяся квота пользователя");
        }
    }

    public class EmailCodeInputType : InputObjectType<EmailCodeInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<EmailCodeInput> descriptor)
        {
            descriptor.Description("Входные данные для запроса email-кода");
            descriptor.Field(f => f.UserId).Type<StringType>().Description("ID пользователя");
            descriptor.Field(f => f.Email).Type<StringType>().Description("Email пользователя");
        }
    }

    public class ConfirmEmailCodeInputType : InputObjectType<ConfirmEmailCodeInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<ConfirmEmailCodeInput> descriptor)
        {
            descriptor.Description("Входные данные для подтверждения email-кода");
            descriptor.Field(f => f.UserId).Type<StringType>().Description("ID пользователя");
            descriptor.Field(f => f.Email).Type<StringType>().Description("Email пользователя");
            descriptor.Field(f => f.Code).Type<StringType>().Description("Код подтверждения");
        }
    }

    public class EmailCodePayloadType : ObjectType<EmailCodePayload>
    {
        protected override void Configure(IObjectTypeDescriptor<EmailCodePayload> descriptor)
        {
            descriptor.Description("Результат подтверждения или запроса email-кода");
            descriptor.Field(f => f.Success).Type<NonNullType<BooleanType>>().Description("Успешность операции");
        }
    }
}