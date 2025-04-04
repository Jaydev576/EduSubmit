# Stage 1: Build the app using .NET 9.0 SDK (Preview)
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /app

# Copy project and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and publish
COPY . ./
RUN dotnet publish -c Release -o out

# Stage 2: Run the app using ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview AS runtime
WORKDIR /app

COPY --from=build /app/out ./

# Use environment variable for port binding (Railway compatibility)
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}
EXPOSE 8080

ENTRYPOINT ["dotnet", "EduSubmit.dll"]