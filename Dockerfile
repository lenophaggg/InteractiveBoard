# Базовый этап: запуск приложения
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Удаляем кэш apt, удаляем дублирующийся файл источников и задаём нужный репозиторий (с HTTPS)
RUN apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    rm -f /etc/apt/sources.list.d/debian.sources && \
    echo "deb https://deb.debian.org/debian bookworm main" > /etc/apt/sources.list && \
    apt-get update && \
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

ENTRYPOINT ["dotnet", "MyMvcApp.dll"]
