# Builds and runs the API alone (the tests/ project is excluded from this project's compile,
# so it is never built here). Used for a cloud deployment (see ../DEPLOYMENT.md); local
# development normally runs via `dotnet run`, not Docker.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY AppointmentSystem.API.csproj .
RUN dotnet restore AppointmentSystem.API.csproj

COPY . .
RUN dotnet publish AppointmentSystem.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Render (and most PaaS hosts) injects PORT at container start, not build time, so the
# actual bind address is set in Program.cs from that env var, not here.
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AppointmentSystem.API.dll"]
