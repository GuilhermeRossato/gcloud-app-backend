# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY HelloHttp.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Use the official .NET runtime image for running
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy the published app
COPY --from=build /app/publish .

# Expose port 8080 (Cloud Run default)
ENV PORT=8080
EXPOSE 8080

# Set the entry point
ENTRYPOINT ["dotnet", "HelloHttp.dll"]
