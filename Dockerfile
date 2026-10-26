FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Project/Project.csproj", "Project/"]
RUN dotnet restore "Project/Project.csproj"

COPY . .
WORKDIR /src/Project
RUN dotnet publish "Project.csproj" --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["sh", "-c", "dotnet Project.dll --urls http://0.0.0.0:${PORT:-8080}"]
