FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY services/ArchLens.Domain/ArchLens.Domain.csproj services/ArchLens.Domain/packages.lock.json services/ArchLens.Domain/
COPY services/ArchLens.Core/ArchLens.Core.csproj services/ArchLens.Core/packages.lock.json services/ArchLens.Core/
RUN dotnet restore services/ArchLens.Core/ArchLens.Core.csproj --locked-mode
COPY services/ services/
COPY reference-models/ reference-models/
RUN dotnet publish services/ArchLens.Core -c Release --no-restore -p:OpenApiGenerateDocuments=false -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:5080
EXPOSE 5080
USER app
ENTRYPOINT ["dotnet", "ArchLens.Core.dll"]
