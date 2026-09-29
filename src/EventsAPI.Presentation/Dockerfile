FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/EventsAPI.Presentation/EventsAPI.Presentation.csproj
RUN dotnet publish src/EventsAPI.Presentation/EventsAPI.Presentation.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventsAPI.Presentation.dll"]
