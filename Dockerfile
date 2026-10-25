# syntax=docker/dockerfile:1

# Build on the host's architecture and cross-publish for the target (amd64 or arm64)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG TARGETARCH
WORKDIR /src

# Restore first so the package layer stays cached as long as no project file changes
COPY global.json Directory.Build.props ./
COPY src/Lernkicker.Domain/Lernkicker.Domain.csproj src/Lernkicker.Domain/
COPY src/Lernkicker.Application/Lernkicker.Application.csproj src/Lernkicker.Application/
COPY src/Lernkicker.QuizFormat/Lernkicker.QuizFormat.csproj src/Lernkicker.QuizFormat/
COPY src/Lernkicker.Persistence/Lernkicker.Persistence.csproj src/Lernkicker.Persistence/
COPY src/Lernkicker.Contracts/Lernkicker.Contracts.csproj src/Lernkicker.Contracts/
COPY src/Lernkicker.Client/Lernkicker.Client.csproj src/Lernkicker.Client/
COPY src/Lernkicker.Web/Lernkicker.Web.csproj src/Lernkicker.Web/
RUN dotnet restore src/Lernkicker.Web/Lernkicker.Web.csproj -a $TARGETARCH

COPY schema/ schema/
COPY src/ src/
RUN dotnet publish src/Lernkicker.Web/Lernkicker.Web.csproj -a $TARGETARCH --no-restore -c Release -o /app

# Chiseled: no shell, no package manager, runs as the non-root user "app" (uid 1654)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Lernkicker__DataPath=/data
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "Lernkicker.Web.dll"]
