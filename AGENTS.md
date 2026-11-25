# AGENTS.md

## 1. Propósito del proyecto

Sistema de Reservas de Espacios es una aplicación académica de
Programación III (ITLA) para gestionar reservas de salones, canchas y
equipos por horario, evitando conflictos de disponibilidad. El proyecto
integra las piezas del Core —Control de Acceso, Gestión de Permisos,
Manejador de Documentos, Notificaciones/cola de correos, Reportes y
Auditoría— con un módulo de negocio propio de reservas.

Este archivo es el contexto operativo para cualquier agente. No sustituye
a los requisitos oficiales: describe la arquitectura, los comandos, las
convenciones y los límites que deben respetarse al modificar el repositorio.

## 2. Arquitectura

| Proyecto | Responsabilidad |
|---|---|
| `SistemaDeReservas.Core` | Entidades, enums y puertos (interfaces). No referencia otros proyectos ni conoce HTTP, EF Core ni SMTP. |
| `SistemaDeReservas.Business` | Servicios, políticas y reglas de negocio. Depende de Core. |
| `SistemaDeReservas.Infrastructure` | EF Core/SQL Server, repositorios, hash de contraseñas, almacenamiento de documentos, envío de correo y cliente de WeatherAPI. Implementa los puertos del Core. |
| `SistemaDeReservas.API` | Controladores, filtro de autorización, DTOs, composición de dependencias y `ColaCorreosWorker`. No contiene reglas de negocio. |
| `SistemaDeReservas.Tests` | Pruebas xUnit, fakes, pruebas HTTP con `WebApplicationFactory` y pruebas de persistencia con SQL Server LocalDB. |

Reglas estructurales:

- La dependencia va en un solo sentido: Business/Infrastructure → Core. El
  Core nunca depende del módulo de negocio (RD-03).
- La lógica no vive en Controllers ni en el filtro de autorización (RD-02).
  Un controlador valida la sesión, invoca un servicio, traduce el resultado a
  un código HTTP y proyecta la respuesta.
- Las transiciones de estado se resuelven en un único componente del Core:
  `TransicionesSolicitudPermiso` para permisos y `TransicionesReserva` para
  reservas (RD-04).
- La autorización se verifica siempre del lado del servidor (RD-06). Ocultar
  una operación en una interfaz no cuenta como control.
- Cada pieza debe poder probarse sin levantar la aplicación completa (RD-12).

## 3. Tecnología

- Lenguaje: **C#** (`net10.0`, nullable e implicit usings habilitados).
- SDK verificado: **.NET 10** (familia 10.0.4xx).
- Framework web: **ASP.NET Core** con controladores.
- Persistencia: **Entity Framework Core** sobre **SQL Server** (LocalDB para
  desarrollo y pruebas; SQL Server 2022 en Docker Compose).
- Pruebas: **xUnit**; `Microsoft.AspNetCore.Mvc.Testing` para HTTP real.
- Contenedores: **Docker Compose v2**.
- Control de versiones: **Git** y **GitHub**.
- No hay frontend en este repositorio. La API es el único producto ejecutable.

## 4. Documentación oficial y de diseño

La fuente de verdad de los requisitos es:

- `docs/requerimientos-core.md`

Documentos de apoyo por pieza:

- `README.md`: arranque, contenedor, API, cola de correos y pruebas.
- `docs/criterios-notificaciones-reportes.md`: criterios verificables de la
  Práctica 5 (RF-NOT-01 a RF-NOT-13 y RF-REP-01 a RF-REP-05).
- `docs/gestion-permisos.md`, `docs/contrato-negocio.md`,
  `docs/maquina-de-estados.md`.
- `docs/manejador-documentos.md`, `docs/api-externa.md`.
- `docs/matriz-requisitos.md` y los informes de cierre de prácticas.

Identificadores:

- `RD-XX`: requisitos de diseño transversales.
- `RF-CA-XX`: Control de Acceso.
- `RF-GP-XX`: Gestión de Permisos.
- `RF-DOC-XX`: Manejador de Documentos.
- `RF-NOT-XX`: Notificaciones y cola de correos.
- `RF-REP-XX`: Reportes con agregación.
- `RF-AUD-XX`: Auditoría.
- `RF-NEG-XX`: módulo de negocio.

Cuando una tarea mencione un identificador, leer el requisito y su criterio
de aceptación en `docs/requerimientos-core.md`. No inventar requisitos ni
cambiar su significado.

## 5. Etapa actual y alcance de la Práctica 5

Están implementadas y verificadas:

