// ApiGateway/Program.cs
using DotNetEnv;
using ApiGateway.GraphQL;
using HotChocolate.AspNetCore;
using HotChocolate; // Для IError
using Grpc.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using HotChocolate.Execution;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using HotChocolate.Types;
using HotChocolate.Types.Pagination;

var builder = WebApplication.CreateBuilder(args);

// Загружаем .env файл
Env.Load();

// Регистрируем UserResolver в DI
builder.Services.AddScoped<UserResolver>();

// Добавляем HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Настраиваем аутентификацию JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "NextGenVideoAI",
        ValidAudience = "NextGenVideoAI",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-very-long-secret-key-1234567890"))
    };
});

// Добавляем авторизацию
builder.Services.AddAuthorization(options =>
{
    // Добавляем политику по умолчанию, требующую аутентификацию
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Добавляем политику для анонимного доступа
    options.AddPolicy("AllowAnonymous", policy =>
        policy.RequireAssertion(context => true));
});

// Добавляем CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Добавляем GraphQL-сервер
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddType<UserType>()
    .AddType<RegisterUserInputType>()
    .AddType<UpdateCreditsInputType>()
    .AddType<LoginInputType>()
    .AddType<AuthResponseType>()
    .AddAuthorizationHandler<DefaultAuthorizationHandler>()
    .AddErrorFilter<ErrorFilter>()
    .ModifyRequestOptions(opt =>
    {
        opt.IncludeExceptionDetails = true;
    })
    .ModifyOptions(opt =>
    {
        opt.DefaultBindingBehavior = BindingBehavior.Explicit;
        opt.StrictValidation = false;
    });

// Настраиваем endpoint
builder.Services.Configure<GraphQLServerOptions>(options =>
{
    options.Tool.Enable = true;
    options.EnableSchemaRequests = true;
    options.AllowedGetOperations = AllowedGetOperations.QueryAndMutation;
});

var app = builder.Build();

// Добавляем CORS middleware
app.UseCors();

// Добавляем middleware для аутентификации и авторизации
app.UseAuthentication();
app.UseAuthorization();

// Настраиваем GraphQL endpoint и включаем GraphiQL UI
app.MapGraphQL()
    .WithOptions(new GraphQLServerOptions
    {
        Tool = { Enable = true }, // Включаем GraphiQL UI
        EnableSchemaRequests = true, // Включаем запросы схемы
        AllowedGetOperations = AllowedGetOperations.QueryAndMutation // Разрешаем GET запросы для Query и Mutation
    });

app.MapGet("/", async context =>
{
    await context.Response.WriteAsync("GraphQL API Gateway is running. Access GraphiQL at /graphql");
});

app.Run();