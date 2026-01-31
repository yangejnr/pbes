FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PbesApi/PbesApi.csproj PbesApi/
RUN dotnet restore PbesApi/PbesApi.csproj

COPY PbesApi/ PbesApi/
WORKDIR /src/PbesApi
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PbesApi.dll"]
