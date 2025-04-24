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
                                   .AllowCredentials()
                                   .SetIsOriginAllowedToAllowWildcardSubdomains();
                        });
                    });

                    // Добавляем сервисы
                    services.AddSingleton<ITwoFactorAuthService, TwoFactorAuthService>();

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
                        .AddType<UserType>()
                        .AddType<RegisterUserInputType>()
                        .AddType<LoginInputType>()
                        .AddType<AuthResponseType>()
                        .AddType<ChatMessageType>()
                        .AddType<SenderTypeType>()
                        .AddType<ChatStreamResponseType>()
                        .AddType<StreamChatInputType>()
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
                        });

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