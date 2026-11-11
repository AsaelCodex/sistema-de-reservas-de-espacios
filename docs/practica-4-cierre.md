# Práctica 4 — comprobación final y cierre (P4-04)

## 1. Veredicto

**PASS.** La práctica 4 cumple los cinco criterios de la rúbrica
(8.0 / 8.0) desde un clon limpio: documentos, contenedor con un solo
comando, volumen persistente, API externa con la clave como secreto y
los seis frentes de la revisión acumulada.

Se encontraron y corrigieron **dos defectos de documentación** (D-01 y
D-02, §12). No se modificó código ni configuración.

## 2. Base evaluada y entorno

| Dato | Valor |
|---|---|
| SHA completo evaluado | `2598e99788f46c90b94b84448ca3e477a523ecd0` |
| Referencia | `main` == `origin/main` (merge del PR #45, P4-03) |
| Contenido | P4-01 (`feature/practica-4-documentos`), P4-02 (`feature/practica-4-contenedor`) y P4-03 (`feature/practica-4-api-externa`) son ancestros de la base |
| Rama de trabajo | `chore/practica-4-cierre` |
| Fecha de ejecución | 2026-11-10 (America/Santo_Domingo) |
| Sistema | Windows 11 Home 10.0.26200 |
| Docker | 29.9.2 |
| Docker Compose | v5.6.1 |
| .NET SDK | 10.0.401 |
| SQL Server | LocalDB disponible (`(localdb)\MSSQLLocalDB`) |

El código y la configuración evaluados son exactamente la base anterior.
Los commits posteriores de esta rama son únicamente de documentación
(correcciones D-01 y D-02, e informe de cierre). El SHA final de la rama
se identifica en la entrega, separado del SHA evaluado.

## 3. Método

1. Se verificó que `main` contiene los tres hitos y que el árbol estaba
   limpio. `main` ya coincidía con `origin/main`, por lo que la
   actualización por fast-forward no tuvo cambios que aplicar.
2. Se creó la rama `chore/practica-4-cierre` sobre la base.
3. Se creó un **clon independiente** en un directorio temporal, sin
   `.git` compartido con el árbol de trabajo, sin `bin`, `obj`, `.env`,
   documentos, correos ni datos de instalaciones anteriores.
4. Se seleccionó el SHA indicado y se copió `.env.example` a `.env`
   **sin modificar sus valores**.
5. Se arrancó con el comando documentado, usando un proyecto Compose
   aislado:

   ```powershell
   Copy-Item .env.example .env
   docker compose -p p4-cierre-eval up --build -d
   ```

   Desviación registrada: se añadió `-d` para poder automatizar la
   comprobación y `-p p4-cierre-eval` para no tocar los volúmenes del
   usuario. El README documenta `docker compose up --build`; el build y
   el arranque son los mismos. El comando terminó con código `0`.
6. Se comprobó que solo la API publica puerto:
   `127.0.0.1:5137->8080/tcp`. SQL Server expone `1433/tcp` solo dentro
   de la red de la composición.
7. Se comprobó la URL documentada. `GET /api/documentos` sin sesión
   responde `401` y la raíz `404`; ninguna respuesta incluye rutas,
   trazas ni detalles internos.
8. No se ejecutaron migraciones, scripts de base ni instalaciones
   manuales para el arranque. SQL se usó únicamente para verificar el
   estado persistido exigido en los apartados de documentos y
   persistencia.
9. Al terminar, se eliminó el proyecto aislado con
   `docker compose -p p4-cierre-eval down -v`. No se tocaron los
   volúmenes del usuario.

## 4. Matriz de la rúbrica

| Criterio | Puntos | Resultado | Evidencia |
|---|---:|---|---|
| Manejador de documentos RF-DOC-01 a 04 | 2.0 | Logrado | §5 |
| Contenedor con un solo comando | 1.5 | Logrado | §3 |
| Volumen persistente RF-DOC-05 | 1.0 | Logrado | §6 |
| API externa con la clave como secreto | 1.5 | Logrado | §7 y §10 |
| Revisión acumulada (seis frentes) | 2.0 | Logrado | §8 |
| **Total** | **8.0** | **PASS** | |

## 5. Documentos (RF-DOC-01 a RF-DOC-04)

Usuarios: Administrador inicial, Ana (Estándar) y Luis (Estándar),
registrados y activados por correo. Se usaron documentos distintos para
la eliminación y para la persistencia.

### Metadatos y propiedad

| Operación | Resultado |
|---|---|
| Subida de Ana (`ana-eliminar.txt`) | `201`; nombre, `text/plain`, 53 bytes, fecha UTC y `usuarioId` de Ana |
| Subida de Ana (`ana-persistir.txt`) | `201`; 47 bytes, propietario Ana |
| Subida de Luis (`luis-persistir.txt`) | `201`; 43 bytes, propietario Luis |
| Subida con `usuarioId` de Luis en el formulario, autenticado como Ana | `201`; el propietario asignado fue **Ana**: el campo del cliente se ignora |
| Listado de Ana | `200`, solo sus 2 documentos activos |
| Listado de Luis | `200`, solo su documento |
| Listado del Administrador | `200`, los 3 documentos de ambos usuarios |

### Descarga y eliminación

| Operación | Esperado | Obtenido |
|---|---|---|
| Ana descarga su documento | `200` | `200` |
| Administrador descarga el de Ana | `200` | `200` |
| Administrador descarga el de Luis | `200` | `200` |
| Luis descarga el de Ana | `403` | `403` |
| Luis elimina el de Ana | `403` | `403` |
| Ana elimina su propio documento | `204` | `204` |
| Administrador elimina un documento de Ana | `204` | `204` |
| Sin sesión: subir (multipart), listar, descargar, eliminar | `401` | `401` en las cuatro |

### Validación de la subida (RD-07)

| Caso | Resultado |
|---|---|
| Multipart sin el campo `archivo` | `400` «El archivo es obligatorio.» |
| Archivo vacío | `400` «El archivo está vacío.» |
| Sin sesión y sin cuerpo multipart | `415` (rechazo del parser antes de la acción); con multipart válido y sin sesión, `401` |

### Eliminación lógica (RF-DOC-04)

Tras eliminar `ana-eliminar.txt`:

- No aparece en el listado de Ana ni en el del Administrador.
- `GET /api/documentos/{id}/contenido` responde `404`.
- La fila sigue en `dbo.Documentos` con `Eliminado = 1` y
  `EliminadoEnUtc` establecido.
- El archivo físico sigue en `/app/documentos` (identificador de
  contenido del servidor).
- El documento eliminado por el Administrador se comporta igual.

### Integridad del contenido

`ana-persistir.txt` se descargó por el endpoint protegido y se comparó
con el archivo original:

| Dato | Valor |
|---|---|
| Tamaño original / descargado | 47 / 47 bytes |
| SHA-256 original | `06601D776691ECA00E67AFADE0C9DD8FD976B7F5674A98C83EFF2E129D275C3E` |
| SHA-256 descargado | `06601D776691ECA00E67AFADE0C9DD8FD976B7F5674A98C83EFF2E129D275C3E` |

## 6. Persistencia (RF-DOC-05, RD-09)

Estado registrado antes de detener la composición:

- Usuario Ana registrado y activado; inicio de sesión correcto.
- Documento activo `ana-persistir.txt` (47 bytes, SHA-256 anterior).
- Documentos eliminados: `ana-eliminar.txt` y el eliminado por el
  Administrador.
- Administrador inicial: identificador
  `7D359406-2157-4DC0-90FE-D6855707E879`, rol Administrador.
- Usuarios totales: 3. Administradores: 1. Documentos: 4 (2 eliminados).

Se ejecutó `docker compose -p p4-cierre-eval down` **sin `-v`** y luego
`docker compose -p p4-cierre-eval up -d`. Tras el arranque:

| Comprobación | Resultado |
|---|---|
| Inicio de sesión nuevo con Ana | Correcto |
| Listado de Ana | Solo `ana-persistir.txt` |
| Descarga del documento | `200`; SHA-256 idéntico al original |
| Documento eliminado | Sigue sin listarse y responde `404` al descargarlo |
| Fila y archivo del eliminado | Persisten (`Eliminado = 1`, `EliminadoEnUtc`; archivo en `/app/documentos`) |
| Administrador inicial | Mismo identificador y mismo `BINARY_CHECKSUM` de su fila |
| Conteos | 3 usuarios, 1 administrador, 4 documentos (2 eliminados) |

No se usó como evidencia un simple reinicio del proceso: se destruyeron y
recrearon los contenedores conservando los volúmenes nombrados.

## 7. API externa

La operación evaluada es `GET /api/reservas/{id}/evaluacion-climatica`.
La reserva utilizada fue creada por Ana para el **2026-11-11, 14:00–16:00**
en `CANCHA 1`, dentro del horizonte consultable.

### Escenario A — `.env` copiado del ejemplo (clave ficticia)

| Comprobación | Resultado |
|---|---|
| Evaluación de una reserva futura | `503` controlado: clave rechazada por el proveedor |
| Inicio de sesión | `201` |
| `GET /api/reservas` | `200` |
| `GET /api/documentos` | `200` |

### Escenario B — clave real solo en el `.env` de la copia aislada

La clave se tomó del entorno local autorizado y se escribió únicamente en
el `.env` de la copia de verificación; no se imprimió ni se versionó. Se
recreó la API para aplicar la configuración.

Respuesta real saneada (ejecutada el 2026-11-10 en America/Santo_Domingo):

```json
{
  "reservaId": "<id>",
  "fecha": "2026-11-11",
  "horaInicio": "14:00:00",
  "horaFin": "16:00:00",
  "zonaHoraria": "America/Santo_Domingo",
  "ubicacion": {
    "nombre": "ITLA, La Caleta, Santo Domingo Este",
    "latitud": 18.4512,
    "longitud": -69.6626
  },
  "probabilidadMaximaLluvia": 15,
  "umbralProbabilidadLluvia": 60,
  "advertenciaLluvia": false,
  "horasEvaluadas": [
    { "hora": "14:00:00", "probabilidadLluvia": 13 },
    { "hora": "15:00:00", "probabilidadLluvia": 15 }
  ],
  "orientacion": "Orientación para actividades al aire libre.",
  "mensaje": "No se alcanza el umbral configurado de advertencia de lluvia. Esto no garantiza ausencia de lluvia; es una orientación para actividades al aire libre."
}
```

Coherencia verificada: se evaluaron las dos horas completas que
intersectan `[14:00, 16:00)`; el máximo (15) coincide con
`max(13, 15)`; el umbral es 60; no se advierte lluvia.

| Comprobación adicional | Resultado |
|---|---|
| Otro usuario Estándar (Luis) | `403` |
| Administrador | `200` |
| Reserva después de evaluar | Sin cambios: `Solicitada`, misma fecha, horario y espacio |

### Sin clave

Se dejó `WeatherApi__ApiKey` vacía y se recreó la API:

- La aplicación arrancó correctamente.
- La evaluación respondió `503` controlado («no está configurada»).
- Inicio de sesión, reservas y documentos siguieron respondiendo `200`.

### Proveedor inaccesible

La indisponibilidad se simuló **solo dentro del contenedor aislado**,
añadiendo una entrada de host que envía `api.weatherapi.com` a una
dirección no enrutable. No se modificó el cliente de producción ni el
código.

| Comprobación | Resultado |
|---|---|
| Evaluación | `504` en 10.4 s, dentro del tiempo de espera configurado (10 s) |
| Inicio de sesión | `201` |
| Reservas | `200` |
| Documentos | `200` |

Después se recreó el contenedor para descartar la modificación temporal.

## 8. Revisión acumulada: seis frentes

Todo se ejecutó por HTTP real contra el contenedor. Los tokens y códigos
de recuperación no se reproducen.

| # | Frente | Petición | Esperado | Obtenido | Estado persistido |
|---|---|---|---|---|---|
| 1 | Registro y activación | `POST /api/usuarios` y `GET /activar?token=…` leído del `.eml` | `201` + activación correcta | `201` y `200`; login posterior correcto | Usuario activo; segundo registro del mismo correo `409` |
| 2 | Inicio y cierre de sesión | `POST /api/sesiones`, `GET /api/usuarios/yo`, `DELETE /api/sesiones`, reutilizar el token | `201`, `200`, cierre y rechazo | `201`, `200`, cierre `200`, token cerrado `401` | Sesión invalidada en la base |
| 3 | Estándar invocando operaciones de Administrador | `GET /api/usuarios`, `PUT /api/usuarios/{id}/rol`, `GET /api/solicitudes-permisos/pendientes` como Ana | `403` | `403` en las tres | Sin cambios |
| 4 | Recuperación de contraseña | `POST /api/recuperacion-contrasena`, lectura del código en el `.eml`, `POST …/cambio` | Flujo por correo y cambio | `200` (idéntico para correo inexistente), cambio `200` | Contraseña nueva con hash |
| 4b | Invalidación tras el cambio | Login con la contraseña anterior y uso de una sesión previa | Rechazo | `401` y `401` | Sesión anterior rechazada |
| 4c | Código de un solo uso | Reutilizar el código | Rechazo | `400` | Contraseña sin cambios |
| 5 | Permisos: crear, aprobar, rechazar | `POST /api/solicitudes-permisos`, `PUT …/aprobar`, `PUT …/rechazar` con motivo | `201`, `200` Aplicada, `200` Rechazada | Igual | `Estado = 3` (Aplicada), `Estado = 2` (Rechazada) con motivo y aprobador |
| 5b | Transiciones prohibidas | Aprobar dos veces, aprobar una rechazada, rechazar una aplicada | `409` | `409` en los tres | Estado sin cambios |
| 5c | Motivo obligatorio | Rechazar con motivo vacío o ausente | `400` | `400` | Sigue Pendiente |
| 5d | Nadie resuelve su propia solicitud | Administrador aprueba/rechaza una solicitud creada por él | `403` | `403` en ambas | Sigue Pendiente |
| 6 | Reservas: operación válida y transición prohibida | Crear reserva; `finalizar` desde `Solicitada`; `confirmar`; `finalizar`; `cancelar` una finalizada | `201`; `409`; `200`; `200`; `409` | Igual | `Solicitada` → `Confirmada` → `Finalizada`; la cancelación no cambia el estado |

## 9. Límites con Kestrel

Configuración de `.env.example`: `TamanioMaximoBytes = 10485760` (10 MiB)
y `MargenSolicitudBytes = 1048576` (1 MiB).

| Caso | Resultado |
|---|---|
| Archivo de exactamente 10 485 760 bytes | `201`, `tamanoBytes = 10485760` |
| Archivo de 10 485 761 bytes | `413` controlado: «El archivo supera el tamaño máximo permitido de 10485760 bytes.» |
| Petición de 11 MiB (por encima del máximo total) | `400` controlado del lector de formularios: «Request body too large. The max request body size is 11534336 bytes.» |

Ninguna respuesta incluyó rutas de archivos, trazas de pila, consultas
SQL ni rutas internas. La tercera respuesta es un `ProblemDetails`
estándar de ASP.NET Core con `traceId`; se registra como comportamiento
observado en §13.

## 10. Secretos (RD-10)

| Comprobación | Resultado |
|---|---|
| `.env` versionado | 0 apariciones en el árbol y 0 en todo el historial (69 ramas y 14 etiquetas) |
| Clave real de WeatherAPI en el árbol versionado | 0 coincidencias |
| Clave real de WeatherAPI en el historial completo | 0 coincidencias |
| Credenciales locales de SQL Server y del administrador | Coinciden exactamente con los valores **ficticios** de `.env.example`; son valores de demostración, no credenciales reales |
| `.env` en la imagen | No existe ningún `.env` ni `.env.*` dentro de la imagen |
| Clave real en archivos de la imagen | 0 archivos |
| Variables de entorno de la imagen | Solo nombres estándar (`ASPNETCORE_*`, `DOTNET_*`, `PATH`, `APP_UID`); la clave no está horneada |
| `.dockerignore` | Excluye `.env` y `.env.*`, con excepción de `.env.example`; también excluye `.git`, `bin`, `obj`, pruebas y contenido generado |

No se ejecutó `docker compose config` ni `docker inspect` con salida
completa de variables. No se reescribió el historial.

## 11. Suite

Comandos ejecutados desde la raíz, sin reutilizar una compilación previa
para la verificación final:

```powershell
dotnet restore SistemaDeReservas/SistemaDeReservas.sln
dotnet build SistemaDeReservas/SistemaDeReservas.sln --no-restore
$env:REQUIERE_PERSISTENCIA = "1"
dotnet test SistemaDeReservas/SistemaDeReservas.sln --no-build
Remove-Item Env:REQUIERE_PERSISTENCIA
```

| Métrica | Resultado |
|---|---|
| Restauración | Correcta (0 errores) |
| Compilación | Correcta, 0 errores, 9 advertencias |
| Pruebas | **659 correctas, 0 fallidas, 0 omitidas** |
| Persistencia en modo estricto | 19 pruebas LocalDB ejecutadas; sin omisiones |

Advertencias existentes (no se afirma que sean inocuas):

- `NU1902` y `NU1903` en API, Infrastructure y Tests: el paquete
  transitivo `System.Security.Cryptography.Pkcs` 9.0.11 tiene
  vulnerabilidades conocidas de gravedad moderada y alta
  (`GHSA-2fmv-rv2c-59x7`, `GHSA-846f-f764-76mx`).
- `CS8524` en `ReservasController.cs` (líneas 69, 266 y 282): las
  expresiones `switch` sobre enumeraciones no cubren valores sin nombre.

No se modificaron dependencias ni código para silenciar estas
advertencias, porque quedan fuera del alcance de corrección de esta
práctica.

## 12. Defectos encontrados y correcciones

### D-01 — README: ruta de correos incorrecta en el flujo con contenedor

- **Requisito afectado:** revisión acumulada (registro con activación por
  correo y recuperación de contraseña); el evaluador sigue el README al
  pie de la letra.
- **Descripción:** los pasos «Sesión de Estándar» y «Sesión de
  Administrador» leían el `.eml` desde
  `SistemaDeReservas/SistemaDeReservas.API/correo-salida/`, una ruta del
  host que no existe al ejecutar con Docker. El correo real está en el
  volumen, en `/app/correo-salida`. La sección «Consultar los correos
  `.eml`» sí describía el comando correcto, pero los pasos que el
  evaluador ejecuta no lo usaban.
- **Corrección:** se añadió a «Consultar los correos `.eml`» el bloque
  para leer el último correo desde el contenedor y extraer el enlace de
  activación o el código de recuperación. Los dos pasos afectados usan
  ahora ese bloque, con la alternativa local documentada.
- **Comprobación posterior:** con el README corregido se registró un
  usuario nuevo, se leyó el `.eml` más reciente con los comandos
  documentados, se extrajo el token y se activó la cuenta (`200`); el
  inicio de sesión posterior fue correcto.
- **Alcance:** solo documentación; no cambió código ni configuración, por
  lo que no fue necesario repetir los escenarios funcionales.

No se encontraron otros defectos que impidieran cumplir los requisitos.

### D-02 — Matriz: RD-01 a RD-05 marcados como pendientes

- **Requisito afectado:** coherencia de la matriz con README, AGENTS.md y
  el código; los RD son criterios de diseño evaluados en el code review.
- **Descripción:** `docs/matriz-requisitos.md` marcaba RD-01, RD-02,
  RD-03, RD-04 y RD-05 como «Pendiente» («todavía no se ha
  implementado»), aunque están implementados y verificados desde las
  prácticas anteriores.
- **Corrección:** se marcaron como «Verificado» con evidencia concreta:
  contratos e interfaces del Core y separación por capas (RD-01),
  servicios de Business invocados por controladores (RD-02),
  `Core.csproj` sin referencias y sin usos de Business (RD-03),
  `TransicionesReserva.Evaluar` y `TransicionReservaService` como único
  punto de transición (RD-04) y `Pbkdf2PasswordHasher` con sus pruebas
  (RD-05).
- **Comprobación posterior:** se revisaron las referencias de proyecto,
  las apariciones de `SistemaDeReservas.Business` en Core (0) y las
  pruebas de transiciones y hashing. No cambió código ni configuración.
- **Alcance:** solo documentación.

## 13. Limitaciones y observaciones

- La verificación se hizo con LocalDB disponible; no aplica la limitación
  de pruebas de persistencia omitidas.
- Las advertencias de §11 se registran sin evaluar su impacto; corregirlas
  requiere actualizar dependencias, fuera del alcance de P4-04.
- Una petición que supera el máximo total del cuerpo devuelve `400` con
  el mensaje estándar de ASP.NET Core, no `413`. El rechazo es controlado
  y no expone rutas, trazas ni SQL, pero el texto y el código no son los
  mismos que usa el límite del archivo (`413`). Se documenta como
  comportamiento observado; el README solo promete `413` para el archivo
  que supera `Documentos:TamanioMaximoBytes`.
- El ejemplo climático es una ejecución real del 2026-11-10; los valores
  concretos cambian con el pronóstico. Lo estable es la forma de la
  respuesta y la regla aplicada.
- La comprobación se ejecutó con `-d` (segundo plano) sobre el comando
  documentado, para automatizarla; el build y el arranque son los mismos.
- Los datos de prueba (usuarios, documentos y reservas) pertenecen al
  proyecto Compose aislado, que se eliminó con `down -v`.

## 14. Fuera del alcance (no se presentan como completados)

- **RF-DOC-06:** auditoría de subidas y eliminaciones.
- Frontend.
- Reportes (`RF-REP-*`).
- Otras piezas del Core no incluidas en la práctica 4.

## 15. Comandos reproducibles (sin secretos)

```powershell
# 1. Clon limpio en el SHA evaluado
git clone <repositorio> reservas-cierre
Set-Location reservas-cierre
git checkout 2598e99788f46c90b94b84448ca3e477a523ecd0

# 2. Configuración de ejemplo y arranque aislado
Copy-Item .env.example .env
docker compose -p p4-cierre-eval up --build -d

# 3. Comprobación de la URL documentada
curl.exe -s -o NUL -w "%{http_code}" http://localhost:5137/api/documentos   # 401
docker compose -p p4-cierre-eval ps

# 4. Suite completa desde la raíz del repositorio
dotnet restore SistemaDeReservas/SistemaDeReservas.sln
dotnet build SistemaDeReservas/SistemaDeReservas.sln --no-restore
$env:REQUIERE_PERSISTENCIA = "1"
dotnet test SistemaDeReservas/SistemaDeReservas.sln --no-build
Remove-Item Env:REQUIERE_PERSISTENCIA

# 5. Limpieza solo del proyecto aislado
docker compose -p p4-cierre-eval down -v
```

Las peticiones HTTP de los seis frentes y de documentos se documentan en
las tablas anteriores; los cuerpos completos requieren las credenciales
de demostración de `.env.example` o una cuenta registrada y activada.
