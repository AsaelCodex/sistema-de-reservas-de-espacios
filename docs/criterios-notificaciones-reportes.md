# Criterios de aceptación — Notificaciones y reportes (Práctica 5)

**Requisitos oficiales:** RF-NOT-01 a RF-NOT-13 y RF-REP-01 a RF-REP-05
(`docs/requerimientos-core.md`, secciones 6 y 7).
**Etapa:** Práctica 5, semana 10.
**Estado:** criterios escritos antes de implementar (bloque P5-01). Este
documento no modifica el significado de los requisitos oficiales; define
cómo se comprobará cada uno en este repositorio y registra las decisiones
propias del proyecto con prefijo `D-NOT-XX` o `D-REP-XX`.

> **Regla de orden:** el commit que agrega este archivo y `AGENTS.md` debe ser
> anterior al primer commit del motor de notificaciones. Si durante la
> implementación cambia un criterio, se actualiza este documento en un commit
> separado y se explica el motivo; no se reescribe después para justificar lo
> entregado.

---

## 1. Alcance

### 1.1 Incluido

- Notificaciones internas para:
  - creación de una solicitud de permiso;
  - aprobación o rechazo de una solicitud de permiso;
  - confirmación válida de una reserva.
- Consulta y marcado de notificaciones propias.
- Preferencia del canal de correo por destinatario.
- Cola de correos completa y expuesta: encolado, worker independiente,
  reintentos limitados, estados, consulta por Administrador y no duplicación.
- Reporte del Core: solicitudes de permiso agrupadas por estado actual.
- Reporte del negocio: horas y reservas por espacio para reservas
  `Confirmada` y `Finalizada`.
- Filtro por rango de fechas y autorización por rol en ambos reportes.

### 1.2 Fuera de alcance

- Auditoría (RF-AUD-XX), incluida RF-DOC-06.
- Frontend o interfaz gráfica.
- Notificaciones por otros canales distintos de interno y correo.
- Eliminación o edición de notificaciones.
- Reportes de documentos o de otras entidades del Core.
- Cambios en las reglas de transición, autorización, documentos o API
  climática ya verificadas.

---

## 2. Datos de prueba y convenciones

### 2.1 Cuentas ficticias

No se usan contraseñas reales ni secretos. Las cuentas se crean con el
arnés de pruebas o con el flujo normal de registro/activación.

| Alias | Correo | Rol | Estado |
|---|---|---|---|
| `admin1` | `admin1@itla.edu.do` | Administrador | Activo |
| `admin2` | `admin2@itla.edu.do` | Administrador | Activo |
| `admin3` | `admin3@itla.edu.do` | Administrador | Inactivo |
| `ana` | `ana@itla.edu.do` | Estándar | Activo |
| `bruno` | `bruno@itla.edu.do` | Estándar | Activo |

Preferencia de correo de notificaciones: `true` para todas las cuentas,
salvo que el escenario indique lo contrario.

### 2.2 Solicitudes de permiso para el reporte del Core

Todos los `CreadaEn` son **instantes UTC** generados con `DateTime.UtcNow`; se
comparan como instantes, no como fechas de calendario.

| Id | Solicitante | Permiso | Estado actual | `CreadaEn` |
|---|---|---|---|---|
| S1 | ana | `CrearReserva` | Pendiente | `2026-03-01T10:00:00Z` |
| S2 | ana | `CrearReserva` | Aplicada | `2026-03-05T11:00:00Z` |
| S3 | bruno | `CrearReserva` | Rechazada | `2026-03-10T12:00:00Z` |
| S4 | ana | `CrearReserva` | Aplicada | `2026-03-20T13:00:00Z` |
| S5 | bruno | `CrearReserva` | Pendiente | `2026-04-02T09:00:00Z` |
| S6 | ana | `CrearReserva` | Rechazada | `2026-03-31T23:59:59Z` |
| S7 | bruno | `CrearReserva` | Aplicada | `2026-04-01T00:00:00Z` |

Notas:

- S6 y S7 prueban el límite del rango: S6 entra en marzo y S7 no.
- El estado `Aprobada` no aparece en este conjunto porque la aprobación se
  atraviesa y termina en `Aplicada`; el reporte debe poder contarlo si existiera
  una fila persistida en ese estado.

### 2.3 Reservas para el reporte del negocio

`Fecha` es una **fecha de calendario**, no un instante: se guarda normalizada
con `.Date` y no se convierte entre zonas horarias. `HoraInicio` y `HoraFin`
son horas del mismo día.

| Id | Propietario | Espacio | `Fecha` | `HoraInicio` | `HoraFin` | Estado | Duración |
|---|---|---|---|---|---|---|---|
| R1 | ana | Salón A | `2026-03-02` | 08:00 | 10:00 | Confirmada | 2.0 h |
| R2 | ana | Salón A | `2026-03-03` | 09:00 | 10:30 | Finalizada | 1.5 h |
| R3 | bruno | Cancha 1 | `2026-03-04` | 14:00 | 16:00 | Confirmada | 2.0 h |
| R4 | ana | Cancha 1 | `2026-04-10` | 10:00 | 12:00 | Confirmada | 2.0 h |
| R5 | ana | Salón A | `2026-03-05` | 08:00 | 09:00 | Rechazada | Excluida |
| R6 | ana | Salón A | `2026-03-06` | 08:00 | 09:00 | Cancelada | Excluida |
| R7 | ana | Salón A | `2026-03-07` | 08:00 | 09:00 | Solicitada | Excluida |
| R8 | bruno | Cancha 1 | `2026-03-31` | 18:00 | 19:30 | Finalizada | 1.5 h |

Notas:

- Solo R1, R2, R3, R4 y R8 son elegibles (`Confirmada` o `Finalizada`).
- R5, R6 y R7 existen para comprobar que los demás estados no se agregan.
- R8 prueba el límite superior de marzo.

### 2.4 Instantes UTC y fechas de calendario

- **Instantes:** `SolicitudPermiso.CreadaEn`, `SolicitudPermiso.ResueltaEn`,
  `Notificacion.CreadaEn`, `Notificacion.LeidaEn`,
  `CorreoEnCola.FechaCreacion` y `CorreoEnCola.FechaEnvio` son marcas de
  tiempo UTC (`DateTime.UtcNow`). Se comparan como instantes.
- **Fecha de calendario:** `Reserva.Fecha` es el día reservado, se normaliza
  con `.Date` y **no se convierte entre zonas horarias**. El 2 de marzo es el
  2 de marzo, sin importar la zona del servidor o del usuario.
- Los parámetros `desde` y `hasta` son fechas de calendario (`yyyy-MM-dd`); la
  forma de convertirlas en límites depende del tipo de dato filtrado
  (sección 2.5).
- No se usa `DateTime.Now` ni horas locales en la persistencia.

### 2.5 Semántica de los rangos `desde`/`hasta`

- Parámetros obligatorios, formato `yyyy-MM-dd`; no se aceptan horas.
- Ambos extremos son inclusivos: el día `desde` y el día `hasta` forman parte
  del rango.
- **Para instantes UTC** (reporte del Core, filtrado por `CreadaEn`) el rango
  se traduce a `[desde 00:00:00Z, hasta + 1 día 00:00:00Z)`: `CreadaEn >=
  desde 00:00:00Z` (inclusive) y `CreadaEn < (hasta + 1 día) 00:00:00Z`
  (exclusivo). Así `2026-03-31T23:59:59Z` entra en marzo y
  `2026-04-01T00:00:00Z` no.
