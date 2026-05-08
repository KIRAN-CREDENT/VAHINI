FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["Vahini.csproj", "./"]
RUN dotnet restore "Vahini.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "Vahini.csproj" -c Release -o /app/build
RUN dotnet publish "Vahini.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY --from=build /app/publish .
ENTRYPOINT ["sh", "-c", "dotnet Vahini.dll --urls http://0.0.0.0:${PORT:-80}"]
