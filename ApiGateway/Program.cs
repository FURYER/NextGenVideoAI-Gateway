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
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using ApiGateway.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.WebSockets;
using HotChocolate.AspNetCore.Subscriptions;
using Microsoft.AspNetCore.Builder;
using Prometheus;
using Serilog;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using ApiGateway.GraphQL.Subscriptions;
using StackExchange.Redis;
using Microsoft.AspNetCore.Mvc;
using UserService.Protos;
using MassTransit;

public class Program
{
    public static void Main(string[] args)
    {
        Serilog.Debugging.SelfLog.Enable(Console.Error);

        // Загружаем .env файл
        Env.Load();

        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .WriteTo.Console()
                .Enrich.WithProperty("Application", "gateway")
                .Enrich.FromLogContext()
                .MinimumLevel.Information())
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
                    // Регистрируем ChatResolver с поддержкой IPublishEndpoint и PaymentResolver
                    services.AddScoped<ChatResolver>(sp => new ChatResolver(
                        sp.GetRequiredService<ILogger<ChatResolver>>(),
                        sp.GetRequiredService<IHttpContextAccessor>(),
                        sp.GetRequiredService<IPublishEndpoint>(),
                        sp.GetRequiredService<PaymentResolver>()
                    ));
                    // Регистрируем StorageResolver в DI
                    services.AddScoped<StorageResolver>();

                    // Регистрируем CurrentUserService в DI
                    services.AddScoped<ICurrentUserService, CurrentUserService>();

                    // Регистрируем StorageService в DI
                    services.AddScoped<IStorageService, StorageService>();

                    services.AddScoped<PaymentResolver>();

                    services.AddHttpContextAccessor();

