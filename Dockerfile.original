FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

RUN apt-get update --fix-missing && \
    apt-get install -y poppler-utils ca-certificates openssl yt-dlp ffmpeg && \
    update-ca-certificates && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

RUN apt-get update --fix-missing && \
    apt-get install -y ca-certificates openssl yt-dlp && \
    update-ca-certificates && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

COPY ["MyMvcApp.csproj", "./"]
RUN dotnet restore "./MyMvcApp.csproj"

COPY . . 
RUN dotnet build "MyMvcApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyMvcApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish . 
COPY wwwroot /app/wwwroot

ENTRYPOINT ["dotnet", "MyMvcApp.dll"]