- **Para fechas de calendario** (reporte del negocio, filtrado por
  `Reserva.Fecha`) se compara directamente `Fecha >= desde` y
  `Fecha <= hasta`, sin sumar ni restar días y sin conversión de zona.
- `hasta < desde` → `400`.
- Parámetro ausente, vacío, con formato inválido o con hora → `400`.
- No se impone un máximo arbitrario de días al rango: el requisito oficial
  (RF-REP-05) no lo exige. El costo de la consulta se controla con los
  índices de las columnas filtradas.
- Un rango válido sin registros → `200` con `grupos: []` y totales en cero;
  no es `404` ni un error.
- No hay valores por defecto silenciosos: si falta un parámetro, se rechaza.

---

## 3. Superficie funcional prevista

Las rutas son parte de la propuesta de esta práctica. Cambiarlas después de
este commit exige actualizar este documento antes de implementar.

| Método y ruta | Descripción | Rol |
|---|---|---|
| `GET /api/notificaciones?leida=...` | Notificaciones propias; `leida` es opcional (`true` o `false`); sin él, todas. | Autenticado |
| `PUT /api/notificaciones/{id}/leida` | Marca una notificación propia como leída. | Autenticado |
| `GET /api/usuarios/yo/notificaciones-correo` | Consulta la preferencia propia. | Autenticado |
| `PUT /api/usuarios/yo/notificaciones-correo` | Activa o desactiva el correo de notificaciones propio. Cuerpo: `{ "habilitado": true }`. | Autenticado |
| `GET /api/correos` | Consulta la cola (ya existente). | Administrador |
| `GET /api/reportes/solicitudes-por-estado?desde=...&hasta=...` | Reporte del Core. | Autenticado |
| `GET /api/reportes/reservas-por-espacio?desde=...&hasta=...` | Reporte del negocio. | Autenticado |

Forma de las notificaciones:

```json
{
  "id": "guid",
  "tipo": "SolicitudPermisoCreada",
  "mensaje": "texto comprensible",
  "leida": false,
  "fechaCreacion": "2026-03-01T10:00:00Z",
  "leidaEn": null
}
```

Tipos previstos: `SolicitudPermisoCreada`, `SolicitudPermisoAprobada`,
`SolicitudPermisoRechazada`, `ReservaConfirmada`.

Forma del reporte del Core:

```json
{
  "desde": "2026-03-01",
  "hasta": "2026-03-31",
  "grupos": [
    { "estado": "Pendiente", "cantidad": 1 },
    { "estado": "Aplicada", "cantidad": 2 },
    { "estado": "Rechazada", "cantidad": 2 }
  ],
  "total": 5
}
```

Forma del reporte del negocio:

```json
{
  "desde": "2026-03-01",
  "hasta": "2026-03-31",
  "grupos": [
    {
      "espacio": "SALÓN A",
      "cantidadReservas": 2,
      "horasTotales": 3.5,
      "duracionPromedioHoras": 1.75
    },
    {
      "espacio": "CANCHA 1",
      "cantidadReservas": 2,
      "horasTotales": 3.5,
      "duracionPromedioHoras": 1.75
    }
  ],
  "totalReservas": 4,
  "totalHoras": 7.0
}
```

Los nombres de espacio se muestran normalizados como los guarda el sistema
(`EspacioReserva.Normalizar`: sin espacios externos y en mayúsculas
invariantes).

---

## 4. Criterios de notificaciones

### RF-NOT-01 — Crear una solicitud notifica solo a Administradores activos

**Requisito oficial:** crear una solicitud de permiso notifica a los
Administradores; todos los Administradores reciben la notificación y nadie
más la recibe.

**Dado** `admin1` y `admin2` activos, `admin3` inactivo, `ana` y `bruno`
Estándar, y ninguna notificación previa.

**Cuando** `ana` crea una solicitud `CrearReserva` mediante
`POST /api/solicitudes-permisos`.

**Entonces**

- La solicitud responde `201` y queda en estado `Pendiente`.
- Existen exactamente dos notificaciones nuevas de tipo
  `SolicitudPermisoCreada`, destinadas a `admin1` y `admin2`.
- `admin3`, `ana` y `bruno` no tienen ninguna notificación nueva.
- Cada notificación tiene `Leida = false`, `LeidaEn = null`, una
  `FechaCreacion` UTC y un mensaje que identifica al solicitante y el permiso.
- Con las preferencias por defecto, existen exactamente dos correos
  `Pendiente` en la cola, uno por cada notificación, y ninguno se ha enviado
  durante la operación.
- Si el solicitante es Administrador activo, se le incluye como destinatario
  por su rol; no se excluye de «todos los Administradores».
- Si no existe ningún Administrador activo, la solicitud se crea igualmente y
  el resultado tiene cero notificaciones y cero correos.

**Comprobación**

- Servicio: crear la solicitud con dobles de `IUsuarioRepository`,
  `INotificacionRepository` y `IColaCorreosRepository`; contar destinatarios
  y verificar que el remitente de la operación no recibe una notificación por
  ser Estándar.
- API: `SolicitudesPermisosControllerTests` o prueba HTTP con sesiones de
  `ana`, `admin1` y `admin2`; consultar las notificaciones de cada uno.
- Manual: crear la solicitud y ejecutar
  `GET /api/notificaciones` con cada sesión.
- Negativos: sin sesión → `401`; solicitud duplicada pendiente → `409` y
  ninguna notificación nueva; permiso inexistente → `400` y ninguna
  notificación.

### RF-NOT-02 — Resolver una solicitud notifica al solicitante

**Requisito oficial:** resolver una solicitud de permiso notifica al
solicitante; la notificación indica si fue aprobada o rechazada y, en el
segundo caso, el motivo.

**Dado** una solicitud `Pendiente` creada por `ana` y un Administrador
distinto de ella.

**Cuando** el Administrador la aprueba con
`PUT /api/solicitudes-permisos/{id}/aprobar`.

**Entonces**

- La solicitud queda `Aplicada` y el permiso se otorga como hoy.
- `ana` recibe exactamente una notificación nueva de tipo
  `SolicitudPermisoAprobada`; el mensaje indica que fue aprobada y menciona el
  permiso concedido.
- `admin1`, `admin2` y `bruno` no reciben una notificación nueva por esta
  resolución.
- Se encola exactamente un correo para `ana` si su preferencia está activa.

**Cuando** el Administrador la rechaza con motivo `"No cumple los requisitos"`
mediante `PUT /api/solicitudes-permisos/{id}/rechazar`.

**Entonces**

- La solicitud queda `Rechazada` y no se otorga el permiso.
- `ana` recibe exactamente una notificación nueva de tipo
  `SolicitudPermisoRechazada`.
- El mensaje de la notificación y el cuerpo del correo contienen exactamente
  el motivo que quedó persistido en `SolicitudPermiso.MotivoRechazo`. El
  servicio actual aplica `motivo.Trim()` antes de guardarlo; por tanto, si la
  entrada trae espacios externos, la notificación y el correo muestran el
  motivo recortado, igual que la solicitud. No se vuelve a recortar ni se
  transforma de otra forma.
- No se notifica a otros usuarios.

**Comprobación**