                    // Регистрируем RabbitMQ сервис
                    services.AddSingleton<IRabbitMQService, RabbitMQService>();

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
                        .UseCryptographicAlgorithms(new AuthenticatedEncryptorConfiguration()
                        {
                            EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
                            ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
                        });

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
                            ClockSkew = TimeSpan.Zero,
                            ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),
                            ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY")!)
                            )
                        };
                        options.Events = new JwtBearerEvents
                        {
                            OnAuthenticationFailed = context =>
                            {
                                // Не мешаем пайплайну GraphQL, лишь логируем
                                return Task.CompletedTask;
                            },
                            OnChallenge = context =>
                            {
                                // Не перехватываем ответ тут, пускай GraphQL вернёт UNAUTHENTICATED
                                return Task.CompletedTask;
                            }
                        };
                    });

                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = new AuthorizationPolicyBuilder()
                            .RequireAuthenticatedUser()
                            .Build();

                        options.AddPolicy("AllowAnonymous", policy =>
                            policy.RequireAssertion(_ => true));
                    });

                    services.AddCors(options =>
                    {
                        options.AddDefaultPolicy(builder =>
                        {
                            builder.WithOrigins(Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(',') ?? Array.Empty<string>())
                                   .WithMethods("GET", "POST", "OPTIONS")
                                   .WithHeaders("Authorization", "Content-Type")
                                   .WithExposedHeaders("ETag")
                                   .AllowCredentials()
                                   .SetIsOriginAllowedToAllowWildcardSubdomains();
                        });
                    });

                    // Настраиваем размеры запросов
                    services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
                    {
                        options.Limits.MaxRequestBodySize = null; // Отключаем ограничение на размер запроса
                    });

                    services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
                    {
                        options.MultipartBodyLengthLimit = long.MaxValue; // Устанавливаем максимальный размер для multipart формы
                    });

                    services.AddSingleton<ITwoFactorAuthService, TwoFactorAuthService>();

                    // Настраиваем Redis
                    services.AddSingleton<IConnectionMultiplexer>(sp =>
                    {
                        var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")!;
                        return ConnectionMultiplexer.Connect(redisConnectionString);
                    });

                    // Используем Redis для хранения состояния загрузки
                    services.AddSingleton<IUploadStateService, RedisUploadStateService>();

                    services.AddAntiforgery(options =>
                    {
                        options.Cookie.Name = "XSRF-TOKEN";
                        options.Cookie.HttpOnly = false;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        options.Cookie.SameSite = SameSiteMode.Strict;
                    });

                    services.AddRateLimiter(options =>
                    {
                        options.AddFixedWindowLimiter("GlobalLimiter", fixedOptions =>
                        {
                            fixedOptions.PermitLimit = 100;
                            fixedOptions.Window = TimeSpan.FromMinutes(1);
                        });
                    });

                    services.AddGraphQLServer()
                        .AddQueryType<Query>()
                        .AddMutationType<Mutation>()
                        .AddSubscriptionType<Subscription>()
                        .AddTypeExtension<NotificationSubscription>()
                        .AddType<UserType>()
                        .AddType<RegisterUserInputType>()
                        .AddType<LoginInputType>()
                        .AddType<AuthResponseType>()
                        .AddType<EmailCodeInputType>()
                        .AddType<ConfirmEmailCodeInputType>()
                        .AddType<EmailCodePayloadType>()
                        .AddType<ChatMessageType>()
                        .AddType<SenderTypeType>()
                        .AddType<ChatStreamResponseType>()
                        .AddType<StreamChatInputType>()
                        .AddType<UpdateChatInputType>()
                        .AddType<UpdateChatPayloadType>()
                        .AddType<NotificationType>()
                        .AddType<FileInfoType>()
                        .AddType<UploadFileResultType>()
                        .AddType<DeleteFileResultType>()
                        .AddType<UploadFileInputType>()
                        .AddType<DownloadFileResultType>()
                        .AddType<FileMetadataType>()
                        .AddType<InitChunkedUploadInputType>()
                        .AddType<InitChunkedUploadResultType>()
                        .AddType<ChunkUploadInputType>()
                        .AddType<ChunkUploadResultType>()
                        .AddType<PresignedUrlType>()
                        .AddType<CompleteChunkedUploadResultType>()
                        .AddType<CancelChunkedUploadResultType>()
                        .AddType<UploadStatusType>()
                        .AddType<FileUploadProgressType>()
                        .AddType<FileChunkResponseType>()
                        .AddAuthorizationHandler<DefaultAuthorizationHandler>()
                        .AddErrorFilter<ErrorFilter>()
                        .AddDiagnosticEventListener(sp =>
                            new LoggingDiagnosticEventListener(
                                sp.GetRequiredService<ILogger<LoggingDiagnosticEventListener>>()))
                        .ModifyRequestOptions(opt =>
                        {
                            opt.IncludeExceptionDetails = true;
                            opt.ExecutionTimeout = TimeSpan.FromMinutes(60);
                        })
                        .ModifyOptions(opt =>
                        {
                            opt.DefaultBindingBehavior = BindingBehavior.Explicit;
                            opt.StrictValidation = false;
                        })
                        .AddErrorFilter(error =>
                        {
                            if (error.Exception is RpcException rpcException)
                            {
                                return ErrorBuilder.New()
                                    .SetMessage("Ошибка при получении ответа от сервиса чата")
                                    .SetCode("CHAT_SERVICE_ERROR")
                                    .SetException(error.Exception)
                                    .Build();
                            }
                            return error;
                        })
                        .AddInMemorySubscriptions()
                        // Регистрируем PaymentResolver в DI
                        .AddType<PaymentResolver>()
                        .AddType<GetUserSubscriptionPayloadType>()
                        .AddType<GetSubscriptionTypesPayloadType>()
                        .AddType<SubscriptionTypeInfoDtoType>()
                        .AddType<CreatePaymentInputType>()
                        .AddType<PaymentWebhookInputType>()
                        .AddType<GetUserQuotaPayloadType>();

                    // Настраиваем endpoint
                    services.Configure<GraphQLServerOptions>(options =>
                    {
                        options.Tool.Enable = true;
                        options.EnableSchemaRequests = false;
                        options.AllowedGetOperations = AllowedGetOperations.QueryAndMutation;
                    });

                    // Настраиваем WebSocket аутентификацию
                    services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                if (context.Request.Path.StartsWithSegments("/graphql/ws"))
                                {
                                    string? token = null;
                                    if (context.Request.Query.TryGetValue("authorization", out var authQuery))
                                    {
                                        token = authQuery.ToString().Replace("Bearer ", "");
                                    }
                                    else if (context.Request.Headers.TryGetValue("Authorization", out var authHeader) && authHeader.ToString().StartsWith("Bearer "))
                                    {
                                        token = authHeader.ToString().Substring(7);
                                    }
                                    else if (context.Request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
                                    {
                                        var protocols = context.Request.Headers["Sec-WebSocket-Protocol"].ToString().Split(',');
                                        foreach (var protocol in protocols)
                                        {
                                            var trimmed = protocol.Trim();
                                            if (trimmed.StartsWith("Bearer "))
                                            {
                                                token = trimmed.Substring(7);
                                                break;
                                            }
                                        }
                                    }

                                    if (!string.IsNullOrWhiteSpace(token))
                                    {
                                        context.Token = token;
                                        context.HttpContext.Items["WebSocketAuthToken"] = token;
                                    }
                                }

                                // Стандартный заголовок для прочих запросов
                                var authHeaderDefault = context.Request.Headers["Authorization"].ToString();
                                if (!string.IsNullOrEmpty(authHeaderDefault) && authHeaderDefault.StartsWith("Bearer "))
                                {
                                    context.Token = authHeaderDefault.Substring(7);
                                }

                                return Task.CompletedTask;
                            }
                        };
                    });

                    // Регистрируем UserServiceClient в DI
                    services.AddSingleton<UserService.Protos.UserService.UserServiceClient>(sp =>
                    {
                        var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL");
                        var channel = Grpc.Net.Client.GrpcChannel.ForAddress(userServiceUrl!);
                        return new UserService.Protos.UserService.UserServiceClient(channel);
                    });

                    // Настраиваем MassTransit с RabbitMQ (через переменные окружения)
                    services.AddMassTransit(x =>
                    {
                        x.UsingRabbitMq((context, cfg) =>
                        {
                            var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
                            var rabbitPort = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
                            var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest";
                            var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";

                            ushort port = 5672;
                            var hostWithPort = port != 5672 ? $"{rabbitHost}:{port}" : rabbitHost;
                            cfg.Host(hostWithPort, "/", h =>
                            {
                                h.Username(rabbitUser);
                                h.Password(rabbitPass);
                            });
                        });
                    });
                })
                .ConfigureKestrel(serverOptions =>
                {
                    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

                    if (environment == "Development")
                    {
                        // В dev используем только HTTP
                        serverOptions.ListenAnyIP(8000);
                    }
                    else
                    {
                        // В production используем HTTPS
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
                    }
                })
                .Configure(app =>
                {
                    app.UseRouting();

                    app.UseMetricServer(); // Экспортирует по пути /metrics по умолчанию
                    app.UseHttpMetrics();

                    app.UseWebSockets(new WebSocketOptions
                    {
                        KeepAliveInterval = TimeSpan.FromMinutes(2),
                        AllowedOrigins = { "*" }
                    });

                    app.UseCors();

                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Path.StartsWithSegments("/graphql/ws"))
                        {
                            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                            string? token = null;

                            // 1) query параметр authorization=Bearer xxx
                            if (context.Request.Query.TryGetValue("authorization", out var authQuery))
                            {
                                token = authQuery.ToString().Replace("Bearer ", "");
                                if (!string.IsNullOrWhiteSpace(token))
                                {
                                    logger.LogInformation("[WS Handshake] Токен получен из query ?authorization");
                                }
                            }

                            // 2) Header Authorization: Bearer xxx
                            if (string.IsNullOrWhiteSpace(token) && context.Request.Headers.TryGetValue("Authorization", out var authHeader) && authHeader.ToString().StartsWith("Bearer "))
                            {
                                token = authHeader.ToString().Substring(7);
                                logger.LogInformation("[WS Handshake] Токен получен из заголовка Authorization");
                            }

                            // 3) Sec-WebSocket-Protocol: Bearer xxx
                            if (string.IsNullOrWhiteSpace(token) && context.Request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
                            {
                                var protocols = context.Request.Headers["Sec-WebSocket-Protocol"].ToString().Split(',');
                                foreach (var protocol in protocols)
                                {
                                    var trimmed = protocol.Trim();
                                    if (trimmed.StartsWith("Bearer "))
                                    {
                                        token = trimmed.Substring(7);
                                        logger.LogInformation("[WS Handshake] Токен получен из заголовка Sec-WebSocket-Protocol");
                                        break;
                                    }
                                }
                            }

                            if (string.IsNullOrWhiteSpace(token))
                            {
                                logger.LogWarning("[WS Handshake] Токен отсутствует — отклоняем подключение (401)");
                                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                await context.Response.WriteAsync("Unauthorized: missing token");
                                return;
                            }

                            try
                            {
                                var userServiceClient = context.RequestServices.GetRequiredService<UserService.Protos.UserService.UserServiceClient>();
                                var validateResponse = await userServiceClient.ValidateTokenAsync(new UserService.Protos.ValidateTokenRequest { Token = token });

                                if (!validateResponse.IsValid)
                                {
                                    logger.LogWarning("[WS Handshake] Токен не валиден — отклоняем подключение (401)");
                                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                    await context.Response.WriteAsync("Unauthorized: invalid token");
                                    return;
                                }

                                // Сохраняем в Items для дальнейших резолверов/авторизации
                                context.Items["WebSocketAuthToken"] = token;
                                context.Items["UserId"] = validateResponse.UserId;
                                context.Items["TokenInfo"] = validateResponse.TokenInfo;

                                logger.LogInformation("[WS Handshake] Токен валиден, пользователь {UserId}. Разрешаем подключение.", validateResponse.UserId);
                            }
                            catch (Exception ex)
                            {
                                var error = ex.Message;
                                logger.LogError(ex, "[WS Handshake] Ошибка при валидации токена: {Error}", error);
                                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                await context.Response.WriteAsync("Unauthorized: validation error");
                                return;
                            }
                        }

                        await next();
                    });

                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGraphQLWebSocket("/graphql/ws");

                        // Настраиваем GraphQL endpoint и включаем GraphiQL UI
                        endpoints.MapGraphQL("/graphql")
                            .WithOptions(new GraphQLServerOptions
                            {
                                Tool = { Enable = false }, // Полностью отключает UI
                                EnableSchemaRequests = false, // Отключает скачивание схемы
                                AllowedGetOperations = AllowedGetOperations.QueryAndMutation
                            });

                        endpoints.MapGet("/", async context =>
                        {
                            await context.Response.WriteAsync("GraphQL API Gateway is running. Access GraphiQL at /graphql");
                        });

                        endpoints.MapGet("/files/{fileId}/download", async (
                            string fileId,
                            HttpContext context,
                            [FromServices] IStorageService storageService,
                            [FromServices] ICurrentUserService currentUserService,
                            [FromServices] UserService.Protos.UserService.UserServiceClient userServiceClient,
                            [FromServices] ILogger<Program> logger) =>
                        {
                            try
                            {
                                // Валидируем токен ТОЛЬКО через UserService
                                var authHeader = context.Request.Headers["Authorization"].ToString();
                                var token = string.IsNullOrWhiteSpace(authHeader) ? string.Empty : authHeader.Replace("Bearer ", "");
                                if (string.IsNullOrWhiteSpace(token))
                                {
                                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                    await context.Response.WriteAsJsonAsync(new { error = "Unauthorized", message = "Отсутствует токен авторизации" });
                                    return;
                                }

                                var validateResponse = await userServiceClient.ValidateTokenAsync(new ValidateTokenRequest { Token = token });
                                if (!validateResponse.IsValid)
                                {
                                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                    await context.Response.WriteAsJsonAsync(new { error = "Unauthorized", message = "Невалидный токен" });
                                    return;
                                }

                                // Сохраняем UserId для CurrentUserService
                                context.Items["UserId"] = validateResponse.UserId;

                                var userId = currentUserService.GetCurrentUserId();

                                // Получаем метаданные файла
                                var metadata = await storageService.GetFileMetadata(fileId, userId);
                                if (metadata == null)
                                {
                                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                                    await context.Response.WriteAsJsonAsync(new { error = "NotFound", message = "Файл не найден" });
                                    return;
                                }

                                // Проверяем размер файла
                                if (metadata.Size <= 0)
                                {
                                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                    await context.Response.WriteAsJsonAsync(new { error = "InvalidFile", message = "Некорректный размер файла" });
                                    return;
                                }

                                // Устанавливаем заголовки ответа
                                context.Response.Headers["Content-Type"] = metadata.ContentType;
                                context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{metadata.FileName}\"";
                                context.Response.Headers["Transfer-Encoding"] = "chunked";

                                // Получаем поток и передаем его напрямую
                                var stream = await storageService.DownloadFile(fileId, userId);
                                if (stream == null)
                                {
                                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                    await context.Response.WriteAsJsonAsync(new { error = "InternalServerError", message = "Не удалось получить файл" });
                                    return;
                                }

                                // Отправляем файл потоком
                                await stream.CopyToAsync(context.Response.Body);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                if (!context.Response.HasStarted)
                                {
                                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                    await context.Response.WriteAsJsonAsync(new { error = "Unauthorized", message = "У вас нет доступа к этому файлу" });
                                }
                                else
                                {
                                    // Если заголовки уже отправлены, просто логируем ошибку
                                    logger.LogWarning("Попытка несанкционированного доступа к файлу {FileId}", fileId);
                                }
                            }
                            catch (Exception ex)
                            {
                                if (!context.Response.HasStarted)
                                {
                                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                    await context.Response.WriteAsJsonAsync(new { error = "InternalServerError", message = ex.Message });
                                }
                                else
                                {
                                    // Если заголовки уже отправлены, просто логируем ошибку
                                    logger.LogError(ex, "Ошибка при отправке файла {FileId}", fileId);
                                }
                            }
                        });
                    });

                    // Стартуем RabbitMQ сервис для прослушивания сообщений
                    var rabbitMQService = app.ApplicationServices.GetRequiredService<IRabbitMQService>();
                    rabbitMQService.StartConsumingAsync().GetAwaiter().GetResult();
                });
            });
}