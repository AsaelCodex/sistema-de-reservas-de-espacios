# Sistema de reservas de espacios

* **Nombre:** Asael Abreu
* **Lenguaje/Framework:** C# / ASP.NET Core
* **Materia:** Programación III
* **Sección:** 2026-C-3

## Descripción

Sistema para gestionar la reserva de espacios y equipos (salones,
canchas, equipos) por horario, evitando conflictos de disponibilidad.

Actualmente la **Pieza 1 - Control de Acceso** está implementada y
verificada (RF-CA-01 a RF-CA-22): registro con correo único,
contraseñas almacenadas con hash, inicio y cierre de sesión, roles
Administrador y Estándar, autorización verificada del lado del servidor,
activación de cuenta por enlace, bloqueo por intentos fallidos, y
recuperación, cambio y restablecimiento de contraseñas. La **Pieza 2 -
Gestión de Permisos** (RF-GP-01 a RF-GP-07) también está implementada y
verificada. El **módulo de negocio** expone la creación, las consultas y
las transiciones de reservas mediante endpoints protegidos: máquina de
estados (RF-NEG-03 a RF-NEG-05 y RF-NEG-09), creación y consultas
(RF-NEG-02) y operaciones protegidas por Control de Acceso (RF-NEG-06). La
**Pieza 3 - Manejador de Documentos** (RF-DOC-01 a RF-DOC-05) está
implementada y verificada: subida, listado, descarga y eliminación lógica
con metadatos persistidos y almacenamiento fuera de `wwwroot`. RF-DOC-05 se
verifica con el volumen Docker nombrado `documentos`: el contenido sobrevive
a `docker compose down` sin `-v`. La **P4-03 - API externa** añade la
evaluación climática de una reserva mediante el pronóstico horario de
WeatherAPI, con autorización por propietario o Administrador, regla del
60 %, cobertura horaria completa y fallos controlados.

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
  RF-CA-22), los 7 de Gestión de Permisos (RF-GP-01 a RF-GP-07), la
  máquina de estados del negocio (RF-NEG-03, RF-NEG-04, RF-NEG-05 y
  RF-NEG-09), la creación y las consultas de reservas (RF-NEG-02), las
  operaciones del negocio protegidas por Control de Acceso (RF-NEG-06) y
  los cinco requisitos del Manejador de Documentos (RF-DOC-01 a RF-DOC-05).
  La evaluación climática de reservas (P4-03) también está implementada y
  verificada, con evidencia para RD-06, RD-07, RD-08, RD-09 y RD-12.
  En la Práctica 5 están implementadas y verificadas la bandeja de
  notificaciones propias (RF-NOT-04), el marcado como leída (RF-NOT-05), la
  persistencia y gestión de la preferencia de correo (RF-NOT-07), los eventos
  de permisos y reservas con entrega interna y encolado condicional de correo
  (RF-NOT-01, RF-NOT-02, RF-NOT-03, RF-NOT-06 y RF-NOT-07) y la cola de correos
  completa con configuración SMTP por variables de entorno, reintentos
  limitados a 3 fallos, no duplicación y consulta administrativa por listado e
  identificador (RF-NOT-08 a RF-NOT-13).
  El detalle y la evidencia están en `docs/matriz-requisitos.md`. La
  comprobación final desde un clon limpio (P4-04) está en
  `docs/practica-4-cierre.md`.
- **Pruebas:** 781 casos en verde (509 métodos de prueba, las teorías se
  expanden): 736 casos sin LocalDB —negocio con fakes, seguridad, correo,
  documentos, evaluación climática, notificaciones, controladores invocados
  en memoria, pruebas HTTP con `WebApplicationFactory` y el flujo real
  permisos–reservas con repositorios de prueba— y 45 casos de persistencia
  sobre SQL Server LocalDB (`SistemaDeReservas.Tests/Persistencia`), con una
  base propia por prueba que se elimina al terminar.
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
| `GET /api/notificaciones?leida=` | Notificaciones propias; `leida` opcional (`true` o `false`); orden de más reciente a más antigua | Autenticado |
| `PUT /api/notificaciones/{id}/leida` | Marca como leída una notificación propia (idempotente; 403 si es ajena, 404 si no existe) | Autenticado |
| `GET /api/usuarios/yo/notificaciones-correo` | Preferencia propia de correo de notificaciones (`{ "habilitado": true }`) | Autenticado |
| `PUT /api/usuarios/yo/notificaciones-correo` | Activa o desactiva la preferencia propia; exige `habilitado` booleano | Autenticado |
| `POST /api/recuperacion-contrasena` | Inicio de recuperación por correo | Público |
| `POST /api/recuperacion-contrasena/cambio` | Restablecimiento con correo y código | Público |
| `GET /api/usuarios` | Listado de usuarios | Administrador |
| `PUT /api/usuarios/{id}/rol` | Cambio de rol | Administrador |
| `PUT /api/usuarios/{id}/estado` | Desactivar/reactivar | Administrador |
| `PUT /api/usuarios/{id}/contrasena` | Restablecimiento forzado (envía código al correo) | Administrador |
| `GET /api/correos` | Consulta de la cola de correos (hasta los 200 más recientes; estado, intentos, fechas y último error, sin cuerpo) | Administrador |
| `GET /api/correos/{id}` | Consulta un correo específico de la cola por identificador (permite consultar correos antiguos fuera de los 200 más recientes, sin cuerpo; 404 si no existe) | Administrador |
| `POST /api/solicitudes-permisos` | Solicitud de un permiso del catálogo | Autenticado |
| `GET /api/solicitudes-permisos/mias` | Solicitudes propias con su estado | Autenticado |
| `GET /api/solicitudes-permisos/pendientes` | Cola de solicitudes por resolver | Administrador |
| `PUT /api/solicitudes-permisos/{id}/aprobar` | Aprobar (aplica el permiso; 403 si es la propia) | Administrador |
| `PUT /api/solicitudes-permisos/{id}/rechazar` | Rechazar con motivo (400 si falta) | Administrador |
| `POST /api/reservas` | Crear reserva (permiso `CrearReserva`; 409 si se solapa) | Autenticado |
| `GET /api/reservas/mias` | Reservas propias del usuario de la sesión | Autenticado |
| `GET /api/reservas` | Reservas para gestión | Administrador |
| `PUT /api/reservas/{id}/confirmar` | Confirmar (409 si la transición no procede) | Administrador |
| `PUT /api/reservas/{id}/rechazar` | Rechazar (409 si la transición no procede) | Administrador |
| `PUT /api/reservas/{id}/cancelar` | Cancelar (propietario o Administrador) | Autenticado |
| `PUT /api/reservas/{id}/finalizar` | Finalizar (solo desde `Confirmada`) | Administrador |
| `GET /api/reservas/{id}/evaluacion-climatica` | Evaluación climática de la reserva (propietario o Administrador; 422, 502, 503 o 504 según el caso) | Autenticado |
| `POST /api/documentos` | Subir un archivo (`multipart/form-data`, campo `archivo`; 413 si supera el límite) | Autenticado |
| `GET /api/documentos` | Listar documentos activos (propios; todos para Administrador) | Autenticado |
| `GET /api/documentos/{id}/contenido` | Descargar el contenido (403 si es de otro Estándar) | Autenticado |
| `DELETE /api/documentos/{id}` | Eliminación lógica (204; 403 si es de otro Estándar) | Autenticado |

- **Pendiente (piezas posteriores):** RF-DOC-06 (auditoría), Reportes
  (RF-REP), Auditoría (RF-AUD) y el resto del módulo de negocio (RF-NEG-01,
  RF-NEG-08 y RF-NEG-10).

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) (`dotnet --version` debe mostrar 10.x)
- Git
- SQL Server (LocalDB, Express o superior) para ejecutar la API y para
  las 45 pruebas de persistencia (si no está disponible, esas pruebas se
  omiten con el motivo; ver «Pruebas»)
- Entity Framework CLI, solo para aplicar migraciones:
  `dotnet tool install --global dotnet-ef`
- Docker con Compose v2, solo para el flujo en contenedor (sección
  «Contenedor»). Ese flujo no requiere .NET SDK ni SQL Server instalados.
- Acceso a Internet y una clave de WeatherAPI solo para la evaluación
  climática real (sección «API externa»). Las pruebas automatizadas no
  llaman a la API real ni necesitan clave.

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

## Contenedor (práctica 4, P4-02)

El flujo completo —API, SQL Server, migraciones, administrador inicial y
volúmenes— se puede levantar solo con Docker. No hace falta instalar el SDK
de .NET, SQL Server ni `dotnet-ef` en el equipo.

### Requisitos

- Docker Desktop (o Docker Engine) con Compose v2
  (`docker compose version`).
- Puerto `5137` libre en el host.

### Arranque desde una copia limpia

```powershell
Copy-Item .env.example .env
docker compose up --build
```

En Linux/macOS: `cp .env.example .env`. Copiar el ejemplo basta para esta
entrega; los valores son ficticios y solo sirven para la demostración local.

La API queda en `http://localhost:5137` (solo HTTP; se publica únicamente en
la interfaz de loopback `127.0.0.1` y el contenedor no usa certificados
locales ni redirecciones HTTPS). El primer arranque tarda hasta que SQL Server
acepta conexiones y la API aplica las migraciones; los registros muestran los
reintentos acotados.