- Servicio: resolver con fakes y verificar tipo, destinatario y contenido.
- API: aprobar y rechazar con sesión de Administrador; consultar como `ana`.
- Negativos: sin motivo → `400`, sin cambio de estado y sin notificación;
  resolver la propia solicitud → `403` y sin notificación; resolver una
  solicitud ya resuelta → `409` y sin notificación nueva.

### RF-NOT-03 — Un evento del negocio notifica al propietario

**Requisito oficial:** al menos un evento propio del módulo de negocio dispara
una notificación; el evento debe estar descrito en la documentación y poder
provocarse en la demostración.

**Dado** la reserva R1 de `ana` en estado `Solicitada`, con espacio, fecha y
horario persistidos.

**Cuando** un Administrador ejecuta
`PUT /api/reservas/{id}/confirmar` y la transición
`Solicitada → Confirmada` es válida.

**Entonces**

- La reserva queda `Confirmada`.
- `ana` recibe exactamente una notificación nueva de tipo
  `ReservaConfirmada`; el mensaje incluye el espacio, la fecha y el horario.
- Se encola un correo para `ana` si su preferencia está activa.
- `admin1` no recibe la notificación, salvo que sea el propietario de la
  reserva.
- Si el propietario confirma su propia reserva siendo Administrador, recibe
  una sola notificación como propietario.

**Comprobación**

- Servicio `TransicionReservaService` con fake de notificaciones y reserva
  persistida en el repositorio de prueba.
- API: confirmar y consultar las notificaciones de `ana` y del Administrador.
- El `README.md` debe describir este evento y el comando o petición para
  provocarlo.

**Negativos**

- Confirmar una reserva ya `Confirmada` o `Finalizada` → `409`, estado sin
  cambios y sin notificación.
- Confirmar una reserva `Rechazada` o `Cancelada` → `409` y sin notificación.
- Rechazar, cancelar o finalizar una reserva no genera `ReservaConfirmada`;
  solo la confirmación válida es el evento elegido.
- Un Estándar que intenta confirmar la reserva de otro → rechazo y sin
  notificación.

### RF-NOT-04 — Consultar solo las notificaciones propias

**Requisito oficial:** consultar las notificaciones propias, distinguiendo
leídas de no leídas; un usuario no ve las notificaciones de otro.

**Dado** notificaciones de `ana`, `bruno` y `admin1` en distintos estados de
lectura.

**Cuando** `ana` ejecuta `GET /api/notificaciones`.

**Entonces**

- Responde `200` con una lista que contiene únicamente notificaciones de
  `ana`.
- Cada elemento incluye `id`, `tipo`, `mensaje`, `leida`, `fechaCreacion` y
  `leidaEn`.
- El orden es de más reciente a más antigua por `fechaCreacion`.
- `GET /api/notificaciones?leida=false` devuelve solo las no leídas.
- `GET /api/notificaciones?leida=true` devuelve solo las leídas.
- Un valor inválido de `leida` (por ejemplo `leida=quizas`) → `400`.
- `admin1` que consulta el mismo endpoint ve solo sus propias
  notificaciones; no existe un listado global de notificaciones para
  Administrador.

**Comprobación**

- Controlador en memoria con sesiones de `ana`, `bruno` y `admin1`.
- Prueba de persistencia con dos usuarios y notificaciones cruzadas.
- Manual: `GET /api/notificaciones` con cada token.
- Negativo: sin sesión, token desconocido, vencido o cerrado → `401`.

### RF-NOT-05 — Marcar como leída solo una notificación propia

**Requisito oficial:** marcar una notificación como leída; intentar marcar la
notificación de otro usuario se rechaza.

**Dado** una notificación no leída de `ana` y otra de `bruno`.

**Cuando** `ana` ejecuta `PUT /api/notificaciones/{id}/leida` sobre la suya.

**Entonces**

- Responde `200`.
- La notificación de `ana` queda `leida = true` y con `leidaEn` no nulo.
- La notificación de `bruno` no cambia.
- No se crea, elimina ni reencola ningún correo.

**Cuando** `ana` repite la misma operación.

**Entonces**

- Responde `200` de forma idempotente.
- `leida` sigue en `true` y `leidaEn` conserva el valor de la primera marca.

**Comprobación**

- Controlador y servicio con fakes; verificar que el repositorio registra una
  sola actualización efectiva.
- Prueba de persistencia con dos usuarios.
- Negativos: notificación de `bruno` → `403` y sin cambios; notificación
  inexistente → `404`; Administrador marcando la de otro → `403`; sin sesión
  → `401`.

### RF-NOT-06 — Toda notificación llega por canal interno y por correo

**Requisito oficial:** toda notificación se entrega por dos canales: dentro
del sistema y por correo electrónico.

**Dado** `admin1` y `admin2` con preferencia de correo activa y `ana`
creando una solicitud.

**Cuando** la operación de creación termina correctamente.

**Entonces**

- Existen dos notificaciones internas, una por Administrador.
- Existen dos correos `Pendiente` en la cola, uno por notificación, con
  destinatario igual al correo del Administrador, asunto y cuerpo
  comprensibles, `Intentos = 0` y `FechaEnvio = null`.
- El envío no ocurre dentro de la operación: el contador de llamadas a
  `IEnvioCorreo` es cero durante la creación.
- El worker o una ejecución independiente del procesador cambia los correos a
  `Enviado` y registra `FechaEnvio`.
- La notificación interna es consultable aunque el correo aún esté pendiente.
- El cuerpo del correo no contiene contraseñas, tokens, hashes, claves ni
  rutas internas.

**Comprobación**

- Servicio con fakes de notificación, cola y envío; comprobar el contador de
  envíos en cero.
- Prueba del procesador con la cola real de prueba.
- Manual: crear la solicitud, consultar la notificación y luego el `.eml` o
  `GET /api/correos`.
- Negativo: si la notificación o el correo no pueden persistirse, la
  operación de origen responde un error controlado y no se declara exitosa
  con una entrega parcial (decisión D-NOT-09).

### RF-NOT-07 — El destinatario puede desactivar el correo sin perder lo interno

**Requisito oficial:** el destinatario puede desactivar el canal de correo sin
perder el canal interno; con el correo desactivado la notificación sigue
apareciendo dentro del sistema y no se encola ningún correo.

**Dado** `ana` con la preferencia de correo activa.

**Cuando** `ana` ejecuta
`PUT /api/usuarios/yo/notificaciones-correo` con
`{ "habilitado": false }`.

**Entonces**

- Responde `200` y `GET /api/usuarios/yo/notificaciones-correo` devuelve
  `{ "habilitado": false }`.
- Una notificación posterior para `ana` (por ejemplo, la resolución de su
  solicitud) se crea internamente y aparece en
  `GET /api/notificaciones`.
- No se crea ningún correo nuevo en la cola para esa notificación.
- Los correos ya encolados antes del cambio no se eliminan ni se modifican.
- Al volver a `{ "habilitado": true }`, las notificaciones posteriores vuelven
  a encolar correo.
- La preferencia no afecta los correos de activación de cuenta, recuperación
  de contraseña ni restablecimiento forzado: esos flujos siguen encolando
  aunque la preferencia esté desactivada.

**Comprobación**

- Controlador y servicio de preferencia con sesión de `ana`.
- Servicio de resolución con preferencia desactivada; contar correos.
- Flujo de registro y recuperación con preferencia desactivada; verificar que
  sus correos siguen en la cola.
