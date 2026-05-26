FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PasswordBreaker.Worker/PasswordBreaker.Worker.csproj", "PasswordBreaker.Worker/"]
COPY ["PasswordBreaker.Shared/PasswordBreaker.Shared.csproj", "PasswordBreaker.Shared/"]
RUN dotnet restore "PasswordBreaker.Worker/PasswordBreaker.Worker.csproj"

COPY . .
WORKDIR "/src/PasswordBreaker.Worker"
RUN dotnet build "PasswordBreaker.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PasswordBreaker.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PasswordBreaker.Worker.dll"]