- Pieza 1 — Control de Acceso: RF-CA-01 a RF-CA-22.
- Pieza 2 — Gestión de Permisos: RF-GP-01 a RF-GP-07.
- Módulo de negocio: creación, consultas y máquina de estados de reservas
  (RF-NEG-02 a RF-NEG-06 y RF-NEG-09).
- Pieza 3 — Manejador de Documentos: RF-DOC-01 a RF-DOC-05.
- Cola de correos mínima: RF-NOT-08 a RF-NOT-13 (se completa y expone en la
  Práctica 5).
- API externa de evaluación climática con WeatherAPI (P4-03).

La Práctica 5 (semana 10) trabaja exclusivamente:

- **RF-NOT-01 a RF-NOT-13**: notificaciones internas, preferencia de correo y
  cola de correos completa y expuesta.
- **RF-REP-01 a RF-REP-05**: reporte del Core y reporte del negocio con
  agregación real, autorización por rol y filtro por rango de fechas.
- Contexto y criterios escritos en `AGENTS.md` y
  `docs/criterios-notificaciones-reportes.md` **antes** del primer commit del
  motor de notificaciones.
- Actualización del `README.md` con las secciones «Notificaciones» y
  «Reportes», y revisión acumulada de las prácticas anteriores.

Quedan fuera de alcance en esta práctica:

- RF-DOC-06 (auditoría de documentos) y toda la pieza de Auditoría
  (RF-AUD-XX).
- Frontend, interfaz gráfica o aplicación de escritorio.
- Cambios no relacionados en el módulo de negocio, Control de Acceso,
  Gestión de Permisos, Documentos o API climática.
- Reestructuraciones generales, nuevos patrones de arquitectura o
  dependencias externas que no sean imprescindibles.

El Bloque P5-01 es **solo documental**: no crear entidades, servicios,
endpoints, migraciones ni pruebas de las funcionalidades nuevas. El commit
que agrega este contexto y los criterios debe ser anterior al primer commit
de implementación de notificaciones.

## 6. Comandos exactos desde la raíz del repositorio

Todos los comandos se ejecutan desde la raíz
(`sistema-de-reservas-de-espacios`). En Windows se usa PowerShell.

### Restaurar y compilar

```powershell
dotnet restore SistemaDeReservas/SistemaDeReservas.sln
dotnet build SistemaDeReservas/SistemaDeReservas.sln
```

### Ejecutar la API con SQL Server LocalDB

```powershell
$env:DB_CONNECTION_STRING = "Server=(localdb)\MSSQLLocalDB;Database=SistemaDeReservas;Trusted_Connection=True;TrustServerCertificate=True"
dotnet ef database update `
  --project SistemaDeReservas/SistemaDeReservas.Infrastructure `
  --startup-project SistemaDeReservas/SistemaDeReservas.API
dotnet run --project SistemaDeReservas/SistemaDeReservas.API
```

El comando de migraciones requiere la herramienta
`dotnet tool install --global dotnet-ef`. La API queda en
`http://localhost:5137`. La cadena de conexión es obligatoria y se lee de
`DB_CONNECTION_STRING`; nunca se escribe en el repositorio.

### Ejecutar la suite de pruebas

```powershell
dotnet test SistemaDeReservas/SistemaDeReservas.sln
```

Filtros útiles:

```powershell
# Solo persistencia (requiere SQL Server LocalDB)
dotnet test SistemaDeReservas/SistemaDeReservas.sln --filter "FullyQualifiedName~Persistencia"

# Todo excepto persistencia
dotnet test SistemaDeReservas/SistemaDeReservas.sln --filter "FullyQualifiedName!~Persistencia"
```

Modo estricto, para exigir que las pruebas de persistencia se ejecuten:

```powershell
$env:REQUIERE_PERSISTENCIA = "1"
dotnet test SistemaDeReservas/SistemaDeReservas.sln
Remove-Item Env:REQUIERE_PERSISTENCIA
```

Sin LocalDB disponible, las pruebas de persistencia se omiten con el motivo;
con `REQUIERE_PERSISTENCIA=1` fallan por infraestructura, no por un defecto
del negocio. No ocultar ni reinterpretar esos fallos.

### Ejecutar con Docker Compose

Crear `.env` a partir del ejemplo es una acción del usuario:

```powershell
Copy-Item .env.example .env
docker compose up --build
docker compose logs -f api
docker compose down
```

`docker compose down` conserva los volúmenes nombrados; `docker compose down -v`
los elimina y es destructivo. Para una verificación aislada:

```powershell
docker compose -p reservas-verificacion up --build
docker compose -p reservas-verificacion down -v
```