Qué ocurre automáticamente:

1. SQL Server 2022 arranca con un volumen nombrado.
2. La API espera a que SQL Server responda (`healthcheck` de Compose y
   reintentos acotados en la aplicación).
3. Se aplican las migraciones existentes (nunca `EnsureCreated`). Un error
   definitivo impide el arranque y queda registrado.
4. Si `AdministradorInicial__Habilitado` es `true` y no existe ningún
   administrador, se crea la cuenta configurada. Los reinicios no modifican
   cuentas existentes ni sus contraseñas.
5. Recién entonces Kestrel atiende solicitudes y el worker de correos empieza
   a consultar la cola.

### Cuenta administradora inicial

Los valores de `.env.example` habilitan una cuenta de demostración:

| Variable | Valor de ejemplo |
|---|---|
| `ADMINISTRADOR_INICIAL_CORREO` | `admin@reservas.local` |
| `ADMINISTRADOR_INICIAL_CONTRASENA` | `Admin_Local_2026!Demo` |

Cambiar estos valores fuera de una demo local. En un despliegue real la
contraseña se define por entorno y nunca se versiona. Para no crear la cuenta:
`ADMINISTRADOR_INICIAL_HABILITADO=false`. El registro y la activación de
usuarios estándar siguen el flujo normal por correo.

### Datos persistentes

| Volumen | Ruta en el contenedor | Contenido |
|---|---|---|
| `sqlserver-datos` | `/var/opt/mssql` | Base de datos |
| `documentos` | `/app/documentos` | Archivos subidos (RF-DOC-05) |
| `correos` | `/app/correo-salida` | Correos `.eml` de demostración |

- `docker compose down` detiene y elimina los contenedores, pero **conserva**
  los volúmenes: la base, los documentos y los correos siguen disponibles al
  volver a levantar con `docker compose up`.
- `docker compose down -v` elimina también los volúmenes y, con ellos, la base,
  los documentos y los correos. Es una operación destructiva.
- `Documentos__DirectorioAlmacenamiento` apunta a `/app/documentos`, dentro del
  volumen y fuera de la imagen. En el host no queda una copia de los archivos.

### Consultar los correos `.eml`

Sin SMTP configurado, el worker escribe los correos en el volumen `correos`;
no quedan en la imagen ni en el host. Con la composición en marcha:

```powershell
docker compose exec api ls /app/correo-salida
docker compose exec api cat /app/correo-salida/<archivo>.eml
```

Para leer el correo más reciente desde PowerShell y extraer su enlace o
código (este es el bloque que usan los pasos de sesión, permisos y
recuperación):

```powershell
$nombre = (docker compose exec -T api sh -c "ls -1t /app/correo-salida | head -1").Trim()
$eml = (docker compose exec -T api sh -c "cat /app/correo-salida/$nombre") -join "`n"

# Enlace de activación
$activacion = [regex]::Match($eml, 'activar\?token=([A-Za-z0-9_\-]+)').Groups[1].Value

# Código de recuperación (6 dígitos)
$codigo = [regex]::Match($eml, '\b\d{6}\b').Value
```

Con `dotnet run` (sin contenedor) el mismo correo está en
`SistemaDeReservas/SistemaDeReservas.API/correo-salida/`; en ese caso se lee
con `Get-Content -Raw` sobre el `.eml` más reciente. También se puede
consultar la cola con `GET /api/correos` usando una sesión de Administrador.

### Registros y parada

```powershell
docker compose logs -f api
docker compose down
```

La composición se llama `sistema-reservas` y sus volúmenes llevan ese prefijo.
Para una verificación aislada sin tocar los volúmenes habituales, usar un
nombre de proyecto distinto, por ejemplo
`docker compose -p reservas-verificacion up --build`.

## Sesión, permisos y reservas (práctica 3)

Los ejemplos usan PowerShell y `curl.exe`. Windows PowerShell 5.1 no pasa las
comillas dobles incrustadas en un argumento a un programa nativo: con
`-d '{"correo":"..."}'` el cuerpo llega sin comillas y la API responde `400`
(el campo `contraseña` exige además UTF-8). Por eso cada cuerpo JSON se escribe
en un archivo UTF-8 **sin BOM** y se envía con
`curl.exe --data-binary "@archivo"`. Definir una vez el auxiliar:

```powershell
$base = "http://localhost:5137"

# Escribe el cuerpo JSON en un archivo UTF-8 sin BOM y lo envía con
# curl.exe --data-binary, sin que PowerShell altere las comillas.
function Enviar-Json {
  param(
    [Parameter(Mandatory)] [string] $Url,
    [Parameter(Mandatory)] [string] $Cuerpo,
    [string] $Metodo = "POST",
    [string] $Token
  )
  $ruta = Join-Path $env:TEMP ("cuerpo-" + [guid]::NewGuid().ToString("N") + ".json")
  [IO.File]::WriteAllText($ruta, $Cuerpo, (New-Object System.Text.UTF8Encoding $false))
  $argumentos = @("-s", "-X", $Metodo, $Url, "-H", "Content-Type: application/json",
    "--data-binary", "@$ruta")
  if ($Token) { $argumentos += @("-H", "Authorization: $Token") }
  & curl.exe @argumentos
  Remove-Item $ruta -Force
}
```

El auxiliar funciona igual en PowerShell 7. Los ejemplos posteriores escriben
el cuerpo JSON tal cual, con `contraseña` incluida.

### 1. Sesión de Estándar

Registrar la cuenta (nace inactiva) y activarla con el enlace que llega a la
cola de correos:

```powershell
Enviar-Json "$base/api/usuarios" '{"nombre":"Estandar Demo","correo":"estandar@itla.edu.do","contraseña":"Secreta123!"}'

# Último .eml del contenedor (ver «Consultar los correos .eml»).
$nombre = (docker compose exec -T api sh -c "ls -1t /app/correo-salida | head -1").Trim()
$eml = (docker compose exec -T api sh -c "cat /app/correo-salida/$nombre") -join "`n"
$activacion = [regex]::Match($eml, 'activar\?token=([A-Za-z0-9_\-]+)').Groups[1].Value
curl.exe -s "$base/activar?token=$activacion"
```

Iniciar sesión y guardar el token:

```powershell
$respuesta = Enviar-Json "$base/api/sesiones" '{"correo":"estandar@itla.edu.do","contraseña":"Secreta123!"}' | ConvertFrom-Json
$std = "Bearer $($respuesta.token)"
```

### 2. Sesión de Administrador

**Con el contenedor:** el administrador inicial se crea automáticamente al
arrancar si no existe ninguno (ver «Contenedor»), con la cuenta configurada en
`.env.example`. Solo hay que iniciar sesión con esa cuenta, guardar el token
en `$adm` y comprobar el rol; no se registra, activa ni promueve nada a mano:

```powershell
$respuesta = Enviar-Json "$base/api/sesiones" '{"correo":"admin@reservas.local","contraseña":"Admin_Local_2026!Demo"}' | ConvertFrom-Json
$adm = "Bearer $($respuesta.token)"

curl.exe -s "$base/api/usuarios/yo" -H "Authorization: $adm"
# 200: {"id":"...","nombre":"Administrador Inicial","correo":"admin@reservas.local","rol":"Administrador"}
```

**Ejecución local sin administrador inicial** (sin
`AdministradorInicial__Habilitado`): el registro (`POST /api/usuarios`) siempre
crea un Estándar inactivo y **no existe otro endpoint para el primer
Administrador**. Se registra y activa la cuenta igual que en el paso anterior,
leyendo el `.eml` local con `Get-Content` (sin comandos Docker):

```powershell
Enviar-Json "$base/api/usuarios" '{"nombre":"Admin Demo","correo":"admin@itla.edu.do","contraseña":"Secreta123!"}'

