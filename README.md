# README

## Sistema de reservas de espacios

* **Nombre:** Asael Abreu
* **Lenguaje/Framework:** C# / ASP.NET Core
* **Materia:** Programación III
* **Sección:** 2026-C-3

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) (`dotnet --version` debe mostrar 10.x)
- Git

## Cómo ejecutar el proyecto

1. Clonar el repositorio y entrar a la carpeta:

```powershell
git clone https://github.com/AsaelCodex/sistema-de-reservas-de-espacios.git
cd sistema-de-reservas-de-espacios
```

2. Restaurar las dependencias:

```powershell
dotnet restore SistemaDeReservas
```

3. Compilar:

```powershell
dotnet build SistemaDeReservas
```

4. Ejecutar:

```powershell
dotnet run --project SistemaDeReservas
```

El Resultado esperado deberia ser: la consola muestra `Hello, World!`. En esta etapa
el proyecto es la estructura inicial y todavía no tiene funcionalidad de reservas.