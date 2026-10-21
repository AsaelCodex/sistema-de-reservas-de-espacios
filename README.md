# Sistema de reservas de espacios

* **Nombre:** Asael Abreu
* **Lenguaje/Framework:** C# / ASP.NET Core
* **Materia:** Programación III
* **Sección:** 2026-C-3

## Descripción

Sistema para gestionar la reserva de espacios y equipos (salones,
canchas, equipos) por horario, evitando conflictos de disponibilidad.

Actualmente el **Pieza 1 - Control de Acceso** está implementado y
verificado (RF-CA-01 a RF-CA-22): registro con correo único,
contraseñas almacenadas con hash, inicio y cierre de sesión, roles
Administrador y Estándar, autorización verificada del lado del servidor,
activación de cuenta por enlace, bloqueo por intentos fallidos, y
recuperación, cambio y restablecimiento de contraseñas. También está
implementada la **máquina de estados del negocio** (`Reserva`,
RF-NEG-03 a RF-NEG-05 y RF-NEG-09) sobre la que se construirá el
módulo de reservas.

## Arquitectura

| Proyecto | Responsabilidad |
|---|---|
| `SistemaDeReservas.Core` | Entidades y puertos (interfaces). Sin dependencias externas. |
| `SistemaDeReservas.Business` | Lógica de negocio (servicios y políticas de Control de Acceso). |
| `SistemaDeReservas.Infrastructure` | Persistencia con EF Core (SQL Server) e implementaciones de los puertos. |
| `SistemaDeReservas.API` | Controladores, autorización y composición (inyección de dependencias). |
| `SistemaDeReservas.Tests` | Pruebas automatizadas que referencian el requisito que verifican. |

## Estado actual

- **Verificado:** los 22 requisitos de Control de Acceso (RF-CA-01 a
  RF-CA-22) y la máquina de estados del negocio (RF-NEG-03, RF-NEG-04,
  RF-NEG-05 y RF-NEG-09). El detalle y la evidencia están en
  `docs/matriz-requisitos.md`.
- **Pruebas:** 268, todas en verde: 261 unitarias con fakes (no necesitan
  base de datos) y 7 de persistencia sobre SQL Server LocalDB
  (`SistemaDeReservas.Tests/Persistencia`), con una base propia por
  prueba que se elimina al terminar.
- **Endpoints:**

| Método y ruta | Descripción | Rol |
|---|---|---|
| `POST /api/usuarios` | Registro (409 si el correo existe) | Público |
| `GET /activar?token=` | Activación de cuenta por enlace | Público |
| `POST /activar/reenviar` | Reenvío del enlace (respuesta idéntica) | Público |
| `POST /api/sesiones` | Inicio de sesión (token de sesión) | Público |
| `DELETE /api/sesiones` | Cierre de sesión | Autenticado |
| `GET /api/usuarios/yo` | Usuario autenticado y su rol | Autenticado |
| `PUT /api/usuarios/yo/contrasena` | Cambio de contraseña propia | Autenticado |
| `POST /api/recuperacion-contrasena` | Inicio de recuperación por correo | Público |
| `POST /api/recuperacion-contrasena/cambio` | Restablecimiento con correo y código | Público |
| `GET /api/usuarios` | Listado de usuarios | Administrador |
| `PUT /api/usuarios/{id}/rol` | Cambio de rol | Administrador |
| `PUT /api/usuarios/{id}/estado` | Desactivar/reactivar | Administrador |
| `PUT /api/usuarios/{id}/contrasena` | Restablecimiento forzado (envía código al correo) | Administrador |
| `GET /api/correos` | Consulta de la cola de correos (estado, intentos, errores) | Administrador |
| `POST /api/solicitudes-permisos` | Solicitud de un permiso del catálogo | Autenticado |
| `GET /api/solicitudes-permisos/mias` | Solicitudes propias con su estado | Autenticado |
| `GET /api/solicitudes-permisos/pendientes` | Cola de solicitudes por resolver | Administrador |
| `PUT /api/solicitudes-permisos/{id}/aprobar` | Aprobar (aplica el permiso; 403 si es la propia) | Administrador |
| `PUT /api/solicitudes-permisos/{id}/rechazar` | Rechazar con motivo (400 si falta) | Administrador |

- **Pendiente (piezas posteriores):** Gestión de Permisos (RF-GP),
  Manejador de Documentos (RF-DOC), Notificaciones (RF-NOT-01 a
  RF-NOT-07; la cola de correos RF-NOT-08 a RF-NOT-13 ya está
  implementada), Reportes (RF-REP), Auditoría (RF-AUD) y el resto del
  módulo de negocio (RF-NEG, salvo su máquina de estados).

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) (`dotnet --version` debe mostrar 10.x)
- Git
- SQL Server (LocalDB, Express o superior) para ejecutar la API y para
  las 7 pruebas de persistencia (si no está disponible, esas pruebas se
  omiten con el motivo; ver «Cómo ejecutar las pruebas»)
- Entity Framework CLI, solo para aplicar migraciones:
  `dotnet tool install --global dotnet-ef`

## Cómo ejecutar el proyecto

1. Clonar el repositorio y entrar a la carpeta:

```powershell
git clone https://github.com/AsaelCodex/sistema-de-reservas-de-espacios.git
cd sistema-de-reservas-de-espacios
```

2. Restaurar y compilar:

```powershell
dotnet restore SistemaDeReservas/SistemaDeReservas.sln
dotnet build SistemaDeReservas/SistemaDeReservas.sln
```

3. Definir la cadena de conexión (obligatoria: la API no inicia sin
   ella y nunca se guarda en el repositorio, RD-10):

```powershell
$env:DB_CONNECTION_STRING = "Server=(localdb)\MSSQLLocalDB;Database=SistemaDeReservas;Trusted_Connection=True;TrustServerCertificate=True"
```

   La URL pública usada en los enlaces de activación del correo se
   toma de `App:PublicBaseUrl`; en desarrollo usa
   `http://localhost:5137` (`appsettings.Development.json`) y en
   producción debe definirse con la variable de entorno
   `App__PublicBaseUrl` (la API también falla al iniciar si falta).

4. Aplicar las migraciones:

```powershell
dotnet ef database update `
  --project SistemaDeReservas/SistemaDeReservas.Infrastructure `
  --startup-project SistemaDeReservas/SistemaDeReservas.API
```

5. Ejecutar:

```powershell
dotnet run --project SistemaDeReservas/SistemaDeReservas.API
```

La API queda disponible en `http://localhost:5137`.

## Cola de correos

Los correos (activación, recuperación y restablecimiento) se encolan en
la tabla `CorreosEnCola` y los envía un worker independiente
(`ColaCorreosWorker`) cada `Correo:IntervaloSegundos` (por defecto 10).
Cada correo queda registrado con estado (`Pendiente`, `Enviado`,
`Fallido`), intentos, fechas y último error; se reintenta hasta 3 veces
y luego se marca `Fallido`. Un correo `Enviado` nunca se vuelve a
procesar.

Configuración de envío (credenciales **solo** por variables de
entorno, RD-10 — nunca en el repositorio):

```powershell
$env:CorreoSmtp__Host = "smtp.ejemplo.com"
$env:CorreoSmtp__Puerto = "587"
$env:CorreoSmtp__Usuario = "usuario"
$env:CorreoSmtp__Password = "clave"
$env:CorreoSmtp__De = "no-responder@itla.edu.do"
$env:CorreoSmtp__Ssl = "true"
```

Si `CorreoSmtp:Host` no está definido, el worker escribe cada correo
como archivo `.eml` en el directorio `App:DirectorioCorreos`
(`correo-salida/` en desarrollo), para poder verlos en la demo sin
servidor de correo.

Comprobar el funcionamiento:

1. Registrar un usuario nuevo (`POST /api/usuarios`).
2. Verificar que aparece el archivo en `correo-salida/` (o, con SMTP
   configurado, el correo recibido), dentro de los ~10 segundos.
3. Consultar `GET /api/correos` como Administrador: el correo aparece
   con estado `Enviado`, `FechaEnvio` y `Intentos = 0`.

## Cómo ejecutar las pruebas

```powershell
dotnet test SistemaDeReservas/SistemaDeReservas.sln
```

Resultado esperado: **268/268** en verde. Cada prueba declara el
requisito que verifica con `[Trait("Requerimiento", "RF-CA-XX")]`,
`[Trait("Requerimiento", "RF-GP-XX")]`,
`[Trait("Requerimiento", "RF-NOT-XX")]`,
`[Trait("Requerimiento", "RF-NEG-XX")]` o
`[Trait("Requerimiento", "RD-XX")]`.

261 pruebas son unitarias: no necesitan base de datos ni servidor
(usan fakes y un hasheador real de prueba). Las 7 de persistencia
(`SistemaDeReservas.Tests/Persistencia`, RD-09) sí requieren SQL Server
LocalDB:

```powershell
# solo las 7 pruebas de persistencia
dotnet test SistemaDeReservas/SistemaDeReservas.sln --filter "FullyQualifiedName~Persistencia"

# solo las 261 unitarias
dotnet test SistemaDeReservas/SistemaDeReservas.sln --filter "FullyQualifiedName!~Persistencia"
```

- Cada prueba de persistencia crea su propia base `Reservas_Tests_*` en
  LocalDB, aplica las migraciones sobre una base vacía y la elimina al
  terminar (también cuando la prueba falla); tras la corrida no queda
  ninguna base temporal.
- Sin LocalDB, esas 7 pruebas se **omiten** con el motivo. Para exigir
  su ejecución (p. ej. en CI), definir `REQUIERE_PERSISTENCIA=1`: la
  ausencia de LocalDB **falla** las pruebas con un mensaje claro en vez
  de omitirlas.
- `LOCALDB_PROBE_DATASOURCE` solo altera la sonda de disponibilidad
  (sirve para verificar los dos comportamientos anteriores); la creación
  de bases siempre usa `(localdb)\MSSQLLocalDB`.

## Documentación

- `docs/requerimientos-core.md`: requisitos oficiales del Core con sus
  identificadores (RD-XX, RF-CA-XX, RF-NOT-XX, etc.).
- `docs/matriz-requisitos.md`: estado de cada requisito y su evidencia.
- `docs/maquina-estados-negocio.md`: estados y transiciones de la
  reserva (RF-NEG-03 a RF-NEG-05).
- `docs/bitacora-asignación-1.md`: bitácora del proyecto.
- `AGENTS.md`: convenciones del proyecto (estructura, seguridad,
  commits y pull requests).