- Negativos: sin sesión → `401`; cuerpo sin `habilitado` o con un tipo
  distinto de booleano → `400`; intentar modificar la preferencia de otro
  usuario no tiene efecto (el endpoint solo opera sobre la sesión).

### RF-NOT-08 — Los correos se encolan, no se envían en la operación

**Requisito oficial:** los correos no se envían dentro de la operación que
los origina; la operación termina correctamente aunque el servidor de correo
no responda.

**Dado** un servidor SMTP apagado, inaccesible o un `IEnvioCorreo` que lanza
una excepción, y la preferencia de correo activa.

**Cuando** se crea una solicitud, se resuelve o se confirma una reserva.

**Entonces**

- La operación de origen responde con su código de éxito normal (`201` o
  `200`) y el cambio de estado se persiste.
- La notificación interna se persiste.
- El correo queda en la cola en estado `Pendiente`, con `Intentos = 0`,
  `FechaEnvio = null` y sin último error.
- La operación no llama a `IEnvioCorreo`: el fallo del SMTP no se propaga en
  ese momento.
- Una ejecución posterior del procesador intenta el envío, incrementa
  intentos y registra el error de forma controlada.
- La API sigue respondiendo a login, reservas, documentos y notificaciones.

**Comprobación**

- Servicio de origen con un `IEnvioCorreo` que lanza y un contador de
  llamadas; verificar cero llamadas y éxito de la operación.
- Prueba con el worker o el procesador y un envío que falla.
- Manual: detener el SMTP o usar un host inexistente, crear la solicitud y
  comprobar que responde éxito y que la cola tiene el correo pendiente.
- Negativo: si la notificación o el encolado no pueden guardarse, no se
  devuelve éxito con una entrega parcial (D-NOT-09).

### RF-NOT-09 — Un proceso independiente envía los pendientes

**Requisito oficial:** un proceso independiente toma los correos pendientes y
los envía; se ejecuta sin que el flujo que creó el correo vuelva a correr.

**Dado** un correo `Pendiente` creado por una operación anterior y el flujo de
origen ya terminado.

**Cuando** se ejecuta el procesador de la cola, ya sea por el worker
automático o por una ejecución manual/independiente.

**Entonces**

- El correo pasa a `Enviado`.
- Se registra `FechaEnvio` UTC.
- El envío se realiza una sola vez.
- No se repite la operación de negocio que creó la notificación: no se crean
  solicitudes, notificaciones ni correos adicionales.
- Si no hay pendientes, el procesador devuelve cero y no falla.

**Comprobación**

- Prueba del procesador con un repositorio de cola que ya contiene un correo,
  sin invocar el servicio de negocio.
- Prueba del worker con un alcance de servicios controlado.
- Manual: crear un correo, esperar al worker o ejecutar el procesador y
  comprobar el estado con `GET /api/correos`.

### RF-NOT-10 — Reintentos limitados y estado fallido

**Requisito oficial:** un correo que falla se reintenta un número limitado de
veces y luego queda marcado como fallido; no se reintenta indefinidamente ni
desaparece sin dejar rastro. El máximo de fallos debe estar declarado en el
README.

**Comportamiento actual de `Intentos` (verificado en
`ProcesadorColaCorreos`):** `Intentos` cuenta **fallos**, no intentos totales.
Se incrementa únicamente en el bloque `catch`; un envío exitoso no lo
incrementa, de modo que un correo encolado y enviado al primer intento
conserva `Intentos = 0`. Con `LimiteIntentos = 3`, el tercer fallo deja
`Estado = Fallido` y conserva `UltimoError`. `ObtenerPendientesAsync` solo
devuelve correos `Pendiente`, así que un correo `Fallido` no vuelve a
procesarse: no hay un cuarto reintento ni un cambio de estado adicional. La
implementación debe conservar esta semántica; el README explicará que el
límite es de 3 fallos.

**Dado** un correo `Pendiente` y un envío que siempre falla.

**Cuando** se ejecuta el procesador tres veces.

**Entonces**

- Tras la primera ejecución: estado `Pendiente`, `Intentos = 1`,
  `UltimoError` no vacío y `FechaEnvio = null`.
- Tras la segunda: estado `Pendiente`, `Intentos = 2`, `UltimoError`
  actualizado.
- Tras la tercera: estado `Fallido`, `Intentos = 3` y `UltimoError`
  conservado.
- Una cuarta ejecución no vuelve a llamar al envío y mantiene el estado
  `Fallido` y el número de intentos.
- El correo no se elimina ni se oculta de la consulta del Administrador.
- Si el envío tiene éxito antes del límite, el correo pasa a `Enviado`,
  `UltimoError` queda nulo y el número de intentos previos se conserva.
- Un correo que falla no impide que los demás del lote se envíen.

**Comprobación**

- `ProcesadorColaCorreosTests` con un `IEnvioCorreo` que falla y otro que
  responde.
- Verificar que `ProcesadorColaCorreos.LimiteIntentos` es 3 y que el README
  declara ese máximo de fallos.
- Manual: provocar el fallo según el README y consultar la cola tras cada
  ejecución.

### RF-NOT-11 — El Administrador consulta el estado de la cola

**Requisito oficial:** cada correo registra estado (`Pendiente`, `Enviado` o
`Fallido`), número de intentos, fechas y último error; un Administrador puede
consultar la cola y saber qué pasó con cualquier correo.

**Dado** una cola con al menos un correo `Pendiente`, uno `Enviado` y uno
`Fallido`.

**Cuando** `admin1` ejecuta `GET /api/correos`.

**Entonces**

- Responde `200` con los correos ordenados del más reciente al más antiguo.
- Cada elemento incluye `id`, `destinatario`, `asunto`, `estado`, `intentos`,
  `fechaCreacion`, `fechaEnvio` y `ultimoError`.
- Los tres estados aparecen y los valores corresponden a lo persistido.
- Un correo `Fallido` muestra su último error; uno `Enviado` muestra su fecha
  de envío; uno `Pendiente` conserva sus intentos y su último error si ya
  falló.
- La respuesta no incluye credenciales SMTP, contraseñas ni cuerpos con
  secretos.
- Se mantiene el límite actual de 200 correos.

**Comprobación**

- `CorreosControllerTests` con sesiones de Administrador y Estándar.
- Prueba de persistencia que lea la cola con un contexto nuevo.
- Negativos: Estándar → `403`; sin sesión, token inválido o vencido → `401`.

### RF-NOT-12 — Un correo enviado no se vuelve a enviar

**Requisito oficial:** un correo enviado no se vuelve a enviar; ejecutar el
procesador de la cola dos veces seguidas no duplica ningún envío.

**Dado** un único correo `Pendiente` y un envío que funciona.

**Cuando** se ejecuta el procesador dos veces seguidas.

**Entonces**

- La primera ejecución envía una vez y devuelve `1`.
- El correo queda `Enviado` con `FechaEnvio` no nula.
- La segunda ejecución devuelve `0`, no llama al envío y no modifica
  `FechaEnvio` ni el estado.
- El total de envíos observados es exactamente uno.
- Un correo que ya estaba `Enviado` al inicio tampoco se procesa.

**Comprobación**

- `ProcesadorColaCorreosTests` con un contador en el fake de envío.
- Prueba con dos instancias secuenciales del procesador sobre el mismo
  repositorio.
