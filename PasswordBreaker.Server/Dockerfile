FROM mcr.microsoft.com/dotnet/sdk:10.0 AS final
WORKDIR /app

# Copy the entire solution for 'dotnet run' to work inside the container
COPY . .

# Build the server
WORKDIR "/app/PasswordBreaker.Server"
RUN dotnet build "PasswordBreaker.Server.csproj" -c Release -o /app/out

# Build the worker (optional, but speeds up first spawn)
WORKDIR "/app/PasswordBreaker.Worker"
RUN dotnet build "PasswordBreaker.Worker.csproj" -c Release

WORKDIR /app/out
EXPOSE 80
ENTRYPOINT ["dotnet", "PasswordBreaker.Server.dll"]
