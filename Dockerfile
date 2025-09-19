# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["EasyPatchy3.csproj", "./"]
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install HDiffPatch prebuilt binary
# Using the latest release v4.11.1 from GitHub
RUN apt-get update && \
    apt-get install -y wget unzip && \
    wget -q https://github.com/sisong/HDiffPatch/releases/download/v4.11.1/hdiffpatch_v4.11.1_bin_linux64.zip -O /tmp/hdiffpatch.zip && \
    unzip -q /tmp/hdiffpatch.zip -d /tmp/hdiffpatch && \
    find /tmp/hdiffpatch -name 'hdiffz*' -type f -exec mv {} /usr/local/bin/hdiffz \; && \
    find /tmp/hdiffpatch -name 'hpatchz*' -type f -exec mv {} /usr/local/bin/hpatchz \; && \
    chmod +x /usr/local/bin/hdiffz /usr/local/bin/hpatchz && \
    rm -rf /tmp/* && \
    apt-get remove -y wget unzip && \
    apt-get autoremove -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create storage directories
RUN mkdir -p /app/Storage/Versions /app/Storage/Patches

# Copy published app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "EasyPatchy3.dll"]