- Manual: ejecutar el procesador dos veces y comprobar un solo `.eml` o un
  solo envío.
- **Alcance de la garantía:** lo anterior se comprueba con ejecuciones
  **secuenciales** del procesador sobre el mismo repositorio. No se presenta
  como garantía frente a dos workers concurrentes ni frente a una caída del
  proceso después de que el SMTP aceptó el envío y antes de persistir
  `Enviado`. Esos escenarios pueden producir un envío duplicado y quedan
  fuera de RF-NOT-12 (D-NOT-12).

### RF-NOT-13 — Credenciales SMTP solo por variables de entorno

**Requisito oficial:** las credenciales del servidor de correo se leen de
variables de entorno; no aparecen en el repositorio ni en el historial
(RD-10).

**Dado** el código de configuración de correo y el historial de Git.

**Cuando** se revisa la configuración y se arranca la aplicación.

**Entonces**

- El host, puerto, usuario, contraseña, remitente y SSL se leen de
  `CorreoSmtp__Host`, `CorreoSmtp__Puerto`, `CorreoSmtp__Usuario`,
  `CorreoSmtp__Password`, `CorreoSmtp__De` y `CorreoSmtp__Ssl`.
- No hay contraseñas, usuarios reales ni claves SMTP escritas en código,
  `appsettings.json`, pruebas, documentación, Dockerfile ni
  `docker-compose.yml`.
- `.env.example` solo contiene valores ficticios o marcadores.
- Sin `CorreoSmtp:Host`, la aplicación arranca y usa el envío a archivos
  `.eml`; no falla por falta de credenciales.
- Los logs y las respuestas no muestran la contraseña ni la cadena de
  conexión.
- Una búsqueda en el árbol y en el historial no encuentra credenciales SMTP
  reales ni la clave de WeatherAPI.

**Comprobación**

- Prueba de configuración con variables ficticias; verificar que
  `SmtpEnvioCorreo` recibe los valores esperados sin abrir una conexión real.
- Prueba de arranque sin host SMTP; verificar el fallback a `.eml`.
- Revisión de `git log -p` y búsqueda de patrones de credenciales **sin
  imprimir valores reales**; el resultado esperado es cero coincidencias.
- No leer `.env` para esta comprobación; usar `.env.example`.

### Criterio transversal — consistencia entre estado y entrega

No tiene un RF propio: concreta cómo se comprueban RF-NOT-01 a RF-NOT-06 y
sirve de criterio de consistencia para toda operación que notifica.

1. **Operación rechazada = cero efectos de entrega.** Si una operación de
   negocio termina en rechazo (`400`, `403`, `409` o el código que
   corresponda), no crea notificaciones ni correos nuevos, no modifica
   notificaciones existentes y no cambia el estado de la entidad. Repetir la
   operación rechazada sigue sin generar entregas.
2. **Operación exitosa = entrega completa o ninguna.** Si una operación
   termina en éxito y el canal de correo del destinatario está habilitado,
   persiste en la misma unidad de trabajo el cambio de estado, la
   notificación interna y el correo `Pendiente` correspondiente. Si el canal
   está deshabilitado, persiste el cambio de estado y la notificación interna,
   y no encola correo. No se admite éxito con notificación sin cambio de
   estado, ni cambio de estado sin notificación cuando la operación debía
   notificar.
3. **Comprobación:** en las pruebas de servicio se verifica el estado final y
   los conteos de notificaciones y correos para el caso exitoso y para el
   rechazado; en las pruebas de API se verifica el código HTTP y se consulta
   la bandeja y la cola después de la operación.

---

## 5. Criterios de reportes

### RF-REP-01 — Reporte del Core con valores agrupados

**Requisito oficial:** un reporte sobre el Core —solicitudes de permiso por
estado y por período, o documentos subidos por usuario— que devuelva valores
agrupados, no el listado de registros.

**Decisión del proyecto:** se implementa **solicitudes de permiso agrupadas
por estado actual**, filtradas por `CreadaEn`.

**Dado** el conjunto S1–S7 de la sección 2.2.

**Cuando** `admin1` consulta
`GET /api/reportes/solicitudes-por-estado?desde=2026-03-01&hasta=2026-03-31`.

**Entonces**

- Responde `200` con `desde`, `hasta`, `grupos` y `total`.
- `grupos` contiene solo estados con al menos una solicitud:
  `Pendiente: 1`, `Aplicada: 2`, `Rechazada: 2`.
- `total` es `5`.
- La respuesta tiene 3 filas de grupo para 5 solicitudes; no incluye el
  listado individual ni identificadores de solicitudes.
- El orden de los grupos es el orden del enum
  (`Pendiente`, `Aprobada`, `Rechazada`, `Aplicada`), omitiendo los estados
  sin filas.
- `Aprobada` no aparece porque no hay filas en ese estado; si existiera, se
  contaría.

**Cuando** `ana` consulta el mismo rango.

**Entonces** obtiene `Pendiente: 1`, `Aplicada: 2`, `Rechazada: 1`,
`total: 4`; S3 de `bruno` no se cuenta.

**Comprobación**

- Servicio de reporte con un repositorio de prueba que devuelva S1–S7.
- Prueba de persistencia con SQL Server LocalDB y `GroupBy` real.
- API: `admin1`, `ana` y `bruno`; verificar totales y ausencia de listados.
- Manual: dos rangos distintos (marzo y abril) y un rango vacío (mayo).

### RF-REP-02 — Reporte del negocio que responde una pregunta real

**Requisito oficial:** un reporte sobre el módulo de negocio, según el dominio
elegido, que responda una pregunta real del dominio y no un conteo de filas.

**Pregunta del dominio:** «¿Qué espacios concentran más horas de uso efectivo
y cuántas reservas confirmadas o finalizadas reciben en el período?».

**Decisión del proyecto:** se agrupa por espacio y se incluyen únicamente
reservas en estado `Confirmada` o `Finalizada`, filtradas por `Fecha`.

**Dado** el conjunto R1–R8 de la sección 2.3.

**Cuando** `admin1` consulta
`GET /api/reportes/reservas-por-espacio?desde=2026-03-01&hasta=2026-03-31`.

**Entonces**

- Responde `200` con `desde`, `hasta`, `grupos`, `totalReservas` y
  `totalHoras`.
- `grupos` contiene:
  - `SALÓN A`: `cantidadReservas: 2`, `horasTotales: 3.5`,
    `duracionPromedioHoras: 1.75`.
  - `CANCHA 1`: `cantidadReservas: 2`, `horasTotales: 3.5`,
    `duracionPromedioHoras: 1.75`.
- `totalReservas` es `4` y `totalHoras` es `7.0`.
- R5 (`Rechazada`), R6 (`Cancelada`) y R7 (`Solicitada`) no se cuentan.
- R8 entra por estar en `2026-03-31` y ser `Finalizada`.
- R4 no entra porque su fecha es de abril.
- La respuesta tiene 2 grupos para 4 reservas elegibles; no es un conteo de
  filas ni un listado de reservas.

**Cuando** `ana` consulta el mismo rango.

**Entonces** obtiene solo `SALÓN A` con 2 reservas, 3.5 horas y promedio
1.75; no ve R3 ni R8 de `bruno`.

**Comprobación**

