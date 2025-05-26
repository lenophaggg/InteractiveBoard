# Базовый этап: запуск приложения
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Настройка репозитория и установка необходимых утилит
RUN cat << 'EOF' > /etc/apt/sources.list
deb https://deb.debian.org/debian bookworm main
deb https://deb.debian.org/debian bookworm-updates main
deb https://security.debian.org/debian-security bookworm-security main
EOF

RUN echo 'Acquire::ForceIPv4 "true";' > /etc/apt/apt.conf.d/99force-ipv4 \
 && echo 'Acquire::Retries "3";'      > /etc/apt/apt.conf.d/99retries \
 && apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates \
      poppler-utils \
      openssl \
      ffmpeg \
      python3-pip \
 && rm -rf /var/lib/apt/lists/*

# Ставим yt-dlp через pip
RUN pip3 install --no-cache-dir yt-dlp

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