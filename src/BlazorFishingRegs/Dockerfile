# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/BlazorFishingRegs/BlazorFishingRegs.csproj", "BlazorFishingRegs/"]
COPY ["src/BlazorFishingRegs.Core/BlazorFishingRegs.Core.csproj", "BlazorFishingRegs.Core/"]
COPY ["src/BlazorFishingRegs.Data/BlazorFishingRegs.Data.csproj", "BlazorFishingRegs.Data/"]

# Restore dependencies
RUN dotnet restore "BlazorFishingRegs/BlazorFishingRegs.csproj"

# Copy source code
COPY src/ .

# Build application
WORKDIR "/src/BlazorFishingRegs"
RUN dotnet build "BlazorFishingRegs.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "BlazorFishingRegs.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install dependencies for PDF processing
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libc6-dev \
        libgdiplus \
        libx11-dev \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r fishing && useradd -r -g fishing fishing

# Create directories
RUN mkdir -p /app/data /app/logs /app/temp \
    && chown -R fishing:fishing /app

# Copy published application
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R fishing:fishing /app

# Switch to non-root user
USER fishing

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Expose ports
EXPOSE 8080 8443

# Set entry point
ENTRYPOINT ["dotnet", "BlazorFishingRegs.dll"]