- Servicio de reporte con repositorio de prueba y R1–R8.
- Prueba de persistencia con `GroupBy`, `Sum`, `Count` y `Average` reales.
- API: Administrador y Estándar; comparar totales.
- Manual: dos rangos y un rango vacío; comprobar los valores exactos.

### RF-REP-03 — Agregación real en ambos reportes

**Requisito oficial:** ambos reportes usan agregación real —agrupaciones,
totales o promedios—; el resultado tiene menos filas que los registros que
resume.

**Dado** los conjuntos S1–S7 y R1–R8.

**Cuando** se ejecutan los reportes de marzo.

**Entonces**

- El reporte del Core devuelve 3 grupos para 5 solicitudes (o 3 grupos para
  4 en el caso de `ana`), no 5 ni 4 filas individuales.
- El reporte del negocio devuelve 2 grupos para 4 reservas elegibles, no 4
  filas individuales.
- Los valores se calculan con agrupación y agregación en el repositorio o en
  una función de dominio; el controlador no recibe el listado completo para
  resumirlo.
- `cantidad`, `total`, `horasTotales`, `totalReservas`, `totalHoras` y
  `duracionPromedioHoras` son coherentes entre sí.
- Un rango sin registros devuelve cero grupos y totales en cero; no se
  considera una violación de «menos filas» porque no hay registros que
  resumir.

**Comprobación**

- Revisión de código: `GroupBy`/`Count`/`Sum`/`Average` en la consulta o
  servicio, no en el controlador.
- Pruebas con los conjuntos de la sección 2; verificar número de filas.
- No se acepta como cumplimiento un endpoint que devuelva todas las
  solicitudes o reservas y deje la suma a la interfaz.

### RF-REP-04 — Los reportes respetan el rol

**Requisito oficial:** los reportes respetan el rol de quien consulta; un
usuario Estándar no obtiene datos de otros usuarios a través de un reporte.

**Dado** los conjuntos S1–S7 y R1–R8, con datos de `ana` y `bruno`.

**Cuando** cada usuario consulta ambos reportes para marzo.

**Entonces**

- `admin1` obtiene todos los registros: Core `total: 5`; negocio
  `totalReservas: 4`, `totalHoras: 7.0`.
- `ana` obtiene solo sus registros: Core `total: 4` (S1, S2, S4, S6);
  negocio `totalReservas: 2`, `totalHoras: 3.5`.
- `bruno` obtiene solo los suyos: Core S3 (Rechazada, total 1); negocio R3 y
  R8 (2 reservas, 3.5 horas).
- El filtro por usuario se aplica **antes** de agrupar y sumar; los totales de
  un Estándar nunca incluyen filas de otro.
- Un Estándar no puede ampliar el alcance con un parámetro como
  `usuarioId`, `todos=true` o similar: esos parámetros se ignoran o se
  rechazan.
- Sin sesión válida, ambos reportes responden `401`.

**Comprobación**

- Servicio de reporte con un actor de rol Estándar y otro Administrador.
- API con sesiones de `ana`, `bruno` y `admin1`; comparar totales.
- Prueba de persistencia que verifique que la consulta filtra por
  `UsuarioId`/`SolicitanteId` antes de `GroupBy`.
- Negativos: sin sesión → `401`; intento de consultar datos ajenos → mismo
  resultado propio, nunca datos de otro.

### RF-REP-05 — Filtro por rango de fechas

**Requisito oficial:** ambos reportes se pueden filtrar por rango de fechas;
dos rangos distintos producen resultados distintos sobre los mismos datos.

**Dado** los mismos conjuntos S1–S7 y R1–R8.

**Cuando** se consulta marzo (`2026-03-01` a `2026-03-31`) y abril
(`2026-04-01` a `2026-04-30`).

**Entonces**

- Core, Administrador:
  - marzo: `Pendiente: 1`, `Aplicada: 2`, `Rechazada: 2`, `total: 5`;
  - abril: `Pendiente: 1`, `Aplicada: 1`, `total: 2`.
- Core, `ana`: marzo total 4; abril sin grupos y total 0.
- Negocio, Administrador:
  - marzo: `SALÓN A` 2/3.5/1.75 y `CANCHA 1` 2/3.5/1.75; total 4/7.0;
  - abril: solo `CANCHA 1` 1/2.0/2.0; total 1/2.0.
- Negocio, `ana`: marzo `SALÓN A` 2/3.5/1.75; abril `CANCHA 1` 1/2.0/2.0.
- Los límites son inclusivos: S6 (`2026-03-31T23:59:59Z`) entra en marzo;
  S7 (`2026-04-01T00:00:00Z`) no. R8 (`2026-03-31`) entra en marzo.
- El reporte del Core aplica a `CreadaEn` el intervalo semiabierto
  `[desde 00:00:00Z, hasta + 1 día 00:00:00Z)`. El reporte del negocio compara
  `Reserva.Fecha` como fecha de calendario (`>= desde`, `<= hasta`), sin
  conversión de zona ni suma de días.
- `desde > hasta` → `400`.
- Formato inválido, ausente o con hora → `400`.
- Rango válido sin datos (por ejemplo mayo de 2026) → `200` con
  `grupos: []` y totales en cero.
- No se impone un máximo de días: el requisito oficial no lo exige.

**Comprobación**

- Servicio con un repositorio de prueba parametrizado por rango.
- Prueba de persistencia con datos en los límites.
- API: dos rangos distintos y un rango vacío para cada reporte.
- Manual: comparar las respuestas de marzo y abril; no se acepta que el
  parámetro de fechas se ignore.

---

## 6. Escenarios negativos obligatorios

| # | Escenario | Dado | Cuando | Entonces | Comprobación |
|---|---|---|---|---|---|
| N-01 | Sesión inválida | Endpoints de notificaciones, preferencia, reportes y cola | Se invoca sin `Authorization`, con token desconocido, vencido o cerrado | `401`; no se devuelve ningún dato ni se crea estado | Pruebas de controlador y HTTP para cada endpoint |
| N-02 | Acceso a notificación ajena | Notificación no leída de `bruno` | `ana` intenta marcarla; `admin1` intenta marcarla | `403`; la notificación sigue no leída; `leidaEn` no cambia | Controlador y persistencia |
| N-03 | Acceso a reporte ajeno | Datos de `ana` y `bruno` | `ana` consulta los reportes e intenta añadir `usuarioId=bruno` | Solo se agregan datos de `ana`; el parámetro no amplía el alcance | Servicio y API |
| N-04 | Transición rechazada sin notificación | Solicitud ya resuelta o reserva ya confirmada | Se repite aprobar/confirmar o se intenta una transición fuera de la tabla | `409` (o el rechazo que corresponda); estado sin cambios; cero notificaciones y cero correos nuevos | Servicios de resolución/transición y API |
| N-05 | Correo desactivado sin encolado | `ana` con `habilitado: false` | Se genera una notificación para ella | Notificación interna creada; cero correos nuevos para esa notificación; activación y recuperación siguen encolando | Servicio de notificaciones y flujos de Control de Acceso |
| N-06 | SMTP caído | Host SMTP inaccesible o envío que lanza | Se crea/resuelve/confirma; luego se ejecuta el procesador | La operación de origen responde éxito; el correo queda pendiente; los intentos avanzan; no se exponen credenciales | Servicio con envío falso y prueba manual con SMTP apagado |
| N-07 | Límite de fallos | Correo pendiente y envío que siempre falla | Se ejecuta el procesador tres veces y una cuarta | Fallos 1, 2 y 3 (`Intentos = 1, 2, 3`); al tercer fallo `Fallido`; la cuarta no llama al envío porque solo se consultan `Pendiente`; el error se conserva | `ProcesadorColaCorreosTests` |
| N-08 | Procesador ejecutado dos veces | Un correo pendiente y envío correcto | Se ejecuta el procesador dos veces seguidas | Un solo envío; la segunda ejecución devuelve 0; estado `Enviado` | `ProcesadorColaCorreosTests` |
| N-09 | Rango inválido | Reportes disponibles | `hasta < desde`, formato inválido o parámetro ausente | `400` controlado, sin listados ni detalles internos | Controlador y API |
| N-10 | Rango vacío | Reportes disponibles | Se consulta un rango sin registros | `200` con `grupos: []` y totales en cero | Servicio y API |
| N-11 | Sin Administradores activos | Ningún Administrador activo | `ana` crea una solicitud | `201`; cero notificaciones y cero correos; la solicitud existe | Servicio de creación |
| N-12 | Duplicado de solicitud | Solicitud pendiente de `ana` | `ana` crea otra igual | `409`; no se crea una segunda notificación ni un segundo correo | Servicio y API de permisos |
| N-13 | Cola consultada por Estándar | Cola con correos | `ana` ejecuta `GET /api/correos` | `403`; no se exponen destinatarios, asuntos ni errores | `CorreosControllerTests` |
| N-14 | Preferencia manipulada por otro | `ana` y `bruno` con preferencias distintas | `ana` intenta cambiar la de `bruno` mediante el cuerpo o la ruta | Solo cambia la preferencia de `ana`; la de `bruno` permanece igual; no hay endpoint para administrar la de otro | Controlador de preferencia |

