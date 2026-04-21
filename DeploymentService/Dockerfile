FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DeploymentService/DeploymentService.csproj", "DeploymentService/"]
RUN dotnet restore "DeploymentService/DeploymentService.csproj"
COPY . .
WORKDIR "/src/DeploymentService"
RUN dotnet build "DeploymentService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DeploymentService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DeploymentService.dll"]