El flujo completo con contenedor no requiere el SDK de .NET ni SQL Server
instalados en el host.

## 7. Convenciones reales del código

### Nombres y estructura

- Namespaces y proyectos: `SistemaDeReservas.NombreDeCapa` (`Core`,
  `Business`, `Infrastructure`, `API`, `Tests`).
- Carpetas de Business por pieza: `ControlAcceso`, `GestionPermisos`,
  `Documentos`, `Reservas`, `Notificaciones` (cola y notificaciones internas)
  y `Reportes` (Práctica 5), además de `Clima`.
- Tipos, propiedades y métodos en PascalCase; parámetros y variables locales
  en camelCase.
- Métodos asíncronos con sufijo `Async` y `CancellationToken` como último
  parámetro cuando aplica.
- Interfaces con prefijo `I` (`INotificacionRepository`, `IEnvioCorreo`).
- El dominio se nombra en español (`Usuario`, `Reserva`, `SolicitudPermiso`,
  `CorreoEnCola`, `Notificacion`); los DTOs de entrada terminan en `Request`.
- Las rutas son plurales y en kebab-case: `api/solicitudes-permisos`,
  `api/reservas`, `api/documentos`, `api/correos`.
- Los servicios devuelven resultados explícitos (`Resultado...` con un enum
  de estado) en lugar de lanzar excepciones para flujos esperados. Las
  excepciones se reservan para fallos de infraestructura.
- Los comentarios que explican una regla citan el requisito (`RF-NOT-01`,
  `RD-04`, etc.).

### Capas y dependencias

- Core define la entidad y el puerto; Business define el servicio; Infrastructure
  implementa el puerto; API compone y expone.
- No inyectar `AppDbContext` en Controllers.
- No repetir validaciones de estado ni de propietario en varios componentes.
- No cambiar contratos públicos existentes (nombres de propiedades JSON,
  rutas, códigos HTTP, firmas de servicios) salvo que el requisito lo exija.
  Los cambios deben ser aditivos y compatibles.

### Fechas y horas (RD-11)

- Los **instantes** (`CreadaEn`, `ResueltaEn`, `FechaCreacion`, `FechaEnvio`,
  `LeidaEn`) se persisten en **UTC** con `DateTime.UtcNow` y se comparan como
  instantes.
- Cada operación captura una sola marca de tiempo y la reutiliza en todos los
  registros que produce.
- `Reserva.Fecha` es una **fecha de calendario**, no un instante: se normaliza
  con `.Date`, se compara como fecha y **no se convierte entre zonas
  horarias**. `HoraInicio`/`HoraFin` son horas del mismo día.
- En reportes, `desde`/`hasta` son fechas inclusivas. Para instantes se usa
  `valor >= desde 00:00Z` y `valor < (hasta + 1 día) 00:00Z`; para
  `Reserva.Fecha` se usa `Fecha >= desde` y `Fecha <= hasta`, sin sumar días
  ni convertir zonas.
- `DateTime?` para fechas opcionales; `TimeSpan` para horarios del día.
- No usar `DateTime.Now` ni horas locales en la persistencia.

### Persistencia y migraciones

- EF Core con SQL Server. Las migraciones se aplican con `MigrateAsync` al
  arrancar; nunca `EnsureCreated`.
- No editar ni eliminar migraciones ya aplicadas. Todo cambio de esquema crea
  una migración nueva y se prueba sobre una base vacía.
- Las pruebas de persistencia crean su propia base `Reservas_Tests_*` y la
  eliminan al terminar.
- Los índices únicos y las relaciones se declaran en `AppDbContext`.
- No guardar archivos ni secretos en la base de datos.

### Pruebas

- Framework xUnit. Nombres `Metodo_Escenario_Resultado`.
- Cada prueba declara el requisito:
  `[Trait("Requerimiento", "RF-NOT-01")]` o `[Trait("Requerimiento", "RD-06")]`.
- Servicios: pruebas con fakes en la carpeta de la pieza
  (`SistemaDeReservas.Tests/Notificaciones`, `Reportes`, etc.).
- Controladores: invocación en memoria en `Tests/API`.
- Pipeline HTTP real: `WebApplicationFactory`/`TestServer` en `Tests/API`.
- Persistencia: `Tests/Persistencia` con `[LocalDbFact]`.
- No llamar a servicios externos reales (SMTP, WeatherAPI) en pruebas
  automatizadas. Usar fakes o transportes falsos.
- Una prueba que falla no se desactiva ni se debilita para “dejar la suite en
  verde”; se corrige el código o se documenta el defecto.

### Git

