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
using Serilog.Sinks.Elasticsearch;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using ApiGateway.GraphQL.Subscriptions;
using StackExchange.Redis;
using Microsoft.AspNetCore.Mvc;
using UserService.Protos;

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
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(Environment.GetEnvironmentVariable("ELASTICSEARCH_URL")!))
                {
                    IndexFormat = "dotnet-logs-{0:yyyy.MM.dd}",
                    AutoRegisterTemplate = true,
                    EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog |
                          EmitEventFailureHandling.WriteToFailureSink,
                    ModifyConnectionSettings = c => c
                        .BasicAuthentication("elastic", "lSoBIBgQjc4nFQWArZBt")
                        .ServerCertificateValidationCallback((o, cert, chain, errors) => true)
                })
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
                    services.AddScoped<ChatResolver>();
                    // Регистрируем StorageResolver в DI
                    services.AddScoped<StorageResolver>();

                    // Регистрируем CurrentUserService в DI
                    services.AddScoped<ICurrentUserService, CurrentUserService>();

                    // Регистрируем StorageService в DI
                    services.AddScoped<IStorageService, StorageService>();

                    // Добавляем HttpContextAccessor
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
                                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                                {
                                    context.Response.Headers["Token-Expired"] = "true";
                                    context.Response.StatusCode = 401;
                                }
                                return Task.CompletedTask;
                            },
                            OnChallenge = context =>
                            {
                                context.HandleResponse();
                                context.Response.StatusCode = 401;
                                context.Response.ContentType = "application/json";
                                var result = System.Text.Json.JsonSerializer.Serialize(new
                                {
                                    error = "Unauthorized",
                                    message = "Invalid token or token expired"
                                });
                                return context.Response.WriteAsync(result);
                            }
                        };
                    });

                    // Добавляем авторизацию
                    services.AddAuthorization(options =>
                    {
                        // Добавляем политику по умолчанию, требующую аутентификацию
                        options.DefaultPolicy = new AuthorizationPolicyBuilder()
                            .RequireAuthenticatedUser()
                            .Build();

                        // Добавляем политику AllowAnonymous для обхода авторизации
                        options.AddPolicy("AllowAnonymous", policy =>
                            policy.RequireAssertion(_ => true));
                    });

                    // Добавляем CORS
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

                    // Добавляем сервисы
                    services.AddSingleton<ITwoFactorAuthService, TwoFactorAuthService>();

                    // Настраиваем Redis
                    services.AddSingleton<IConnectionMultiplexer>(sp =>
                    {
                        var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")!;
                        return ConnectionMultiplexer.Connect(redisConnectionString);
                    });

                    // Используем Redis для хранения состояния загрузки
                    services.AddSingleton<IUploadStateService, RedisUploadStateService>();

                    // Добавляем защиту от CSRF
                    services.AddAntiforgery(options =>
                    {
                        options.Cookie.Name = "XSRF-TOKEN";
                        options.Cookie.HttpOnly = false;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        options.Cookie.SameSite = SameSiteMode.Strict;
                    });

                    // Добавляем rate limiting
                    services.AddRateLimiter(options =>
                    {
                        options.AddFixedWindowLimiter("GlobalLimiter", fixedOptions =>
                        {
                            fixedOptions.PermitLimit = 100;
                            fixedOptions.Window = TimeSpan.FromMinutes(1);
                        });
                    });

                    // Добавляем GraphQL-сервер
                    services.AddGraphQLServer()
                        .AddQueryType<Query>()
                        .AddMutationType<Mutation>()
                        .AddSubscriptionType<Subscription>()
                        .AddTypeExtension<NotificationSubscription>()
                        .AddType<UserType>()
                        .AddType<RegisterUserInputType>()
                        .AddType<LoginInputType>()
                        .AddType<AuthResponseType>()
                        .AddType<ChatMessageType>()
                        .AddType<SenderTypeType>()
                        .AddType<ChatStreamResponseType>()
                        .AddType<StreamChatInputType>()
                        .AddType<NotificationType>()
                        // Добавляем типы хранилища файлов
                        .AddType<FileInfoType>()
                        .AddType<UploadFileResultType>()
                        .AddType<DeleteFileResultType>()
                        .AddType<UploadFileInputType>()
                        .AddType<DownloadFileResultType>()
                        .AddType<FileMetadataType>()
                        // Добавляем типы для фрагментированной загрузки файлов
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
                        .AddInMemorySubscriptions();

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
                                // Логируем путь и заголовки для диагностики
                                Console.WriteLine($"Path: {context.Request.Path}, Method: {context.Request.Method}");

                                // Проверяем WebSocket соединения для GraphQL
                                if (context.Request.Path.StartsWithSegments("/graphql/ws"))
                                {
                                    Console.WriteLine("WebSocket connection detected");
                                    string? token = null;

                                    // Ищем токен в connectionParams authorization
                                    if (context.Request.Query.TryGetValue("authorization", out var authQuery))
                                    {
                                        Console.WriteLine($"Found token in query: {authQuery}");
                                        token = authQuery.ToString().Replace("Bearer ", "");
                                    }
                                    // Проверяем заголовок Authorization
                                    else
                                    {
                                        var authHeader = context.Request.Headers["Authorization"].ToString();
                                        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                                        {
                                            Console.WriteLine($"Found token in Authorization header: {authHeader}");
                                            token = authHeader.Substring(7);
                                        }
                                    }

                                    // Если токен найден, сохраняем его и в контексте
                                    if (token != null)
                                    {
                                        context.Token = token;
                                        // Важно: сохраняем токен в HttpContext.Items для обработчика авторизации
                                        context.HttpContext.Items["WebSocketAuthToken"] = token;
                                        return Task.CompletedTask;
                                    }

                                    // Проверяем заголовок Sec-WebSocket-Protocol 
                                    if (context.Request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
                                    {
                                        Console.WriteLine($"WebSocket Protocols: {context.Request.Headers["Sec-WebSocket-Protocol"]}");
                                        var protocols = context.Request.Headers["Sec-WebSocket-Protocol"].ToString().Split(',');
                                        foreach (var protocol in protocols)
                                        {
                                            var trimmedProtocol = protocol.Trim();
                                            if (trimmedProtocol.StartsWith("Bearer "))
                                            {
                                                Console.WriteLine($"Found token in protocol: {trimmedProtocol}");
                                                token = trimmedProtocol.Substring(7);
                                                context.Token = token;
                                                context.HttpContext.Items["WebSocketAuthToken"] = token;
                                                return Task.CompletedTask;
                                            }
                                        }
                                    }

                                    // Для диагностики проверяем все заголовки
                                    foreach (var header in context.Request.Headers)
                                    {
                                        Console.WriteLine($"Header: {header.Key} = {header.Value}");
                                    }

                                    // Временный хак для отладки: проверка всех параметров запроса
                                    foreach (var param in context.Request.Query)
                                    {
                                        Console.WriteLine($"Query param: {param.Key} = {param.Value}");
                                        if (param.Key.Contains("token") || param.Key.Contains("auth"))
                                        {
                                            token = param.Value.ToString().Replace("Bearer ", "");
                                            context.Token = token;
                                            context.HttpContext.Items["WebSocketAuthToken"] = token;
                                            return Task.CompletedTask;
                                        }
                                    }
                                }

                                // Для любых других запросов проверяем стандартный заголовок
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
                    app.UseHttpMetrics(); // Собирает метрики HTTP-запросов

                    // Добавляем WebSocket middleware с настройками
                    app.UseWebSockets(new WebSocketOptions
                    {
                        KeepAliveInterval = TimeSpan.FromMinutes(2),
                        AllowedOrigins = { "*" }
                    });

                    // Добавляем CORS middleware
                    app.UseCors();

                    // Добавляем middleware для аутентификации и авторизации
                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.UseEndpoints(endpoints =>
                    {
                        // Добавляем WebSocket endpoint для GraphQL с явным указанием пути
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

                        // Добавляем REST endpoint для скачивания файлов
                        endpoints.MapGet("/files/{fileId}/download", async (
                            string fileId,
                            HttpContext context,
                            [FromServices] IStorageService storageService,
                            [FromServices] ICurrentUserService currentUserService,
                            [FromServices] ILogger<Program> logger) =>
                        {
                            try
                            {
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
                        }).RequireAuthorization();
                    });

                    // Стартуем RabbitMQ сервис для прослушивания сообщений
                    var rabbitMQService = app.ApplicationServices.GetRequiredService<IRabbitMQService>();
                    rabbitMQService.StartConsumingAsync().GetAwaiter().GetResult();
                });
            });
}