FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем файлы проекта
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]
RUN dotnet restore "ApiGateway/ApiGateway.csproj"

# Копируем исходный код
COPY . .
RUN dotnet build "ApiGateway/ApiGateway.csproj" -c Release -o /app/build

# Публикуем приложение
FROM build AS publish
RUN dotnet publish "ApiGateway/ApiGateway.csproj" -c Release -o /app/publish

# Финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/keys && chmod 700 /app/keys
ENTRYPOINT ["dotnet", "ApiGateway.dll"]