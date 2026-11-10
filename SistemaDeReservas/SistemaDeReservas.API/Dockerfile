# syntax=docker/dockerfile:1

# P4-02 (RD-09, RD-10): imagen multi-etapa para la API .NET 10. Las
# credenciales no se copian en ninguna capa; se reciben en ejecución por
# variables de entorno.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /origen

# Restaurar solo los proyectos que necesita la API para aprovechar la caché.
COPY SistemaDeReservas/SistemaDeReservas.API/SistemaDeReservas.API.csproj SistemaDeReservas/SistemaDeReservas.API/
COPY SistemaDeReservas/SistemaDeReservas.Business/SistemaDeReservas.Business.csproj SistemaDeReservas/SistemaDeReservas.Business/
COPY SistemaDeReservas/SistemaDeReservas.Core/SistemaDeReservas.Core.csproj SistemaDeReservas/SistemaDeReservas.Core/
COPY SistemaDeReservas/SistemaDeReservas.Infrastructure/SistemaDeReservas.Infrastructure.csproj SistemaDeReservas/SistemaDeReservas.Infrastructure/
RUN dotnet restore SistemaDeReservas/SistemaDeReservas.API/SistemaDeReservas.API.csproj

COPY SistemaDeReservas/ SistemaDeReservas/
RUN dotnet publish SistemaDeReservas/SistemaDeReservas.API/SistemaDeReservas.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publicacion

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publicacion .

# Directorios que respaldan los volúmenes nombrados. Se crean con el usuario
# no root de la imagen para que Docker herede la propiedad al inicializar un
# volumen vacío (RF-DOC-05, persistencia de documentos y correos .eml).
RUN mkdir -p /app/documentos /app/correo-salida \
    && chown -R app:app /app/documentos /app/correo-salida

USER app

EXPOSE 8080

ENTRYPOINT ["dotnet", "SistemaDeReservas.API.dll"]
