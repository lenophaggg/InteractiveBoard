# Базовый этап: запуск приложения
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Настройка репозитория и установка необходимых утилит
RUN apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    rm -f /etc/apt/sources.list.d/debian.sources && \
    echo "deb https://deb.debian.org/debian bookworm main" > /etc/apt/sources.list && \
    echo 'Acquire::Retries "3";' > /etc/apt/apt.conf.d/80-retries && \
    apt-get update -o Acquire::ForceIPv4=true && \
    apt-get install -y --no-install-recommends \
        poppler-utils \
        ca-certificates \
        openssl \
        yt-dlp \
        ffmpeg && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    chmod -R 777 /app

# Этап сборки приложения
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        openssl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

COPY ["MyMvcApp.csproj", "./"]
RUN dotnet restore "./MyMvcApp.csproj"

COPY . .
RUN dotnet build "MyMvcApp.csproj" -c Release -o /app/build

# Этап публикации приложения
FROM build AS publish
RUN dotnet publish "MyMvcApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Финальный этап: копирование опубликованного приложения
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .
COPY wwwroot /app/wwwroot

# Указываем, что контейнер будет слушать порт 8082
EXPOSE 8082

ENTRYPOINT ["dotnet", "MyMvcApp.dll"]