$eml = Get-ChildItem SistemaDeReservas/SistemaDeReservas.API/correo-salida/*.eml |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
$activacion = [regex]::Match((Get-Content -Raw $eml), 'activar\?token=([A-Za-z0-9_\-]+)').Groups[1].Value
curl.exe -s "$base/activar?token=$activacion"
```

Promover **exclusivamente esa cuenta** con un `UPDATE` por su correo único
(`IX_Usuarios_Correo`) y comprobar que exactamente una fila cambió. `sqlcmd
-b` devuelve código de error si la comprobación falla:

```powershell
$sql = "SET NOCOUNT OFF; UPDATE Usuarios SET Rol = 0 WHERE Correo = N'admin@itla.edu.do'; IF @@ROWCOUNT <> 1 THROW 50001, 'Se esperaba promover exactamente una cuenta.', 1;"
sqlcmd -S "(localdb)\MSSQLLocalDB" -d SistemaDeReservas -b -Q $sql
if ($LASTEXITCODE -ne 0) { throw "La promoción no afectó exactamente una cuenta." }
```

`Rol = 0` corresponde a `Administrador`. No debe ejecutarse nunca un `UPDATE`
sin `WHERE`: la condición limita el cambio a esa cuenta y la comprobación de
`@@ROWCOUNT` confirma que fue exactamente una.

Después, iniciar una **sesión nueva** con esa cuenta, guardar su token en
`$adm` y comprobar el rol:

```powershell
$respuesta = Enviar-Json "$base/api/sesiones" '{"correo":"admin@itla.edu.do","contraseña":"Secreta123!"}' | ConvertFrom-Json
$adm = "Bearer $($respuesta.token)"

curl.exe -s "$base/api/usuarios/yo" -H "Authorization: $adm"
# 200: {"id":"...","nombre":"Admin Demo","correo":"admin@itla.edu.do","rol":"Administrador"}
```

`docs/gestion-permisos.md` §5 registra este procedimiento. El seeder
idempotente de dos administradores sigue siendo una **propuesta futura**: el
código actual no lo implementa.

### 3. Solicitar y aprobar el permiso

Un Estándar necesita `Permiso.CrearReserva` para crear reservas. La solicitud
nace `Pendiente` y la resuelve un Administrador:

```powershell
# 201: {"id":"...","permiso":"CrearReserva","estado":"Pendiente",...}
Enviar-Json "$base/api/solicitudes-permisos" '{"permiso":"CrearReserva"}' -Token $std

# 200: la solicitud queda Aplicada y el permiso se concede
curl.exe -s -X PUT "$base/api/solicitudes-permisos/<id-solicitud>/aprobar" `
  -H "Authorization: $adm"
```

Mientras la solicitud siga `Pendiente`, `POST /api/reservas` responde 403 y
no inserta nada. Aprobar dos veces la misma solicitud responde 409.

### 4. Crear y consultar reservas

```powershell
# 201: reserva creada en estado Solicitada
Enviar-Json "$base/api/reservas" '{"espacio":"SALON A","fecha":"2027-01-15T00:00:00","horaInicio":"10:00:00","horaFin":"12:00:00"}' -Token $std

# 200: solo las reservas del usuario de la sesión
curl.exe -s "$base/api/reservas/mias" -H "Authorization: $std"

# 200: todas las reservas (Administrador)
curl.exe -s "$base/api/reservas" -H "Authorization: $adm"
```

Respuesta de una reserva:

```json
{
  "id": "6f1c...",
  "usuarioId": "0a2b...",
  "espacio": "SALON A",
  "fecha": "2027-01-15T00:00:00",
  "horaInicio": "10:00:00",
  "horaFin": "12:00:00",
  "estado": "Solicitada",
  "creadaEn": "2026-11-01T14:00:00Z"
}
```

El cliente solo envía `espacio`, `fecha`, `horaInicio` y `horaFin`. El
propietario, el rol, el estado inicial y `creadaEn` los fija el servidor a
partir de la sesión; enviarlos en el cuerpo no cambia nada.

### 5. Transiciones

```powershell
curl.exe -s -X PUT "$base/api/reservas/<id>/confirmar" -H "Authorization: $adm"
curl.exe -s -X PUT "$base/api/reservas/<id>/rechazar"  -H "Authorization: $adm"
curl.exe -s -X PUT "$base/api/reservas/<id>/cancelar"  -H "Authorization: $std"   # propietario
curl.exe -s -X PUT "$base/api/reservas/<id>/finalizar" -H "Authorization: $adm"   # desde Confirmada
```

| Operación | Respuesta esperada |
|---|---|
| `POST /api/reservas` | 201 con la reserva en estado `Solicitada`. |
| `GET /api/reservas/mias` | 200 con un arreglo de las reservas del usuario de la sesión. |
| `GET /api/reservas` | 200 con un arreglo de todas las reservas. |
| `PUT .../confirmar` | 200 con `estado: "Confirmada"`. |
| `PUT .../rechazar` | 200 con `estado: "Rechazada"`. |
| `PUT .../cancelar` | 200 con `estado: "Cancelada"`. |
| `PUT .../finalizar` | 200 con `estado: "Finalizada"`. |

### 6. Restricciones y códigos HTTP

| Operación | Estándar propietario | Estándar ajeno | Administrador |
|---|---|---|---|
| `GET /api/reservas` | 403 | 403 | 200 |
| `PUT .../confirmar` | 403 | 403 | 200 si la transición procede |
| `PUT .../rechazar` | 403 | 403 | 200 si la transición procede |
| `PUT .../finalizar` | 403 | 403 | 200 desde `Confirmada` |
| `PUT .../cancelar` | 200 | 403 | 200 |
| `GET /api/reservas/mias` | 200 solo las propias | 200 solo las propias | 200 solo las propias |

| Código | Significado |
|---|---|
| 201 | Reserva creada. |
| 200 | Consulta o transición exitosa. |
| 400 | Entrada inválida (por ejemplo, `horaFin` anterior a `horaInicio`). |
| 401 | Sesión ausente, desconocida, vencida o cerrada. |
| 403 | Rol insuficiente, permiso `CrearReserva` ausente o condición de propietario incumplida. |
| 404 | Reserva inexistente. |
| 409 | Solapamiento de horario o transición prohibida. |
| 503 | No se pudo adquirir el bloqueo de aplicación de SQL Server. |

El 503 no se provoca con una petición normal: requiere que `sp_getapplock`
falle. Está cubierto por la prueba automatizada
`ReservasControllerTests.Post_WhenLockIsNotAcquired_ReturnsServiceUnavailable`.

## Documentos (práctica 4)

El manejador de documentos permite subir, listar, descargar y eliminar
lógicamente archivos. Los metadatos se guardan en la tabla `Documentos`
(migración `AddDocumentos`) y el contenido se escribe fuera de `wwwroot`;
la descarga solo se sirve por el endpoint protegido, nunca como archivo
estático.

Todas las operaciones requieren una sesión válida. Un usuario Estándar solo
gestiona sus propios documentos; un Administrador puede gestionar los de
otros usuarios. La eliminación es **lógica**: el documento deja de listarse
y descargarse, pero la fila y el archivo físico se conservan.

### Configuración

```json
{
  "Documentos": {
    "DirectorioAlmacenamiento": "documentos-almacenados",
    "TamanioMaximoBytes": 10485760,
    "MargenSolicitudBytes": 1048576
  }
}
```

Se puede sobrescribir por entorno:

```powershell
$env:Documentos__DirectorioAlmacenamiento = "C:\datos\documentos"
$env:Documentos__TamanioMaximoBytes = "5242880"
$env:Documentos__MargenSolicitudBytes = "1048576"
```

- La ruta relativa se resuelve contra el directorio de contenido de la API.
- En el contenedor, `Documentos__DirectorioAlmacenamiento` vale
  `/app/documentos`, respaldado por el volumen nombrado `documentos`
  (sección «Contenedor»).
- La API rechaza al iniciar una ruta dentro de `wwwroot`.
- El límite por defecto del archivo es 10 MiB. El límite de la solicitud
  multipart es `TamanioMaximoBytes + MargenSolicitudBytes` (1 MiB de margen por
  defecto), de modo que los delimitadores y cabeceras no rechacen un archivo
  válido cercano al máximo.
- El almacenamiento cuenta los bytes realmente leídos, corta al superar el
  límite y guarda ese tamaño real, incluso con flujos no posicionables.
- El nombre físico lo genera el servidor (32 caracteres hexadecimales); el
  nombre original solo se guarda como metadato.

### Comprobación manual

Con la API en marcha y una sesión de Estándar en `$std`:

```powershell
# 201: metadatos del documento (no incluye el identificador interno)
curl.exe -s -X POST "$base/api/documentos" `
  -H "Authorization: $std" -F "archivo=@C:\ruta\acta.pdf"

# 200: solo los documentos activos del usuario de la sesión
curl.exe -s "$base/api/documentos" -H "Authorization: $std"

# 200: descarga por el identificador del documento
curl.exe -s "$base/api/documentos/<id>/contenido" `
  -H "Authorization: $std" -o acta.pdf

# 204: eliminación lógica
curl.exe -s -X DELETE "$base/api/documentos/<id>" -H "Authorization: $std"
```

Tras eliminar, el listado ya no muestra el documento, la descarga responde
`404`, la fila sigue en `Documentos` con `Eliminado = 1` y el archivo físico
sigue en el directorio configurado.

| Código | Significado |
|---|---|
| 201 | Documento subido y metadatos persistidos. |
| 200 | Listado o descarga correctos. |
| 204 | Eliminación lógica completada. |
| 400 | Archivo ausente, vacío, nombre con ruta, tipo/tamaño inválido. |
| 401 | Sesión ausente, desconocida, vencida o cerrada. |
| 403 | Otro Estándar intenta descargar o eliminar un documento ajeno. |
| 404 | Documento inexistente, eliminado o contenido físico no disponible. |
| 413 | El archivo supera `Documentos:TamanioMaximoBytes`. |
| 500 | Fallo controlado de persistencia; la subida compensa el archivo escrito. |
| 503 | El almacenamiento físico no pudo escribir el archivo. |

El detalle de la pieza está en `docs/manejador-documentos.md`.

## API externa — evaluación climática (práctica 4, P4-03)

`GET /api/reservas/{id}/evaluacion-climatica` consulta el pronóstico
**horario** de WeatherAPI para la fecha y el horario de una reserva
persistida. No modifica la reserva ni sus transiciones.

- Solo el **propietario** o un **Administrador** pueden evaluarla; la
  autorización se comprueba antes de consultar al proveedor.
- Se evalúan las horas completas que intersectan
  `[HoraInicio, HoraFin)` y se usa `max(chance_of_rain)`.
- El umbral inicial es **60 %** y es configurable entre 0 y 100. Si se
  alcanza se advierte y se sugiere un espacio cubierto; si no, no se
  promete ausencia de lluvia.
- Se rechazan fechas pasadas, horarios ya iniciados, fechas fuera de las
  devueltas por el proveedor y cobertura incompleta.
- La ubicación es única para todos los espacios y no se deduce del
  nombre: `ITLA, La Caleta, Santo Domingo Este`
  (`18.4512, -69.6626`, `America/Santo_Domingo`).

### Configuración

```json
{
  "Clima": {
    "Ubicacion": {
      "Nombre": "ITLA, La Caleta, Santo Domingo Este",
      "Latitud": 18.4512,
      "Longitud": -69.6626,
      "ZonaHoraria": "America/Santo_Domingo"
    },
    "UmbralProbabilidadLluvia": 60,
    "TimeoutSegundos": 10,
    "DiasPronostico": 3
  },
  "WeatherApi": { "ApiKey": "" }
}
```

La clave se define por entorno (RD-10):

```powershell
$env:WeatherApi__ApiKey = "<su-clave-de-weatherapi>"
```

En el contenedor, definir `WeatherApi__ApiKey` en `.env`; Compose la
propaga al servicio. `.env.example` solo contiene un marcador ficticio.
Si la clave falta, la aplicación **arranca igual** y solo esta operación
responde `503`.

### Ejemplo real

Verificación del 2026-11-10 con una reserva para el 2026-11-11 de
10:00 a 12:00, en un proyecto Compose aislado y con una clave real
(no incluida):

```powershell
curl.exe -s "$base/api/reservas/<id>/evaluacion-climatica" `
  -H "Authorization: Bearer $token"
```

Respuesta real (`200`):

```json
{
  "reservaId": "84d11dff-2e23-444d-b2fa-baddf42b1de7",
  "fecha": "2026-11-11",
  "horaInicio": "10:00:00",
  "horaFin": "12:00:00",
  "zonaHoraria": "America/Santo_Domingo",
  "ubicacion": {
    "nombre": "ITLA, La Caleta, Santo Domingo Este",
    "latitud": 18.4512,
    "longitud": -69.6626
  },
  "probabilidadMaximaLluvia": 19,
  "umbralProbabilidadLluvia": 60,
  "advertenciaLluvia": false,
  "horasEvaluadas": [
    { "hora": "10:00:00", "probabilidadLluvia": 19 },
    { "hora": "11:00:00", "probabilidadLluvia": 14 }
  ],
  "orientacion": "Orientación para actividades al aire libre.",
  "mensaje": "No se alcanza el umbral configurado de advertencia de lluvia. Esto no garantiza ausencia de lluvia; es una orientación para actividades al aire libre."
}
```

Los valores concretos cambian con el pronóstico; lo verificado es la
forma de la respuesta y la regla aplicada.

### Códigos

| Código | Significado |
|---|---|
| 200 | Evaluación calculada con cobertura completa. |
| 400 | Datos de la reserva inválidos para evaluar. |
| 401 | Sesión ausente, desconocida, vencida o cerrada. |
| 403 | Otro Estándar intenta evaluar una reserva ajena. |
| 404 | Reserva inexistente. |
| 422 | Fecha pasada, horario iniciado, fuera de horizonte o cobertura incompleta. |
| 502 | Error remoto o respuesta inválida del proveedor. |
| 503 | Clave ausente o inválida, cuota agotada o proveedor no disponible. |
| 504 | Tiempo de espera agotado. |

### Real vs. simulado

- **Real:** la verificación manual anterior usó WeatherAPI, una clave
  real y red; también se comprobó una clave ficticia (`503`) y la
  ausencia de clave (`503`), con login, reservas y documentos en `200`.
- **Simulado:** las pruebas automatizadas usan un `HttpMessageHandler`
  falso y un reloj fijo; no llaman a la API real ni necesitan clave.

El detalle completo está en `docs/api-externa.md`.

## Notificaciones (práctica 5)

La bandeja interna y la preferencia de correo se persisten en la tabla
`Notificaciones` y en la columna `Usuarios.RecibirCorreosNotificacion`
(migración `AddNotificacionesYPreferenciaCorreo`). La preferencia parte en
`true` para usuarios nuevos y existentes.

Cada evento de negocio genera una **notificación interna** para su
destinatario (o para cada Administrador activo, al crear una solicitud) y,
solo si la preferencia persistida de ese usuario está activa, un
`CorreoEnCola` en estado `Pendiente`. La notificación y el correo se
guardan en la **misma transacción** que la operación de origen: si una parte
falla, no queda ni la solicitud, ni el cambio de estado, ni la entrega
parcial. El negocio nunca envía correo directamente; el worker existente
(`ColaCorreosWorker`) es el único que llama a `IEnvioCorreo`.

Las pruebas de atomicidad usan SQL Server LocalDB y un contexto de prueba con
`MaxBatchSize(1)`: EF emite un comando por escritura, el interceptor registra
las que ya se ejecutaron dentro de la transacción y fuerza el fallo en la
última escritura de la operación. Después, un contexto nuevo comprueba que no
sobrevivió ninguna fila (solicitud, estado, permiso, notificación o correo).
No se modifica la configuración de producción para estas pruebas.

| Evento | Destinatario | Tipo interno |
|---|---|---|
| Crear una solicitud de permiso | Cada Administrador **activo** (uno por cada uno; cero si no hay) | `SolicitudPermisoCreada` |
| Aprobar una solicitud | Solo el solicitante | `SolicitudPermisoAprobada` |
| Rechazar una solicitud | Solo el solicitante, con el `MotivoRechazo` persistido | `SolicitudPermisoRechazada` |
| Confirmar una reserva (`Solicitada → Confirmada`) | Solo el propietario | `ReservaConfirmada` |

Las transiciones rechazadas, la resolución propia, el motivo ausente y las
demás transiciones de reserva **no** generan notificación ni correo. Repetir
una operación ya resuelta tampoco duplica entregas.

### Eventos y canales: comprobación manual

Con la API en marcha y las sesiones `$std` (Estándar) y `$adm`
(Administrador) definidas como en «Sesión, permisos y reservas»:

**1. Crear una solicitud (notifica a los Administradores activos).**

```powershell
Enviar-Json "$base/api/solicitudes-permisos" '{"permiso":"CrearReserva"}' -Token $std
# 201: {"id":"...","permiso":"CrearReserva","estado":"Pendiente",...}

# Bandeja del Administrador: una notificación interna por cada admin activo.
curl.exe -s "$base/api/notificaciones" -H "Authorization: $adm"
# 200: [{"id":"...","tipo":"SolicitudPermisoCreada","mensaje":"Nueva solicitud ...","leida":false,...}]

# Canal de correo: si la preferencia del admin está activa, hay un .eml nuevo.
Get-ChildItem SistemaDeReservas/SistemaDeReservas.API/correo-salida/*.eml |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
```

**2. Aprobar (notifica solo al solicitante).**

```powershell
$id = "<id de la solicitud>"
curl.exe -s -X PUT "$base/api/solicitudes-permisos/$id/aprobar" -H "Authorization: $adm"
# 200: la solicitud queda Aplicada y el permiso se concede

curl.exe -s "$base/api/notificaciones" -H "Authorization: $std"
# 200: [{"tipo":"SolicitudPermisoAprobada","mensaje":"Tu solicitud ... fue aprobada ..."}]
```

**3. Rechazar (notifica solo al solicitante con el motivo).**

```powershell
Enviar-Json "$base/api/solicitudes-permisos/$id/rechazar" '{"motivo":"No cumple los requisitos"}' -Metodo PUT -Token $adm
# 200: la solicitud queda Rechazada y el motivo se guarda recortado

curl.exe -s "$base/api/notificaciones" -H "Authorization: $std"
# 200: [{"tipo":"SolicitudPermisoRechazada","mensaje":"... Motivo: No cumple los requisitos"}]
```

El mensaje y el correo usan exactamente el valor persistido en
`SolicitudPermiso.MotivoRechazo` (ya recortado), sin volver a transformarlo.
Un motivo válido de hasta 500 caracteres cabe completo en
`Notificacion.Mensaje` (`nvarchar(1000)`) y no se trunca.

**4. Confirmar una reserva (notifica solo al propietario).**

```powershell
$reserva = "<id de la reserva en estado Solicitada>"
curl.exe -s -X PUT "$base/api/reservas/$reserva/confirmar" -H "Authorization: $adm"
# 200: la reserva queda Confirmada

curl.exe -s "$base/api/notificaciones" -H "Authorization: $std"
# 200: [{"tipo":"ReservaConfirmada","mensaje":"Tu reserva del espacio ... fue confirmada."}]
```

**5. Desactivar el correo y repetir un evento.** La notificación interna
sigue apareciendo, pero no se encola ningún `.eml` nuevo para ese usuario:

```powershell
Enviar-Json "$base/api/usuarios/yo/notificaciones-correo" '{"habilitado":false}' -Metodo PUT -Token $std
# 200: {"habilitado":false}

# Crear una reserva nueva y confirmarla como Administrador.
Enviar-Json "$base/api/reservas" '{"espacio":"SALON B","fecha":"2027-02-10T00:00:00","horaInicio":"08:00:00","horaFin":"09:00:00"}' -Token $std
$nueva = "<id devuelto por la creación>"
curl.exe -s -X PUT "$base/api/reservas/$nueva/confirmar" -H "Authorization: $adm"

# La notificación ReservaConfirmada aparece en la bandeja de $std, pero no se
# crea un .eml nuevo dirigido a su correo.
curl.exe -s "$base/api/notificaciones" -H "Authorization: $std"
```

Los correos de activación, recuperación y restablecimiento **no** consultan
esta preferencia y siguen encolándose siempre. Cambiar la preferencia no
borra ni modifica correos ya encolados o enviados.

### Bandeja propia

```http
GET /api/notificaciones
GET /api/notificaciones?leida=false
GET /api/notificaciones?leida=true
Authorization: Bearer <token>
```

- Devuelve únicamente las notificaciones del usuario de la sesión, incluido
  cuando es Administrador. No hay listado global.
- El orden es descendente por `fechaCreacion`; los empates se resuelven por
  `id` para que el resultado sea estable.
- `leida` es opcional: sin el parámetro devuelve todas, con `true` solo las
  leídas y con `false` solo las no leídas. Cualquier otro valor responde
  `400`.
- Cada elemento tiene `id`, `tipo`, `mensaje`, `leida`, `fechaCreacion` y
  `leidaEn`. Las fechas son instantes UTC.

### Marcar como leída

```http
PUT /api/notificaciones/{id}/leida
Authorization: Bearer <token>
```

- Solo el destinatario puede marcar su notificación. Un Administrador tampoco
  puede marcar la de otro usuario.
- Es idempotente: repetir la operación responde `200` y conserva el
  `leidaEn` de la primera lectura.
- La actualización se ejecuta como un `UPDATE` condicionado por `Leida = 0`
  y modifica solo `Leida` y `LeidaEn`; no reescribe el mensaje, el tipo, el
  destinatario ni la fecha de creación. Con dos marcados concurrentes, solo
  uno afecta la fila y sobrevive la fecha de ese marcado; el segundo no la
  sobrescribe.
- Códigos: `200` marcada o ya leída, `401` sin sesión válida, `403`
  notificación existente de otro usuario y `404` notificación inexistente.
  En `403` y `404` no se expone el contenido de la notificación.

### Preferencia de correo

```http
GET /api/usuarios/yo/notificaciones-correo
PUT /api/usuarios/yo/notificaciones-correo
Content-Type: application/json

{ "habilitado": false }
```

- Ambas operaciones usan exclusivamente el usuario de la sesión; no aceptan
  un identificador de usuario en la ruta ni en el cuerpo.
- `PUT` exige `habilitado` booleano. Un cuerpo vacío, `habilitado: null` o un
  valor de otro tipo responde `400` y no desactiva nada.
- La actualización es dirigida a esa columna: no cambia nombre, correo,
  contraseña, rol, activación ni bloqueo, y no altera notificaciones ni
  correos ya existentes.
- La preferencia se lee de los datos persistidos al construir cada entrega;
  gobernar solo los correos de notificaciones. Los de activación, recuperación
  y restablecimiento no la consultan.
- Sin sesión válida, `GET` y `PUT` responden `401`.


## Cola de correos (práctica 5, P5-04 — RF-NOT-08 a RF-NOT-13)

Los correos (activación, recuperación, restablecimiento y notificaciones de
permisos o reservas) nunca se envían dentro de la operación HTTP que los
origina (`RF-NOT-08`): se encolan en la tabla `CorreosEnCola` en estado
`Pendiente`, con `Intentos = 0`, `FechaEnvio = null` y `UltimoError = null`.
Un proceso independiente (`ColaCorreosWorker` + `ProcesadorColaCorreos`,
`RF-NOT-09`) toma lotes de hasta 20 pendientes (`TamanoLote = 20`) cada
`Correo:IntervaloSegundos` (10 s por defecto; 5 s en `docker-compose.yml`) y
los entrega mediante `IEnvioCorreo`.

### 1. Configuración SMTP por variables de entorno (`RF-NOT-13`, `RD-10`)

Las credenciales y parámetros SMTP se leen únicamente de variables de entorno
(`ConfiguracionCorreoSmtp.LeerYValidar`); nunca se escriben contraseñas ni
claves reales en `appsettings.json`, `Dockerfile`, `docker-compose.yml` ni el
repositorio:

```powershell
$env:CorreoSmtp__Host = "127.0.0.1"
$env:CorreoSmtp__Puerto = "1025"
$env:CorreoSmtp__Usuario = ""
$env:CorreoSmtp__Password = ""
$env:CorreoSmtp__De = "no-responder@reservas.local"
$env:CorreoSmtp__Ssl = "false"
```

Reglas de validación al iniciar:

- **Sin `CorreoSmtp__Host`** (o vacío): la aplicación arranca en modo de
  respaldo `.eml` (`ArchivoEnvioCorreo`) y escribe cada correo en
  `App:DirectorioCorreos` (`correo-salida/` en desarrollo, `/app/correo-salida`
  en el contenedor). No exige `CorreoSmtp__De` ni credenciales.
- **Con `CorreoSmtp__Host` definido**:
  - `CorreoSmtp__De` es obligatorio y debe tener formato de correo válido.
  - `CorreoSmtp__Puerto` debe ser entero entre `1` y `65535` (por defecto `587`).
  - `CorreoSmtp__Ssl` debe ser booleano (`true` o `false`; por defecto `false`).
  - `CorreoSmtp__Usuario` y `CorreoSmtp__Password` deben definirse ambos o
    dejarse ambos vacíos (servidores locales de captura como Mailpit no
    requieren autenticación).
  - `SmtpEnvioCorreo` aplica un tiempo de espera acotado (`10 s` por defecto)
    y redacta la contraseña si algún mensaje de excepción del transporte la
    incluyera.

`docker-compose.yml` propaga `CorreoSmtp__Host`, `CorreoSmtp__Puerto`,
`CorreoSmtp__Usuario`, `CorreoSmtp__Password`, `CorreoSmtp__De` y
`CorreoSmtp__Ssl` con valores por defecto vacíos, conservando el modo `.eml` en
el arranque normal. `.env.example` documenta las variables con valores vacíos o
ficticios.

### 2. Demostración reproducible con servidor de captura SMTP (Mailpit)

Para verificar el envío SMTP real sin cuentas externas ni credenciales reales,
el archivo `docker-compose.smtp.yml` añade un contenedor `mailpit`
(`axllent/mailpit:latest`), publica su interfaz web/API **únicamente en
localhost** (`127.0.0.1:8025:8025`) y fija deliberadamente en la API:

- `CorreoSmtp__Host: "mailpit"`
- `CorreoSmtp__Puerto: "1025"`
- `CorreoSmtp__Usuario: ""` y `CorreoSmtp__Password: ""` (vacíos)
- `CorreoSmtp__De: "no-responder@reservas.local"`
- `CorreoSmtp__Ssl: "false"`

Así la composición de demostración selecciona siempre el servidor local de
captura, sin heredar `CorreoSmtp__Puerto=587` de `.env.example` ni credenciales
de un SMTP externo que pudieran existir en `.env`. El archivo principal
`docker-compose.yml` conserva intacto su comportamiento y su modo `.eml` por
defecto.

Comandos exactos de arranque y operación aislada:

> **Atención sobre `.env`:** si ya tiene un archivo `.env` local (por ejemplo,
> con su clave de WeatherAPI), **no** ejecute `Copy-Item .env.example .env`
> porque lo sobrescribiría. Solo cree `.env` si aún no existe, o use una copia
> temporal con `--env-file` para verificar sin tocar su `.env`:

```powershell
# Solo si aún no existe .env en el repositorio:
if (-not (Test-Path .env)) { Copy-Item .env.example .env }

# Arranque del proyecto aislado incluyendo ambos archivos Compose:
docker compose -p reservas-smtp -f docker-compose.yml -f docker-compose.smtp.yml up --build -d

# Estado y registros de la composición de demostración:
docker compose -p reservas-smtp -f docker-compose.yml -f docker-compose.smtp.yml ps
docker compose -p reservas-smtp -f docker-compose.yml -f docker-compose.smtp.yml logs -f api
```

Todos los comandos que operan sobre este despliegue (`ps`, `logs`, `stop`,
`start`, `down`) deben incluir `-p reservas-smtp -f docker-compose.yml -f
docker-compose.smtp.yml` para que Compose conozca el servicio `mailpit` y la
sobreescritura de variables.

Pasos de comprobación:

1. **Iniciar sesión** con el Administrador inicial (`admin@reservas.local`) y
   registrar/activar un usuario Estándar (`ana@itla.edu.do`). El correo de
   activación es capturado por SMTP y puede consultarse en
   `http://localhost:8025` (o `GET http://localhost:8025/api/v1/messages`).
2. **Provocar un evento que notifica y encola correo** (por ejemplo, crear una
   solicitud de permiso como Estándar):

```powershell
Enviar-Json "$base/api/solicitudes-permisos" '{"permiso":"CrearReserva"}' -Token $std
```

3. **Comprobar la notificación interna** del destinatario:

```powershell
curl.exe -s "$base/api/notificaciones" -H "Authorization: $adm"
# 200: [{"tipo":"SolicitudPermisoCreada","mensaje":"Nueva solicitud de permiso CrearReserva de ...","leida":false,...}]
```

4. **Comprobar el mensaje capturado por SMTP** (tras el siguiente ciclo del
   worker):

```powershell
curl.exe -s "http://localhost:8025/api/v1/messages"
```

   El mensaje muestra remitente `no-responder@reservas.local`, destinatario
   `admin@reservas.local`, asunto `Nueva solicitud de permiso` y el cuerpo con
   el solicitante y el permiso solicitado.
5. **Comprobar el estado `Enviado` en la cola**:

```powershell
curl.exe -s "$base/api/correos" -H "Authorization: $adm"
# 200: [{"id":"...","destinatario":"admin@reservas.local","asunto":"Nueva solicitud de permiso","estado":"Enviado","intentos":0,"fechaCreacion":"...","fechaEnvio":"...","ultimoError":null}, ...]
```

6. **Limpieza al terminar la demostración** (elimina únicamente los contenedores
   y volúmenes del proyecto `reservas-smtp`):

```powershell
docker compose -p reservas-smtp -f docker-compose.yml -f docker-compose.smtp.yml down -v
```

> **Captura SMTP local vs. recepción en buzón externo:** un servidor de captura
> local (Mailpit o listener SMTP en loopback) verifica de extremo a extremo que
> el worker extrae el correo de SQL Server, abre una sesión TCP/SMTP real con
> `SmtpEnvioCorreo`, transmite `MAIL FROM`, `RCPT TO` y `DATA` con el
> destinatario, asunto y cuerpo esperados, y persiste `Estado = Enviado` con
> `FechaEnvio` tras el `250 OK`. No demuestra la entrega final a un buzón
> externo de Internet (Gmail, Outlook, dominio institucional), la cual depende
> además de credenciales de un relay externo, resolución DNS/MX, reputación de
> IP, SPF/DKIM/DMARC y políticas anti-spam del proveedor.

### 3. Fallos, reintentos y máximo de 3 fallos (`RF-NOT-08`, `RF-NOT-10`)

**Semántica de `Intentos` (`D-NOT-16`):** `Intentos` cuenta **fallos de
envío**, no intentos totales ni envíos exitosos. Solo se incrementa en el
bloque `catch` de `ProcesadorColaCorreos` cuando `IEnvioCorreo.EnviarAsync`
lanza una excepción:

- Un correo enviado con éxito en su primera ejecución queda en
  `Estado = Enviado`, `Intentos = 0`, `FechaEnvio != null` y
  `UltimoError = null`.
- `ProcesadorColaCorreos.LimiteIntentos` es **3** (máximo de 3 fallos):
  - **1.er fallo:** `Estado = Pendiente`, `Intentos = 1`, `UltimoError` con el
    motivo del fallo, `FechaEnvio = null`.
  - **2.º fallo:** `Estado = Pendiente`, `Intentos = 2`, `UltimoError`
    actualizado, `FechaEnvio = null`.
  - **3.er fallo:** `Estado = Fallido`, `Intentos = 3`, `UltimoError`
    conservado, `FechaEnvio = null`.
  - **4.º ciclo y siguientes:** `ObtenerPendientesAsync` solo selecciona filas
    con `Estado = Pendiente`; un correo `Fallido` no vuelve a seleccionarse ni
    se reintenta una cuarta vez.
- Si el servidor SMTP se recupera antes del 3.er fallo (por ejemplo, tras 1 o 2
  fallos), el envío siguiente marca `Estado = Enviado`, registra `FechaEnvio`,
  limpia `UltimoError = null` y **conserva** el número de fallos previos en
  `Intentos`.

**Cómo provocar el fallo manteniendo API y BD activas:**

Con la composición de Mailpit en marcha, detener únicamente el contenedor
`mailpit` indicando la misma composición completa:

```powershell
docker compose -p reservas-smtp -f docker-compose.yml -f docker-compose.smtp.yml stop mailpit
docker compose -p reservas-smtp -f docker-compose.yml -f docker-compose.smtp.yml ps
```

(Para reactivar el servidor SMTP más adelante sin recrear el entorno:
`docker compose -p reservas-smtp -f docker-compose.yml -f docker-compose.smtp.yml start mailpit`.
En ejecución local con `dotnet run`, apuntar `CorreoSmtp__Host=127.0.0.1` a un
puerto local cerrado o detener el receptor SMTP local mientras la API y
LocalDB siguen corriendo).

1. Ejecutar una operación que notifica (por ejemplo, aprobar una solicitud con
   `PUT /api/solicitudes-permisos/{id}/aprobar`). La operación responde `200`
   de inmediato (`RF-NOT-08`), la solicitud pasa a `Aplicada` y la notificación
   interna del solicitante queda disponible en `GET /api/notificaciones`.
2. Consultar `GET /api/correos` (o `GET /api/correos/{id}`) como Administrador
   tras cada ciclo del worker:
   - **Tiempos reales de espera:** `ColaCorreosWorker` procesa el lote actual
     (hasta 20 correos pendientes en serie) y **después** espera
     `Correo:IntervaloSegundos` (`Task.Delay(_intervalo)`) antes del siguiente
     ciclo. Por tanto, el tiempo total hasta alcanzar 3 fallos no es un valor
     fijo: depende de en qué punto de la pausa actual se encoló el correo, de
     cuánto tarda cada intento de envío en fallar (inmediato si hay rechazo
     TCP/DNS o respuesta `4xx`/`5xx`, o hasta el timeout de 10 s por correo en
     `SmtpEnvioCorreo` si el destino no responde) y de cuántos correos
     pendientes haya delante en el lote. Para un único correo pendiente, cada
     ciclo dura `duración_del_fallo + IntervaloSegundos`.
   - Tras el 1.er y 2.º ciclo se observa `estado: "Pendiente"` con
     `intentos: 1` e `intentos: 2`.
   - Tras el 3.er ciclo se observa `estado: "Fallido"`, `intentos: 3`,
     `fechaEnvio: null` y `ultimoError` poblado (sin credenciales).
   - Esperar un 4.º ciclo adicional y volver a consultar confirma que `estado`
     sigue en `"Fallido"` e `intentos` permanece en `3`.

### 4. Verificación de envíos sin duplicados (`RF-NOT-12`, `D-NOT-12`)

Una vez que un correo pasa a `Enviado`:

1. `ObtenerPendientesAsync` filtra exclusivamente `Estado == EstadoCorreo.Pendiente`.
2. En la demostración operativa, tras el envío del primer ciclo del worker se
   espera uno o más ciclos adicionales (`Correo:IntervaloSegundos`) y se
   comprueba tanto en la base de datos (`GET /api/correos`: un solo registro
   con la misma `fechaEnvio` y `intentos: 0`) como en el servidor SMTP de
   captura / directorio `.eml` que el contador de mensajes recibidos para ese
   evento es **exactamente 1**.
3. No se debe lanzar un procesador manual en paralelo con `ColaCorreosWorker`
   sobre la misma cola.

> **Alcance de la garantía (`D-NOT-12`):** la no duplicación está garantizada y
> verificada para **ejecuciones secuenciales** del procesador sobre el mismo
> repositorio. No promete exclusión mutua entre múltiples instancias de worker
> concurrentes ni entrega exactamente una vez (*exactly-once*) si el proceso se
> interrumpe después de que el servidor SMTP aceptó el mensaje y antes de que
> EF Core confirme `Estado = Enviado` en SQL Server.

### 5. Consulta administrativa y correos antiguos (`RF-NOT-11`, `RD-06`)

```http
GET /api/correos
GET /api/correos/{id}
Authorization: Bearer <token-administrador>
```

- **`GET /api/correos`** devuelve hasta los **200 correos más recientes**
  (`ListadoCorreosService.LimiteListado = 200`), ordenados por `fechaCreacion`
  descendente (y `id` como desempate determinista).
- **`GET /api/correos/{id}`** permite a un Administrador consultar cualquier
  correo concreto por su `Guid`, incluidos correos antiguos que hayan quedado
  fuera de la ventana de los 200 más recientes. Responde `200` si existe o
  `404` si no existe.
- Ambas rutas proyectan únicamente `id`, `destinatario`, `asunto`, `estado`,
  `intentos`, `fechaCreacion`, `fechaEnvio` y `ultimoError`. **Nunca exponen el
  campo `cuerpo`**, evitando filtrar enlaces de activación o códigos de
  recuperación de contraseña (`RD-08`).
- Autorización verificada del lado del servidor (`[RequiereRol(Administrador)]`
  + `SesionService`):
  - Administrador con sesión activa → `200` (o `404` en `{id}` inexistente).
  - Usuario Estándar → `403 Forbidden`.
  - Sin cabecera `Authorization`, o con token desconocido, vencido o cerrado →
    `401 Unauthorized`.

### 6. Evidencia automatizada vs. demostración operativa y pendientes

- **Evidencia automatizada (781 casos en verde con `REQUIERE_PERSISTENCIA=1`):**
  - `ConfiguracionCorreoTests` (`RF-NOT-13`, `RD-07`, `RD-08`, `RD-10`):
    lectura de `CorreoSmtp__*`, fallback a `.eml` sin host, validación de
    remitente/puerto/SSL/credenciales incompletas, envío SMTP real contra un
    listener TCP local en loopback y redacción de secretos en errores.
  - `ProcesadorColaCorreosTests` (`RF-NOT-08`, `RF-NOT-09`, `RF-NOT-10`,
    `RF-NOT-12`): transición exacta de 3 fallos (`Pendiente` → `Pendiente` →
    `Fallido`) y 4.ª ejecución con 0 llamadas a envío, recuperación antes del
    3.er fallo conservando `Intentos`, y dos instancias secuenciales sin
    duplicar envíos.
  - `CorreosControllerTests` y `CorreosHttpTests` (`RF-NOT-11`, `RD-06`):
    controlador y pipeline HTTP real (`WebApplicationFactory`) para
    `GET /api/correos` y `GET /api/correos/{id}`, límite de 200 registros,
    consulta del correo antiguo #205 por ID, ausencia de `cuerpo`/tokens y
    rechazo `403` (Estándar) y `401` (sesión ausente, desconocida, vencida o
    cerrada).
  - `ColaCorreosPersistenciaTests` y `EventosNotificacionesPersistenciaTests`
    (`RF-NOT-08` a `RF-NOT-12`, `RD-09`): persistencia real en SQL Server
    LocalDB con contextos nuevos para no duplicación, 3 fallos + 4.º ciclo sin
    reintento, límite de 200 y consulta por ID.
- **Demostración operativa local ejecutada con SQL Server LocalDB (no sustituye la ejecución de Docker Compose):**
  - Se levantó la API compilada (`SistemaDeReservas.API`) en `Production`
    contra una base limpia en SQL Server LocalDB, con `ColaCorreosWorker`
    activo y un servidor SMTP de captura en `127.0.0.1`:
    1. `POST /api/usuarios` + activación + `POST /api/solicitudes-permisos`
       (`201`) creó la notificación interna `SolicitudPermisoCreada` para el
       Administrador (`GET /api/notificaciones` → `200`) y entregó 1 solo
       mensaje SMTP (`MAIL FROM:<no-responder@reservas.local>`,
       `RCPT TO:<admin@reservas.local>`), quedando `Enviado` con `intentos: 0`
       y sin duplicarse tras ciclos posteriores del worker.
    2. Con el servidor SMTP respondiendo `421 Servicio SMTP temporalmente no
       disponible`, `PUT /api/solicitudes-permisos/{id}/aprobar` terminó en
       `200` (`estado: "Aplicada"`), creó la notificación interna de Ana y el
       worker registró `Pendiente (intentos=1)`, `Pendiente (intentos=2)` y
       `Fallido (intentos=3, fechaEnvio=null, ultimoError="Service not
       available...")`; un ciclo posterior mantuvo `Fallido` con `intentos=3`
       consultado en `GET /api/correos/{id}` (`200`).
    3. Se verificaron por HTTP `GET /api/correos` y `GET /api/correos/{id}` sin
       sesión (`401`), con token inválido (`401`), con sesión de Estándar
       (`403`) y con sesión de Administrador (`200`).
- **Pendiente de ejecución en Docker Compose:** el demonio de Docker Desktop
  (`dockerDesktopLinuxEngine`) no está en ejecución en este host. La
  configuración combinada de `docker-compose.yml` + `docker-compose.smtp.yml`
  fue validada con `docker compose config` usando una copia temporal de
  `.env.example` (`Host=mailpit`, `Puerto=1025`, `Ssl=false`,
  `Autenticado=false`), pero la ejecución en vivo de los contenedores
  (`docker compose -p reservas-smtp -f docker-compose.yml -f docker-compose.smtp.yml up/stop/down`)
  queda pendiente hasta disponer de un demonio de Docker activo.

## Pruebas

Desde la raíz del repositorio, un único comando ejecuta toda la suite:

```powershell
dotnet test SistemaDeReservas/SistemaDeReservas.sln
```

Cada prueba declara el requisito que verifica con
`[Trait("Requerimiento", "RF-CA-XX")]`,
`[Trait("Requerimiento", "RF-GP-XX")]`,
`[Trait("Requerimiento", "RF-DOC-XX")]`,
`[Trait("Requerimiento", "RF-NOT-XX")]`,
`[Trait("Requerimiento", "RF-NEG-XX")]` o
`[Trait("Requerimiento", "RD-XX")]`.

### Qué necesita cada grupo de pruebas

- **Entorno mínimo** (cualquier equipo con el .NET SDK 10): los
  **736 casos sin LocalDB** — pruebas de negocio con fakes, de seguridad,
  de correo, de documentos, de evaluación climática, de notificaciones, las
  pruebas de API que invocan los controladores en memoria, las pruebas HTTP
  reales con `WebApplicationFactory`/`TestServer` y el flujo real
  permisos–reservas con repositorios de prueba. Esta parte no necesita base
  de datos ni servidor externo (la evaluación climática usa un transporte
  falso y la prueba SMTP usa un listener local efímero en loopback).
- **Entorno con persistencia**: los **45 casos de persistencia LocalDB**
  (`SistemaDeReservas.Tests/Persistencia`, RD-09) requieren **SQL Server
  LocalDB** instalado y accesible en `(localdb)\MSSQLLocalDB`. Cada una
  crea su propia base `Reservas_Tests_*`, aplica las migraciones sobre una
  base vacía y la elimina al terminar (también cuando la prueba falla);
  tras la corrida no queda ninguna base temporal.

Los 781 casos salen de **509 métodos de prueba**: 387 `[Fact]`, 77
`[Theory]` y 45 `[LocalDbFact]`; las teorías se expanden en 349 casos con
`[InlineData]`, de modo que métodos y casos son conteos distintos.

```powershell
# solo los 45 casos de persistencia LocalDB
dotnet test SistemaDeReservas/SistemaDeReservas.sln --filter "FullyQualifiedName~Persistencia"

# solo los 736 casos sin LocalDB
dotnet test SistemaDeReservas/SistemaDeReservas.sln --filter "FullyQualifiedName!~Persistencia"
```

### Cómo interpretar el resumen de `dotnet test`

- **Ejecutadas** (`Pruebas totales`): casos descubiertos (781).
- **Fallidas** (`Con error` / `Incorrecto`): casos que no cumplieron su
  aserción; en una suite sana deben ser 0.
- **Omitidas** (`Omitido`): casos que no corrieron. Solo aparecen los 45
  de persistencia cuando LocalDB no está disponible.

Con LocalDB disponible la corrida completa es **781 ejecutadas /
0 fallidas / 0 omitidas**. Sin LocalDB y sin modo estricto: **736
ejecutadas / 0 fallidas / 45 omitidas**.

### Modo estricto: `REQUIERE_PERSISTENCIA=1`

Para exigir que las pruebas de persistencia se ejecuten (p. ej. en CI o
antes de una entrega), definir la variable **antes** de lanzar el mismo
comando:

```powershell
$env:REQUIERE_PERSISTENCIA = "1"
dotnet test SistemaDeReservas/SistemaDeReservas.sln   # mismo comando único
Remove-Item Env:REQUIERE_PERSISTENCIA                 # o cerrar la terminal
```

Comportamiento verificado de los tres escenarios:

| Escenario | Resultado |
|---|---|
| LocalDB disponible (con o sin `REQUIERE_PERSISTENCIA=1`) | 781 ejecutadas, 0 fallidas, 0 omitidas. |
| Sin LocalDB, modo normal | 736 ejecutadas, 0 fallidas, **45 omitidas** con el motivo. |
| Sin LocalDB, `REQUIERE_PERSISTENCIA=1` | 781 ejecutadas, **45 fallidas** por infraestructura (`InvalidOperationException` de `BasePruebaPersistencia.InitializeAsync` indicando que LocalDB no está disponible), 736 correctas. |

- **Un fallo por ausencia de LocalDB es un problema de infraestructura,
  no es la detección de un defecto**: no prueba ningún comportamiento del
  negocio, solo que la máquina de pruebas no tiene base de datos.
- Del mismo modo, un **timeout puntual de SQL Server LocalDB** en una
  prueba de persistencia (`SqlException: se agotó el tiempo de espera`)
  es infraestructura: re-ejecutar la suite lo confirma y no cambia el
  resultado de la suite.
- `LOCALDB_PROBE_DATASOURCE` solo altera la **sonda** de disponibilidad
  (sirve para reproducir los dos escenarios anteriores sin desinstalar
  LocalDB); la creación de bases siempre usa `(localdb)\MSSQLLocalDB`.
  La variable se define y se elimina dentro de la misma sesión de
  PowerShell:

```powershell
$env:LOCALDB_PROBE_DATASOURCE = "servidor-inexistente"   # simula ausencia
dotnet test SistemaDeReservas/SistemaDeReservas.sln
Remove-Item Env:LOCALDB_PROBE_DATASOURCE
```

## Defectos de la Práctica 2 (histórico)

> Los conteos de esta sección (**303 casos**) corresponden a la suite de la
> Práctica 2 en el momento en que se verificaron esos defectos. La suite
> actual, con las operaciones de la Práctica 3, el Manejador de Documentos de
> la Práctica 4 y las notificaciones de la Práctica 5, tiene 781
> casos. Los resultados históricos no se reescriben ni se vuelven a atribuir
> a prácticas posteriores.

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
| A4 | En el listado propio se invierte «el solicitante es quien consulta» a «no es quien consulta» | `ListadoSolicitudesPersistenciaTests.ListarPropias_WithTwoApplicants_ReturnsOnlyOwnRequests` | Con dos solicitantes persistidos (3 propias y 2 ajenas), el listado propio tiene el tamaño exacto, todo lo devuelto pertenece al usuario, los identificadores coinciden exactamente con los de sus solicitudes (sin ajenas ni faltantes) y el listado del otro solicitante devuelve exactamente las suyas. |
| A5 | Se agrega `Aprobada → Pendiente` a las transiciones permitidas | `TransicionesSolicitudPermisoTests.EsValida_WithCombinationOutsideTable_DeniesIt(Aprobada, Pendiente)` (junto con `Transicionar_WithCombinationOutsideTable…`) | `Aprobada → Pendiente` se rechaza y conserva el estado. |
| A6 | Se elimina el guardado del motivo en la operación de rechazo | `ResolucionSolicitudServiceTests.Rechazar_WithMotivo_PersistsRejectionAndReason` (y 3 más, incluida la prueba de persistencia) | El rechazo válido guarda el motivo (recortado) junto al resolutor y la fecha; el inválido no resuelve la solicitud. |

Nota sobre A1: de sus 5 fallos, **1 es por aserción directa** y **4 por
excepción** (3 `InvalidOperationException` del repositorio falso y 1
`DbUpdateException`/`SqlException` del índice único en LocalDB). El
desglose se confirmó reproduciendo A1 durante la revisión de esta
documentación: 303 pruebas totales, 298 correctas, 5 incorrectas, 0
omitidas.

Nota sobre A4: su mutación vive en `SolicitudPermisoRepository.ObtenerPorSolicitanteAsync`
y ninguna otra prueba ejercita ese método sobre EF Core real (las que no
usan LocalDB pasan por `FakeSolicitudPermisoRepository`), por lo que
**solo** la prueba de persistencia lo detecta. Esa prueba necesita LocalDB:

- sin LocalDB y en modo normal se **omite**, y el defecto A4 quedaría sin
  detectar (el resto de la suite seguiría en verde);
- con `REQUIERE_PERSISTENCIA=1` y sin LocalDB la prueba **falla por
  infraestructura**, lo que **no constituye la detección del defecto**: la
  detección solo es válida cuando la prueba corre contra SQL Server y su
  aserción de resultados falla (`Expected: 3 / Actual: 2` es el mensaje
  observado con A4 introducido).

Por eso la comprobación de A4 se hace con LocalDB disponible.

### Verificación de la entrega (7 etiquetas, pendientes de crear)

La entrega será `practica-2` (base correcta) más
`practica-2-defecto-1` … `practica-2-defecto-6`. Para verificarlas:

1. **SQL Server LocalDB disponible y `REQUIERE_PERSISTENCIA=1` en las 7
   etiquetas**, para que ninguna prueba necesaria quede omitida (con
   LocalDB presente el modo estricto no cambia la ejecución; solo evita
   que la ausencia de la base se disimule como omisiones).
2. **Base en verde:** una ejecución completa satisfactoria, 303
   correctas / 0 incorrectas / 0 omitidas.
3. **Cada mutante** debe fallar por su defecto, con 0 omitidas: los
   fallos tienen que ser atribuibles al defecto introducido, no a
   compilación ni a infraestructura.
4. **Una corrida fallida se conserva como fallida.** La única corrida
   fallida de esta revisión (timeout puntual de LocalDB en
   `Esquema_CreaIndiceUnicoDeUsuarioYPermiso`) queda registrada como
   infraestructura: no se elimina ni se reclasifica, y no justifica
   modificar la implementación; se acepta porque las corridas
   satisfactorias posteriores con el mismo código dieron 303/303.

## Defectos — Práctica 3

Esta sección documenta los cuatro defectos controlados de la Práctica 3. Cada
etiqueta `practica-3-defecto-N` parte directamente del commit de
`practica-3`; no se encadenan ni se mezclan entre sí. La base debe permanecer
en verde y cada mutante debe compilar y producir al menos un fallo funcional
atribuible al cambio introducido.

| Etiqueta | Cambio intencional de producción | Prueba que lo detecta | Comportamiento que comprueba |
|---|---|---|---|
| `practica-3-defecto-1` | En `SistemaDeReservas.Core/Reservations/TransicionesReserva.cs` se agrega `Solicitada → Finalizada` a la tabla `Permitidas` con condición `Administrador`. | `TransicionReservaServiceTests.Transicionar_WithProhibitedCombination_IsRejectedAndKeepsState(origen: Solicitada, destino: Finalizada)` y `ReservasControllerTests.Transitions_OutsideTable_ReturnConflictAndKeepState(operacion: "PutFinalizar", estado: Solicitada)`. | Una transición fuera de la tabla se rechaza (`TransicionProhibida` / 409) y conserva el estado. El defecto la permite y actualiza la reserva. |
| `practica-3-defecto-2` | En `SistemaDeReservas.Core/Reservations/TransicionesReserva.cs` se agrega `Finalizada → Cancelada` a la tabla `Permitidas` con condición `Administrador`, creando una salida desde un estado terminal. | `TransicionReservaServiceTests.TerminalStates_HaveNoOutgoingTransitions`, `TransicionReservaServiceTests.Transicionar_FromTerminalState_IsRejectedAndKeepsState(origen: Finalizada, destino: Cancelada)` y `TransicionReservaServiceTests.Transicionar_WithProhibitedCombination_IsRejectedAndKeepsState(origen: Finalizada, destino: Cancelada)`. | `Finalizada` es terminal y no tiene transiciones de salida. El defecto permite salir de ella y cambia el estado. |
| `practica-3-defecto-3` | En `SistemaDeReservas.Core/Reservations/TransicionesReserva.cs`, dentro de `Evaluar`, se ignora la condición de `Solicitada → Confirmada` (se fuerza `cumple = true` para esa combinación), conservando la validación del actor y las condiciones de las demás transiciones. | `TransicionReservaServiceTests.Transicionar_AsStandardUser_CannotConfirmRejectOrFinish(origen: Solicitada, destino: Confirmada)`. | La primera transición condicionada exige Administrador: un Estándar recibe `CondicionIncumplida` y el estado no cambia. El defecto permite confirmar a un Estándar. |
| `practica-3-defecto-4` | En `SistemaDeReservas.Business/Reservas/CrearReservaService.cs` se elimina la consulta y comprobación de `AutorizacionPermisosService.PuedeAsync(..., Permiso.CrearReserva)`. | `CrearReservaServiceTests.Crear_WithoutPermission_IsRejectedWithoutWrite`, `ReservasControllerTests.Post_WithoutPermission_ReturnsForbiddenWithoutInsert` y la prueba de integración real `FlujoPermisosReservasTests.SinPermiso_SolicitudPendiente_Aprobacion_PermiteCrearReserva` (se ejecuta sin LocalDB); también falla `FlujoPermisosReservasPersistenciaTests.SinPermiso_SolicitudPendiente_Aprobacion_PermiteCrearReserva`. | Sin permiso concedido, la creación se rechaza (`PermisoDenegado` / 403) y no inserta nada; tras la aprobación, el mismo flujo crea la reserva en `Solicitada`. El defecto permite crear sin permiso. |

### Comando de verificación

```bash
dotnet test SistemaDeReservas/SistemaDeReservas.sln
```

La etiqueta `practica-3` debe pasar la suite completa. Cada etiqueta
`practica-3-defecto-N` debe compilar y producir al menos un fallo funcional
por el comportamiento alterado. Un error de compilación, un timeout de
LocalDB o un fallo de adquisición de bloqueo no cuentan como detección del
defecto.

## Documentación

- `docs/requerimientos-core.md`: requisitos oficiales del Core con sus
  identificadores (RD-XX, RF-CA-XX, RF-NOT-XX, etc.).
- `docs/matriz-requisitos.md`: estado de cada requisito y su evidencia.
- `docs/maquina-de-estados.md`: estados, transiciones y condiciones de la
  reserva (RF-NEG-03 a RF-NEG-05).
- `docs/contrato-negocio.md`: operaciones previstas del módulo de reservas y su
  autorización.
- `docs/manejador-documentos.md`: Manejador de Documentos (RF-DOC-01 a
  RF-DOC-05): endpoints, almacenamiento, límites, consistencia y pruebas.
- `docs/api-externa.md`: API externa (P4-03): evaluación climática,
  autorización, regla, horizonte, configuración, errores, ejemplo real y
  distinción entre verificación real y pruebas simuladas.
- `docs/bitacora-asignación-1.md`: bitácora del proyecto.
- `docs/practica-2-suite-permisos.md`: Práctica 2 — suite de Gestión de
  permisos: relación RF-GP ↔ pruebas, defectos detectados y resultados.
- `AGENTS.md`: convenciones del proyecto (estructura, seguridad,
  commits y pull requests).
