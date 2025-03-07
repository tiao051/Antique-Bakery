# === STAGE 1: BUILD & PUBLISH ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Tắt telemetry để tối ưu build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Copy file project (.csproj) để cache dependencies
COPY *.sln ./ 
COPY highlands/*.csproj highlands/
RUN dotnet restore highlands/highlands.csproj

# Copy toàn bộ source code và build
COPY highlands/ highlands/
WORKDIR /app/highlands
RUN dotnet publish -c Release -o /app/out

# === STAGE 2: RUNTIME (CHẠY ỨNG DỤNG) ===
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy code từ giai đoạn build vào runtime
COPY --from=build /app/out ./

# Chạy ứng dụng
ENTRYPOINT ["dotnet", "highlands.dll"]
