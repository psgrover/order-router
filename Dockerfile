# ── Build stage ───────────────────────────────────────────────────────────────
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copy project file and restore using the specific architecture
# This stays cached unless the .csproj changes
COPY ["src/OrderRouter.Api/OrderRouter.Api.csproj", "OrderRouter.Api/"]
RUN dotnet restore "OrderRouter.Api/OrderRouter.Api.csproj" -a $TARGETARCH

# Copy the rest of the source code
# The .dockerignore ensures we DON'T copy local bin/obj folders here
COPY src/OrderRouter.Api/ OrderRouter.Api/

WORKDIR "/src/OrderRouter.Api"
RUN dotnet publish "OrderRouter.Api.csproj" \
    -c Release \
    -o /app/publish \
    -a $TARGETARCH \
    --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Expose port
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OrderRouter.Api.dll"]
