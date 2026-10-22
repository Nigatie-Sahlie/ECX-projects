# Build the API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DaycareApi.csproj ./
RUN dotnet restore DaycareApi.csproj

COPY . ./
RUN dotnet publish DaycareApi.csproj \
    --configuration Release \
    --output /app/publish 

# Run the published API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

EXPOSE 5000
ENTRYPOINT ["dotnet", "DaycareApi.dll"]