# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Application/Application.csproj src/Application/
COPY src/Presentation/Presentation.csproj src/Presentation/
RUN dotnet restore src/Presentation/Presentation.csproj

COPY . .
RUN dotnet publish src/Presentation/Presentation.csproj -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Создаём нужные директории
RUN mkdir -p /app/Data && mkdir -p /app/storage && mkdir -p /app/Data/DefaultIcons && mkdir -p /app/Data/Logs && mkdir -p /app/https

# Копируем иконки
COPY src/Application/Data/DefaultIcons /app/Data/DefaultIcons

COPY certs/aspnetapp.pfx /https/aspnetapp.pfx


# Копируем приложение (без базы — она будет в volume)
COPY --from=build /app/publish .


# Точка входа
ENTRYPOINT ["dotnet", "Presentation.dll"]