- Una rama por funcionalidad, por ejemplo:
  `feature/practica-5-contexto`, `feature/rf-not-01-notificaciones`,
  `feature/rf-rep-01-reporte-core`.
- Commits con una sola cosa, asunto imperativo, máximo 50 caracteres, sin
  mensajes genéricos como «cambios», «arreglos» o «final».
- No mezclar documentación, implementación y correcciones no relacionadas en
  un mismo commit.
- No hacer `push`, `merge` ni crear etiquetas sin instrucción explícita. La
  etiqueta `practica-5` se crea al cerrar la práctica.
- Los Pull Requests explican qué cambia, por qué, cómo probarlo y qué no
  incluye.

## 8. Seguridad y secretos

Reglas absolutas:

- **Nunca leer `.env`**, ni con `cat`, `Get-Content`, editores, scripts,
  pruebas o herramientas de búsqueda. Tampoco imprimir su contenido,
  rutas con credenciales ni variables de entorno sensibles.
- Se permite consultar `.env.example`, que solo contiene valores ficticios.
- Nunca escribir credenciales, contraseñas, claves de API, cadenas de
  conexión con contraseña, tokens ni secretos en código, pruebas,
  documentación, comentarios, logs, mensajes de error, commits o historial.
- Las credenciales se obtienen de variables de entorno o del mecanismo de
  configuración seguro: `DB_CONNECTION_STRING`, `CorreoSmtp__*`,
  `WeatherApi__ApiKey`, `AdministradorInicial__*`.
- No exponer contraseñas, hashes, tokens, claves, URLs internas, stack traces,
  rutas del sistema ni consultas SQL en respuestas o logs.
- No commitear `.env`; está ignorado por Git y excluido del contexto de
  construcción.
- Antes de entregar, revisar el diff y el historial en busca de secretos, sin
  imprimir valores reales.

## 9. Restricciones de cambio

Mientras se trabaja en la Práctica 5:

1. **Conservar los contratos existentes.** No romper endpoints, DTOs,
   códigos HTTP, nombres JSON, firmas públicas ni comportamientos ya
   verificados de Control de Acceso, Gestión de Permisos, Reservas,
   Documentos o API climática.
2. **Conservar la autorización.** Toda operación nueva declara
   `[RequiereRol(...)]` y valida la sesión con `SesionService` antes de
   invocar al negocio. El propietario y el rol salen solo de la sesión.
3. **Conservar las transiciones.** No duplicar ni alterar las tablas de
   `TransicionesSolicitudPermiso` y `TransicionesReserva`. Una transición
   rechazada no cambia el estado y no genera notificación.
4. **Conservar los documentos.** No cambiar el almacenamiento, la eliminación
   lógica, los límites ni la persistencia de RF-DOC-01 a RF-DOC-05.
5. **Conservar la API climática.** No modificar la evaluación con WeatherAPI,
   su autorización, sus umbrales ni su manejo de errores.
6. **Conservar las migraciones previas.** No editarlas ni eliminarlas; agregar
   una migración nueva para las tablas/columnas de la Práctica 5.
7. **Reutilizar la cola y el worker existentes.** `CorreoEnCola`,
   `IColaCorreosRepository`, `ProcesadorColaCorreos`, `ColaCorreosWorker`,
   `GET /api/correos` y el límite de 3 intentos se conservan y se extienden,
   no se reemplazan.
8. **No agregar frontend** ni una capa de presentación distinta de la API.
9. **No reestructurar** proyectos, namespaces o patrones existentes sin
   necesidad demostrable.
10. **No implementar fuera de alcance.** Si una tarea parece requerir una
    pieza posterior (Auditoría, RF-DOC-06, etc.), detenerse y preguntar.

## 10. Piezas ya implementadas que no deben romperse

### Control de Acceso

- Registro con correo único, activación por enlace, contraseña con hash,
  sesión con token, roles Administrador/Estándar, recuperación y cambio de
  contraseña, bloqueo por intentos fallidos y administración de usuarios.
- La autorización por rol vive en `AutorizacionRolFilter` y
  `DeclaracionOperaciones`; la validación de sesión en `SesionService`.

### Gestión de Permisos

- `POST /api/solicitudes-permisos`, `GET /api/solicitudes-permisos/mias`,
  `GET /api/solicitudes-permisos/pendientes`,
  `PUT /api/solicitudes-permisos/{id}/aprobar` y
  `PUT /api/solicitudes-permisos/{id}/rechazar`.
- `ResolucionSolicitudService` concentra la resolución y aplica el permiso en
  la misma operación. Una solicitud ya resuelta no se vuelve a resolver.
