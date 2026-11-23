# Etapa 1: compila e publica com o SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PousadaMareMansa.Web.csproj .
RUN dotnet restore PousadaMareMansa.Web.csproj
COPY . .
RUN dotnet publish PousadaMareMansa.Web.csproj -c Release -o /app --no-restore

# Etapa 2: imagem final, só com o runtime (menor e mais segura)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "PousadaMareMansa.Web.dll"]
