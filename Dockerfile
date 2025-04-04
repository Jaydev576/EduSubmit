# ==============================
# Stage 1: Build the application
# ==============================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the project and build the app
COPY . ./
RUN dotnet publish -c Release -o /out --no-restore

# ==============================
# Stage 2: Run the application
# ==============================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy built application from build stage
COPY --from=build /out ./

# Set environment variables (Railway compatibility)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "EduSubmit.dll"]