---

## 7. Decisiones y ambigüedades

Las siguientes son decisiones propias del proyecto. El requisito oficial no
las define; se documentan para que la implementación y la revisión sean
reproducibles.

| ID | Ambigüedad o decisión | Decisión |
|---|---|---|
| D-NOT-01 | Atributos de `Notificacion`. | `Id`, `DestinatarioId`, `Tipo`, `Mensaje`, `Leida`, `CreadaEn` y `LeidaEn`. `CreadaEn` es UTC. Se indexa por destinatario, estado de lectura y fecha. |
| D-NOT-02 | Catálogo de tipos. | `SolicitudPermisoCreada`, `SolicitudPermisoAprobada`, `SolicitudPermisoRechazada` y `ReservaConfirmada`. Se serializa como texto. |
| D-NOT-03 | «Todos los Administradores». | Solo Administradores **activos**. Un Administrador inactivo no recibe notificación ni correo. Si no hay ninguno, la solicitud se crea con cero notificaciones. |
| D-NOT-04 | Notificación de aprobación. | La solicitud termina en `Aplicada` por la máquina de estados, pero la notificación usa el tipo `SolicitudPermisoAprobada` y el mensaje dice que fue aprobada y que el permiso quedó concedido. No se expone el estado interno «Aplicada» como resultado distinto. |
| D-NOT-05 | Notificación de rechazo. | Se usa el valor ya persistido en `SolicitudPermiso.MotivoRechazo`; el servicio actual aplica `motivo.Trim()`, así que la notificación y el correo muestran ese mismo valor recortado. Sin motivo no hay rechazo ni notificación. |
| D-NOT-06 | Evento del negocio. | Se elige la confirmación válida de una reserva (`Solicitada → Confirmada`) y se notifica al propietario. Rechazar, cancelar o finalizar no generan esta notificación. |
| D-NOT-07 | Preferencia de correo. | Se agrega a `Usuario` un booleano `RecibirCorreosNotificacion` con valor inicial `true`, mediante una migración nueva. Afecta solo a los correos de notificaciones; activación, recuperación y restablecimiento siguen encolando siempre. |
| D-NOT-08 | Alcance de la bandeja. | `GET /api/notificaciones` siempre se limita al usuario de la sesión, incluido el Administrador. No existe un listado global de notificaciones; la cola de correos es la vista administrativa. |
| D-NOT-09 | Atomicidad de la entrega. | La notificación interna y el correo encolado forman una sola unidad de trabajo. Si la preferencia está activa y una de las dos escrituras falla, la operación de origen no se declara exitosa y no deja una entrega parcial. Si la preferencia está desactivada, solo se escribe la notificación interna. |
| D-NOT-10 | Idempotencia de lectura. | Marcar una notificación ya leída responde `200`, mantiene `Leida = true` y conserva el `LeidaEn` original. No se considera un error. |
| D-NOT-11 | Acceso a notificación ajena. | `403` para una notificación existente de otro usuario, `404` para una inexistente y `401` sin sesión. El Administrador tampoco puede marcar notificaciones ajenas. |
| D-NOT-12 | Doble procesamiento. | RF-NOT-12 se garantiza solo para ejecuciones secuenciales: la segunda ejecución no encuentra el correo `Pendiente`. Las pruebas de procesamiento consecutivo no demuestran exclusión mutua entre workers concurrentes ni recuperación ante una caída posterior al envío y anterior a la persistencia de `Enviado`; esos casos no se prometen ni se usan como criterio de aceptación. |
| D-NOT-13 | Contenido de los mensajes. | El texto exacto no es un requisito. Las pruebas verifican datos clave (solicitante, permiso, resultado, motivo, espacio, fecha y horario) y que no haya secretos; no comparan una redacción literal. |
| D-NOT-14 | Reintentos y notificaciones. | Reintentar el envío de un correo no crea otra notificación. Repetir una operación de negocio rechazada tampoco crea una notificación nueva. |
| D-NOT-15 | Reutilización de la cola. | Se conservan `CorreoEnCola`, `IColaCorreosRepository`, `ProcesadorColaCorreos`, `ColaCorreosWorker`, el límite de 3 fallos y `GET /api/correos`. Las notificaciones solo agregan correos a esa cola; no se crea una segunda cola ni un segundo worker. |
| D-NOT-16 | Semántica de `Intentos`. | `Intentos` cuenta **fallos**, no envíos exitosos: se incrementa solo cuando el envío lanza. Al tercer fallo el correo queda `Fallido` y deja de ser candidato porque el procesador solo consulta `Pendiente`. Un envío exitoso conserva el número de fallos previos y limpia `UltimoError`. |
| D-REP-01 | Reporte del Core. | Se elige «solicitudes de permiso agrupadas por estado actual», no documentos por usuario. Se filtra por `CreadaEn`. |
| D-REP-02 | Reporte del negocio. | Se elige «horas totales y cantidad de reservas por espacio», solo `Confirmada` y `Finalizada`, con duración promedio. Se filtra por `Fecha` de la reserva, no por `CreadaEn`. |
| D-REP-03 | Semántica de fechas. | `desde` y `hasta` obligatorios, `yyyy-MM-dd`, inclusivos. Instantes (`CreadaEn`, Core): `>= desde 00:00:00Z` y `< (hasta + 1 día) 00:00:00Z`. Fechas de calendario (`Reserva.Fecha`, negocio): `>= desde` y `<= hasta`, sin conversión de zona. `hasta < desde` o formato inválido → `400`; no se impone un máximo de días porque el requisito oficial no lo exige. Rango vacío → `200` con grupos y totales vacíos. |
| D-REP-04 | Autorización de reportes. | Administrador ve todos los registros; Estándar solo los propios. El filtro se aplica antes de agrupar. Los parámetros del cliente no pueden ampliar el alcance. |
| D-REP-05 | Grupos vacíos. | Se omiten los estados y espacios sin registros. Un rango sin datos devuelve `grupos: []`, no grupos con cero. |
| D-REP-06 | Redondeo. | Los totales se calculan sin redondear y se proyectan con hasta dos decimales. En los ejemplos, `3.5`, `1.75`, `7.0` y `2.0` son valores exactos. |
| D-REP-07 | Orden de grupos. | Core: orden del enum (`Pendiente`, `Aprobada`, `Rechazada`, `Aplicada`). Negocio: orden alfabético por nombre de espacio normalizado. |
| D-REP-08 | Rutas y nombres JSON. | Las rutas y campos de la sección 3 son la propuesta inicial. Cambiarlos después de este commit exige actualizar este documento antes de implementar. |
| D-REP-09 | Límite de resultados. | Los reportes son agregados y no se paginan; el número de grupos está acotado por los estados y los espacios, no por la cantidad de registros. |

