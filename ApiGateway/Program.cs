// ApiGateway/Program.cs
using DotNetEnv;
using ApiGateway.GraphQL;
using HotChocolate.AspNetCore;
using Grpc.Core;

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

                    // Добавляем GraphQL-сервер
                    services.AddGraphQLServer()
                        .AddQueryType<Query>()
                        .AddMutationType<Mutation>()
                        .AddType<User>()
                        .AddErrorFilter(error =>
                        {
                            // Обработка gRPC-ошибок
                            if (error.Exception is RpcException rpcException)
                            {
                                return error.WithMessage(rpcException.Status.Detail)
                                            .WithCode(rpcException.StatusCode.ToString());
                            }

                            // Отображаем детали других ошибок
                            return error.WithMessage(error.Exception?.Message ?? error.Message);
                        });
                })
                .Configure(app =>
                {
                    app.UseRouting();

                    app.UseEndpoints(endpoints =>
                    {
                        // Настраиваем GraphQL endpoint и включаем GraphiQL UI
                        endpoints.MapGraphQL()
                            .WithOptions(new GraphQLServerOptions
                            {
                                Tool = { Enable = true } // Включаем GraphiQL UI
                            });

                        endpoints.MapGet("/", async context =>
                        {
                            await context.Response.WriteAsync("GraphQL API Gateway is running. Access GraphiQL at /graphql");
                        });
                    });
                });
            });
}