- `SolicitudPermiso` guarda `SolicitanteId`, `AprobadorId`, `ResueltaEn` y
  `MotivoRechazo`.

### Reservas

- `POST /api/reservas`, `GET /api/reservas/mias`, `GET /api/reservas`,
  `PUT /api/reservas/{id}/confirmar|rechazar|cancelar|finalizar` y
  `GET /api/reservas/{id}/evaluacion-climatica`.
- Estados: `Solicitada`, `Confirmada`, `Rechazada`, `Cancelada`,
  `Finalizada`. La tabla de transiciones es la única fuente de verdad.
- `Reserva` guarda `UsuarioId`, `Espacio`, `Fecha`, `HoraInicio`, `HoraFin`,
  `Estado` y `CreadaEn`.

### Documentos

- `POST /api/documentos`, `GET /api/documentos`,
  `GET /api/documentos/{id}/contenido` y `DELETE /api/documentos/{id}`.
- Eliminación lógica: la fila y el archivo físico se conservan.
- El propietario sale de la sesión; otro Estándar recibe rechazo.

### Cola de correos

- `CorreoEnCola`: destinatario, asunto, cuerpo, estado (`Pendiente`,
  `Enviado`, `Fallido`), intentos, fechas y último error.
- `ProcesadorColaCorreos.LimiteIntentos = 3` y
  `ProcesadorColaCorreos.TamanoLote = 20`.
- `Intentos` cuenta **fallos**: solo se incrementa cuando el envío lanza. Al
  tercer fallo el correo queda `Fallido`; como el procesador solo consulta
  `Pendiente`, no se reintenta una cuarta vez. Un envío exitoso conserva el
  número de fallos previos y limpia `UltimoError`.
- La no duplicación de RF-NOT-12 se garantiza para ejecuciones **secuenciales**
  del procesador; no cubre dos workers concurrentes ni una caída después del
  envío y antes de persistir `Enviado`.
- `ColaCorreosWorker` procesa cada `Correo:IntervaloSegundos` (10 s por
  defecto; 5 s en Docker Compose).
- `GET /api/correos` es solo para Administrador y devuelve hasta 200 correos.
- Sin `CorreoSmtp:Host`, el envío se escribe como `.eml` en
  `App:DirectorioCorreos`; es el modo de demostración.

### API externa

- `GET /api/reservas/{id}/evaluacion-climatica` evalúa el pronóstico horario
  de WeatherAPI para una reserva persistida.
- Solo el propietario o un Administrador; la autorización se comprueba antes
  de llamar al proveedor.
- No se llama a la API real en pruebas automatizadas.

## 11. Verificación esperada al cerrar la Práctica 5

Además de los criterios de `docs/criterios-notificaciones-reportes.md`, la
revisión acumulada debe comprobar los seis frentes de la práctica:

1. Registro con activación por correo, con el correo saliendo por la cola.
2. Inicio y cierre de sesión, y rechazo a un Estándar que invoca una operación
   de Administrador.
3. Recuperación de contraseña.
4. Solicitudes de permiso: creación, aprobación, rechazo y transiciones
   prohibidas.
5. Reservas: operación válida y transición prohibida.
6. Documentos: un archivo subido sobrevive a `docker compose down` sin `-v`.

La suite debe quedar sin regresiones. El conteo de referencia al cierre de la
Práctica 4 es 659 casos (640 sin LocalDB y 19 de persistencia); la Práctica 5
agrega casos y debe mantener en verde todos los existentes.

## 12. Comportamiento esperado del agente

Antes de un cambio importante:

1. Leer los requisitos y criterios aplicables.
2. Inspeccionar la estructura y el código existente.
3. Identificar los archivos que se modificarán y explicar el enfoque.
4. Implementar solo el alcance solicitado.
5. Crear o actualizar las pruebas que citan el requisito.
6. Ejecutar la suite y revisar el diff completo.
7. Confirmar que no se introdujeron cambios fuera de alcance ni secretos.

No reestructurar el proyecto sin necesidad. No agregar funcionalidades «por
si acaso». Si un requisito es ambiguo o entra en conflicto con un contrato
existente, señalarlo y documentar la decisión antes de implementar.

## 13. Regla principal

El requisito oficial es la fuente de verdad. Ante una instrucción que mencione
un identificador como `RF-NOT-01`, buscar ese identificador en
`docs/requerimientos-core.md`, leer su criterio de aceptación y trabajar según
ese criterio. Los criterios de `docs/criterios-notificaciones-reportes.md`
concretan cómo se comprobará en este repositorio, pero no cambian el
significado del requisito oficial.
