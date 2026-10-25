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
- **Pruebas:** 303, todas en verde: 294 unitarias con fakes (no necesitan
  base de datos) y 9 de persistencia sobre SQL Server LocalDB
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
  las 9 pruebas de persistencia (si no está disponible, esas pruebas se
  omiten con el motivo; ver «Pruebas»)
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

## Pruebas

Desde la raíz del repositorio, un único comando ejecuta toda la suite:

```powershell
dotnet test SistemaDeReservas/SistemaDeReservas.sln
```

Resultado actual (ejecutado en esta rama): **303/303 en verde** —
303 ejecutadas, **0 fallidas**, **0 omitidas**. Cada prueba declara el
requisito que verifica con `[Trait("Requerimiento", "RF-CA-XX")]`,
`[Trait("Requerimiento", "RF-GP-XX")]`,
`[Trait("Requerimiento", "RF-NOT-XX")]`,
`[Trait("Requerimiento", "RF-NEG-XX")]` o
`[Trait("Requerimiento", "RD-XX")]`.

294 pruebas son unitarias: no necesitan base de datos ni servidor
(usan fakes y un hasheador real de prueba). Las 9 de persistencia
(`SistemaDeReservas.Tests/Persistencia`, RD-09) sí requieren SQL Server
LocalDB:

```powershell
# solo las 9 pruebas de persistencia
dotnet test SistemaDeReservas/SistemaDeReservas.sln --filter "FullyQualifiedName~Persistencia"

# solo las 294 unitarias
dotnet test SistemaDeReservas/SistemaDeReservas.sln --filter "FullyQualifiedName!~Persistencia"
```

Cómo interpretar el resumen que imprime `dotnet test`:

- **Ejecutadas**: total de pruebas descubiertas (303).
- **Fallidas** (`Con error`): pruebas que no cumplieron su aserción; en
  una suite sana deben ser 0.
- **Omitidas**: pruebas que no corrieron. Aquí solo aparecen las 9 de
  persistencia cuando LocalDB no está disponible; con LocalDB disponible
  la ejecución es de 303 ejecutadas / 0 omitidas, y sin LocalDB queda en
  294 ejecutadas / 9 omitidas / 0 fallidas.

- Cada prueba de persistencia crea su propia base `Reservas_Tests_*` en
  LocalDB, aplica las migraciones sobre una base vacía y la elimina al
  terminar (también cuando la prueba falla); tras la corrida no queda
  ninguna base temporal.
- Sin LocalDB, esas 9 pruebas se **omiten** con el motivo. Para exigir
  su ejecución (p. ej. en CI), definir `REQUIERE_PERSISTENCIA=1`: la
  ausencia de LocalDB **falla** las pruebas con un mensaje claro en vez
  de omitirlas.
- `LOCALDB_PROBE_DATASOURCE` solo altera la sonda de disponibilidad
  (sirve para verificar los dos comportamientos anteriores); la creación
  de bases siempre usa `(localdb)\MSSQLLocalDB`.

## Defectos

La suite de **Gestión de permisos** (RF-GP-01 a RF-GP-07) fue validada
contra los seis defectos de la variante A de la Práctica 2: cada uno se
introdujo temporalmente sobre el código correcto, uno por uno, sin tocar
las pruebas, y la ejecución registró los fallos indicados. El código
correcto quedó restaurado y la suite volvió a 303/303. El detalle está en
`docs/practica-2-suite-permisos.md`.

| # | Defecto | Prueba que lo detecta | Comportamiento que comprueba |
|---|---|---|---|
| A1 | `AprobarAsync` cambia la condición «el estado es Pendiente» por «el estado no es Rechazada» | `ResolucionSolicitudServiceTests.Aprobar_FromApprovedRequest_IsRejectedAndKeepsFirstApproval` (y 4 más, incluida `PutAprobar_AppliedRequest_ReturnsConflict`) | Aprobar desde `Aprobada` o `Aplicada` se rechaza (`TransicionProhibida`) conservando estado, primer aprobador, fecha y permisos; no nace un segundo permiso. |
| A2 | Se agrega `Rechazada → Aplicada` a las transiciones permitidas | `TransicionesSolicitudPermisoTests.EsValida_WithCombinationOutsideTable_DeniesIt(Rechazada, Aplicada)` (junto con `Transicionar_WithCombinationOutsideTable…` y `ResolvedStates_HaveNoOutgoingTransitions`) | `Rechazada` es estado final: la combinación se rechaza y el estado no cambia. |
| A3 | Se elimina la verificación que impide resolver la propia solicitud | `ResolucionPropiaTests.Aprobar_OwnRequest_IsRejectedWithoutChanges` y `…Rechazar_OwnRequest_IsRejectedWithoutChanges` (más las dos pruebas de API que esperan 403) | Un Administrador no puede aprobar ni rechazar su propia solicitud: `ResolucionPropia` / 403, sin estado, motivo, fecha ni permisos nuevos. |
| A4 | En el listado propio se invierte «el solicitante es quien consulta» a «no es quien consulta» | `ListadoSolicitudesPersistenciaTests.ListarPropias_WithTwoApplicants_ReturnsOnlyOwnRequests` | Con dos solicitantes, el listado propio devuelve las 3 solicitudes del usuario y excluye las 2 ajenas (filtro en el repositorio EF real). |
| A5 | Se agrega `Aprobada → Pendiente` a las transiciones permitidas | `TransicionesSolicitudPermisoTests.EsValida_WithCombinationOutsideTable_DeniesIt(Aprobada, Pendiente)` (junto con `Transicionar_WithCombinationOutsideTable…`) | `Aprobada → Pendiente` se rechaza y conserva el estado. |
| A6 | Se elimina el guardado del motivo en la operación de rechazo | `ResolucionSolicitudServiceTests.Rechazar_WithMotivo_PersistsRejectionAndReason` (y 3 más, incluida la prueba de persistencia) | El rechazo válido guarda el motivo (recortado) junto al resolutor y la fecha; el inválido no resuelve la solicitud. |

Nota sobre A4: su mutación vive en `SolicitudPermisoRepository`, que solo
se ejercita con LocalDB; sin LocalDB esa prueba se omite y el defecto no
se detectaría.

## Documentación

- `docs/requerimientos-core.md`: requisitos oficiales del Core con sus
  identificadores (RD-XX, RF-CA-XX, RF-NOT-XX, etc.).
- `docs/matriz-requisitos.md`: estado de cada requisito y su evidencia.
- `docs/maquina-estados-negocio.md`: estados y transiciones de la
  reserva (RF-NEG-03 a RF-NEG-05).
- `docs/bitacora-asignación-1.md`: bitácora del proyecto.
- `docs/practica-2-suite-permisos.md`: Práctica 2 — suite de Gestión de
  permisos: relación RF-GP ↔ pruebas, defectos detectados y resultados.
- `AGENTS.md`: convenciones del proyecto (estructura, seguridad,
  commits y pull requests).