### 7.1 Ambigüedades que quedan explícitamente resueltas

- **«Todos los Administradores»**: se interpreta como activos (D-NOT-03).
- **«Dos canales»**: el canal interno siempre existe; el correo se encola
  solo si la preferencia está activa (D-NOT-07, RF-NOT-07).
- **«Notificar al solicitante»**: se notifica solo al solicitante, no a todos
  los Administradores (D-NOT-04).
- **«Un evento del negocio»**: se elige la confirmación de reserva (D-NOT-06).
- **«Reporte por período»**: el período se aplica a `CreadaEn` en el Core y a
  `Fecha` en el negocio (D-REP-01, D-REP-02).
- **«Datos de otros usuarios»**: se impide filtrando por el usuario de la
  sesión antes de agregar (D-REP-04).

### 7.2 Ambigüedades que no bloquean la implementación

- La redacción exacta de los mensajes y asuntos queda a criterio de la
  implementación, siempre que incluya los datos exigidos (D-NOT-13).
- La concurrencia de dos workers simultáneos y la caída del proceso después
  del envío pero antes de persistir `Enviado` no están exigidas por RF-NOT-12;
  se documentan como limitaciones y no se presentan como garantías (D-NOT-12).
- La preferencia de correo se guarda en `Usuario`; si en el futuro se
  requieren más canales, se podrá extraer a una tabla de preferencias sin
  cambiar el comportamiento observado.

---

## 8. Trazabilidad y pruebas previstas

Las pruebas se crearán en la implementación (P5-02 en adelante); este bloque
P5-01 no agrega código ni pruebas.

| Requisito | Criterio | Pruebas previstas |
|---|---|---|
| RF-NOT-01 | Sección 4, RF-NOT-01 | `Tests/Notificaciones/NotificacionServiceTests`, `Tests/API/SolicitudesPermisosTests` |
| RF-NOT-02 | Sección 4, RF-NOT-02 | `Tests/Notificaciones/NotificacionServiceTests`, `Tests/API/SolicitudesPermisosTests` |
| RF-NOT-03 | Sección 4, RF-NOT-03 | `Tests/Notificaciones/NotificacionServiceTests`, `Tests/Reservas/TransicionReservaServiceTests` |
| RF-NOT-04 | Sección 4, RF-NOT-04 | `Tests/API/NotificacionesControllerTests`, `Tests/Persistencia/NotificacionPersistenciaTests` |
| RF-NOT-05 | Sección 4, RF-NOT-05 | `Tests/API/NotificacionesControllerTests`, `Tests/Persistencia/NotificacionPersistenciaTests` |
| RF-NOT-06 | Sección 4, RF-NOT-06 | `Tests/Notificaciones/NotificacionServiceTests`, `Tests/Notificaciones/ProcesadorColaCorreosTests` |
| RF-NOT-07 | Sección 4, RF-NOT-07 | `Tests/API/PreferenciaNotificacionesTests`, `Tests/Notificaciones/NotificacionServiceTests` |
| RF-NOT-08 | Sección 4, RF-NOT-08 | `Tests/Notificaciones/ProcesadorColaCorreosTests`, `Tests/API/SolicitudesPermisosTests` |
| RF-NOT-09 | Sección 4, RF-NOT-09 | `Tests/Notificaciones/ProcesadorColaCorreosTests`, `Tests/Notificaciones/ColaCorreosWorkerTests` |
| RF-NOT-10 | Sección 4, RF-NOT-10 | `Tests/Notificaciones/ProcesadorColaCorreosTests` |
| RF-NOT-11 | Sección 4, RF-NOT-11 | `Tests/API/CorreosControllerTests`, `Tests/Persistencia/ColaCorreosPersistenciaTests` |
| RF-NOT-12 | Sección 4, RF-NOT-12 | `Tests/Notificaciones/ProcesadorColaCorreosTests` |
| RF-NOT-13 | Sección 4, RF-NOT-13 | `Tests/Email/ConfiguracionCorreoTests`, revisión de historial |
| RF-REP-01 | Sección 5, RF-REP-01 | `Tests/Reportes/SolicitudesPorEstadoServiceTests`, `Tests/API/ReportesControllerTests` |
| RF-REP-02 | Sección 5, RF-REP-02 | `Tests/Reportes/ReservasPorEspacioServiceTests`, `Tests/API/ReportesControllerTests` |
| RF-REP-03 | Sección 5, RF-REP-03 | `Tests/Reportes/*`, revisión de la consulta |
| RF-REP-04 | Sección 5, RF-REP-04 | `Tests/Reportes/*`, `Tests/API/ReportesControllerTests` |
| RF-REP-05 | Sección 5, RF-REP-05 | `Tests/Reportes/*`, `Tests/Persistencia/ReportesPersistenciaTests` |

Además, cada prueba debe declarar `[Trait("Requerimiento", "RF-NOT-XX")]`,
`[Trait("Requerimiento", "RF-REP-XX")]` o el `RD-XX` correspondiente.

---

## 9. Definición de terminado de la Práctica 5

La práctica se considera terminada cuando:

1. Este documento y `AGENTS.md` están en un commit anterior al primer commit
   del motor de notificaciones.
2. Los 18 requisitos (RF-NOT-01 a RF-NOT-13 y RF-REP-01 a RF-REP-05) tienen
   implementación y pruebas que citan el requisito.
3. La suite completa pasa sin regresiones; las pruebas de persistencia se
   ejecutan con `REQUIERE_PERSISTENCIA=1` en un entorno con LocalDB.
4. El `README.md` explica:
   - cómo provocar cada evento de notificación;
   - cómo consultar y marcar notificaciones;
   - cómo desactivar el correo;
   - cómo ejecutar el procesador de la cola;
   - cómo provocar un fallo de SMTP;
   - el máximo de 3 fallos (`Intentos` cuenta fallos, no envíos exitosos);
   - qué pregunta responde el reporte del negocio;
   - cómo consultar ambos reportes con un rango de fechas.
5. Se comprobaron los seis frentes de la revisión acumulada de la Práctica 5.
6. No se agregaron secretos, no se leyó `.env` y el diff no contiene cambios
   fuera de alcance.
7. La etiqueta `practica-5` se crea solo cuando la entrega esté cerrada.
