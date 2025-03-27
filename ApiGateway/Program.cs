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
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;

public class Program
{
    public static void Main(string[] args)
    {
        // Загружаем .env файл
        Env.Load();

        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddEnvironmentVariables();
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureServices(services =>
                {
                    // Регистрируем UserResolver в DI
                    services.AddScoped<UserResolver>();

                    // Добавляем HttpContextAccessor
                    services.AddHttpContextAccessor();

                    // Настраиваем Data Protection API
                    var dataProtectionPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys");

                    var certificatePassword = Environment.GetEnvironmentVariable("CERTIFICATE_PASSWORD");
                    var certificatePath = $"certs/{Environment.GetEnvironmentVariable("CERTIFICATE_NAME")}";

                    // Создаем SecureString из пароля
                    var securePassword = new System.Security.SecureString();
                    foreach (char c in certificatePassword!)
                    {
                        securePassword.AppendChar(c);
                    }

                    services.AddDataProtection()
                        .SetApplicationName("Gateway")
                        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
                        .ProtectKeysWithCertificate(new X509Certificate2(
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, certificatePath!),
                        securePassword
                    ))
                        .SetDefaultKeyLifetime(TimeSpan.FromDays(30));

                    // Настраиваем аутентификацию JWT
                    services.AddAuthentication(options =>
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
                            ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),
                            ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY")!)
                            )
                        };
                    });

                    // Добавляем авторизацию
                    services.AddAuthorization(options =>
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
                    services.AddCors(options =>
                    {
                        options.AddDefaultPolicy(builder =>
                        {
                            builder.AllowAnyOrigin()
                                   .AllowAnyMethod()
                                   .AllowAnyHeader();
                        });
                    });

                    // Добавляем GraphQL-сервер
                    services.AddGraphQLServer()
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
                    services.Configure<GraphQLServerOptions>(options =>
                    {
                        options.Tool.Enable = true;
                        options.EnableSchemaRequests = true;
                        options.AllowedGetOperations = AllowedGetOperations.QueryAndMutation;
                    });

                    // Настраиваем логирование Kestrel
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.AddDebug();
                    });
                })
                .ConfigureKestrel(serverOptions =>
                {
                    serverOptions.ListenAnyIP(8000, listenOptions =>
                    {
                        listenOptions.UseHttps(httpsOptions =>
                        {
                            var certificatePassword = Environment.GetEnvironmentVariable("CERTIFICATE_PASSWORD");
                            var certificatePath = $"certs/{Environment.GetEnvironmentVariable("CERTIFICATE_NAME")}";

                            // Создаем SecureString из пароля
                            var securePassword = new System.Security.SecureString();
                            foreach (char c in certificatePassword!)
                            {
                                securePassword.AppendChar(c);
                            }

                            httpsOptions.ServerCertificate = new X509Certificate2(
                                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, certificatePath!),
                                securePassword
                            );
                        });
                    });
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                })
                .Configure(app =>
                {
                    app.UseRouting();

                    // Добавляем CORS middleware
                    app.UseCors();

                    // Добавляем middleware для аутентификации и авторизации
                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.UseEndpoints(endpoints =>
                    {
                        // Настраиваем GraphQL endpoint и включаем GraphiQL UI
                        endpoints.MapGraphQL()
                            .WithOptions(new GraphQLServerOptions
                            {
                                Tool = { Enable = true }, // Включаем GraphiQL UI
                                EnableSchemaRequests = true, // Включаем запросы схемы
                                AllowedGetOperations = AllowedGetOperations.QueryAndMutation // Разрешаем GET запросы для Query и Mutation
                            });

                        endpoints.MapGet("/", async context =>
                        {
                            await context.Response.WriteAsync("GraphQL API Gateway is running. Access GraphiQL at /graphql");
                        });
                    });
                });
            });
}