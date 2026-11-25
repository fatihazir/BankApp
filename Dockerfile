# ---- build stage: the SDK image (it has the compiler and does NOT end up in the final image) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Project files first: the restore layer comes from the cache until a dependency changes
COPY global.json ./
COPY src/BankingSystemCore/BankingSystemCore.csproj src/BankingSystemCore/
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Persistence/Persistence.csproj src/Persistence/
COPY src/WebApi/WebApi.csproj src/WebApi/
RUN dotnet restore src/WebApi/WebApi.csproj
COPY src/ src/
RUN dotnet publish src/WebApi/WebApi.csproj -c Release -o /out --no-restore

# ---- runtime stage: the plain ASP.NET image, runs as the non-root "app" user ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
# Listens on 8080. Required at run time (not baked in): ConnectionStrings__BankAppDb, Security__Jwt__SecurityKey.
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "WebApi.dll"]
