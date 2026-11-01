# Contrato del módulo de negocio — Reservas

Requisitos: RF-NEG-02, RF-NEG-06 y RD-04. Complementa
`docs/maquina-de-estados.md` y `docs/requerimientos-core.md`.

Este documento define las operaciones del módulo de reservas y su
autorización. Las siete operaciones están implementadas como servicios y
expuestas por endpoints protegidos en la práctica 3; el flujo completo
solicitud–aprobación–reserva está verificado con servicios reales.

## Actores

Toda operación del negocio se ejecuta con un actor obtenido de la **sesión
validada** por Control de Acceso (`SesionService.ConsultarUsuarioAsync`).

- El identificador y el rol del actor nunca se toman del cuerpo, de la ruta ni
  de la cadena de consulta de la petición.
- Un usuario sin sesión válida no ejecuta ninguna operación del negocio
  (RF-NEG-06, RD-06).
- El actor se representa en el dominio con
  `Core/Reservations/ActorReserva.cs` (`UsuarioId` + `Rol`).
- La autorización se verifica del lado del servidor; ocultar la operación en la
  interfaz no cuenta.

`ActorReserva` es solo el contexto de dominio que consumen las reglas del
negocio: **no autentica ni demuestra por sí mismo que exista una sesión
válida**. La autenticación y la construcción del actor son responsabilidad de
cada endpoint, que valida la credencial con `SesionService` y crea el
`ActorReserva` a partir del usuario autenticado. Un `ActorReserva` armado con
datos de la petición no cuenta como autorización.

Un actor ausente, sin identificador (`Guid.Empty`) o con un rol fuera del enum
se rechaza con un resultado controlado (`ActorInvalido`) antes de evaluar la
operación, aunque su identificador coincida con el propietario de la reserva.

## Operaciones del módulo

| Operación | Endpoint | Autorización | Estado en la práctica 3 |
|---|---|---|---|
| Crear reserva | `POST /api/reservas` | Usuario autenticado; consulta `AutorizacionPermisosService` para `Permiso.CrearReserva`. | Implementada (`CrearReservaService` + endpoint). |
| Consultar reservas propias | `GET /api/reservas/mias` | Usuario autenticado; solo sus reservas. | Implementada (`ListadoReservasService` + endpoint). |
| Consultar reservas para gestión | `GET /api/reservas` | Administrador. | Implementada (`ListadoReservasService` + endpoint). |
| Confirmar reserva | `PUT /api/reservas/{id}/confirmar` | Administrador. | Implementada (`TransicionReservaService` + endpoint). |
| Rechazar reserva | `PUT /api/reservas/{id}/rechazar` | Administrador. | Implementada (`TransicionReservaService` + endpoint). |
| Cancelar reserva | `PUT /api/reservas/{id}/cancelar` | Propietario de la reserva o Administrador. | Implementada (`TransicionReservaService` + endpoint). |
| Finalizar reserva | `PUT /api/reservas/{id}/finalizar` | Administrador. | Implementada (`TransicionReservaService` + endpoint). |

La creación consulta el permiso `Permiso.CrearReserva` en el servicio real de
autorización y rechaza sin guardar cuando no lo tiene. El rol Administrador
conserva el comportamiento implícito del catálogo. El servicio de transiciones
no evalúa ese permiso: mezclar ambas responsabilidades duplicaría reglas y
rompería RD-01.

## API expuesta

`API/Controllers/ReservasController.cs` recibe y responde las peticiones; no
contiene reglas de negocio. Cada operación:

- declara `[RequiereRol]` con el nivel correspondiente (RF-CA-05);
- valida la credencial con `SesionService.ConsultarUsuarioAsync` y responde
  401 si falta o no sirve;
- construye `ActorReserva` solo con el `Id` y el `Rol` del usuario de la
  sesión, nunca con campos del cuerpo;
- delega en `CrearReservaService`, `ListadoReservasService` o
  `TransicionReservaService`, sin repetir condiciones de propietario ni la
  tabla de estados.

El DTO `CrearReservaRequest` solo acepta `espacio`, `fecha`, `horaInicio` y
`horaFin`. Un `usuarioId`, `rol`, `estado` o `creadaEn` enviados por el
cliente se ignoran porque el DTO no los expone.

Mapeo de resultados:

| Código | Resultado |
|---|---|
| 201 | Reserva creada. |
| 200 | Consulta o transición exitosa. |
| 400 | Entrada inválida. |
| 401 | Sesión ausente, desconocida, vencida o cerrada. |
| 403 | Rol insuficiente, permiso `CrearReserva` ausente o condición de propietario incumplida. |
| 404 | Reserva inexistente. |
| 409 | Conflicto de horario o transición prohibida. |
| 503 | Bloqueo de aplicación no adquirido. |

Las pruebas `ReservasControllerTests` ejecutan el `AutorizacionRolFilter` real
para las restricciones de rol y los servicios reales del negocio con
repositorios de prueba para el resto. El flujo real permisos–reservas está en
`FlujoPermisosReservasTests` (repositorios de prueba) y
`FlujoPermisosReservasPersistenciaTests` (EF Core sobre LocalDB), ambas con
`CrearSolicitudPermisoService`, `ResolucionSolicitudService`,
`AutorizacionPermisosService` y `CrearReservaService` reales.

## Reglas implementadas en esta tarea

| Elemento | Ubicación | Requisito |
|---|---|---|
| Los cinco estados de la reserva | `Core/Reservations/EstadoReserva.cs` | RF-NEG-03 |
| Tabla única de transiciones con condiciones del actor | `Core/Reservations/TransicionesReserva.cs` | RF-NEG-04, RD-04 |
| Actor construido desde la sesión validada | `Core/Reservations/ActorReserva.cs` | RF-NEG-06 |
| Ejecución de la transición y rechazos controlados | `Business/Reservas/TransicionReservaService.cs` | RF-NEG-05, RD-07 |
| Regla única de disponibilidad (estados bloqueantes y solapamiento) | `Core/Reservations/DisponibilidadReserva.cs` | RF-NEG-02, RD-04 |
| Normalización del nombre del espacio | `Core/Reservations/EspacioReserva.cs` | RF-NEG-02, RD-07 |
| Creación con permiso, propietario = actor y estado inicial | `Business/Reservas/CrearReservaService.cs` | RF-NEG-02, RF-NEG-06, RD-07 |
| Consultas propias y para gestión | `Business/Reservas/ListadoReservasService.cs` | RF-NEG-02, RF-NEG-06 |
| Inserción atómica con bloqueo de aplicación | `Infrastructure/Repositories/ReservaRepository.cs` | RF-NEG-02, RD-04 |
| Exposición protegida de las siete operaciones | `API/Controllers/ReservasController.cs` | RF-NEG-02, RF-NEG-06, RF-CA-05, RD-06 |
| DTO que no acepta datos de autoridad del cliente | `API/Models/CrearReservaRequest.cs` | RF-NEG-02, RD-07 |

## Disponibilidad y concurrencia

La regla vive en `Core/Reservations/DisponibilidadReserva.cs` y se aplica tanto
en memoria como en la consulta de persistencia:

- `Solicitada` y `Confirmada` bloquean el espacio en su fecha y horario.
- `Rechazada`, `Cancelada` y `Finalizada` no bloquean.
- Dos franjas del mismo espacio y fecha se solapan cuando
  `inicioNuevo < finExistente && finNuevo > inicioExistente`.
- Los horarios consecutivos (por ejemplo, 10:00-12:00 y 12:00-14:00) son
  válidos.
- El nombre del espacio se normaliza con `Trim` y `ToUpperInvariant` antes de
  guardarlo. La consulta de persistencia compara `TRIM(UPPER(Espacio))` con el
  valor canónico, de modo que las filas antiguas sin normalizar también
  bloquean: diferencias de mayúsculas o espacios externos no evaden la
  comprobación.
- El bloqueo de aplicación se toma por **fecha**, no por espacio. La comparación
  del recurso de `sp_getapplock` es binaria, mientras que la columna puede usar
  una intercalación que considere iguales nombres que la normalización no
  colapsa (por ejemplo, acentos). Al serializar todas las creaciones de una
  fecha, cualquier par de nombres que SQL considere iguales compite por el
  mismo bloqueo y no pueden insertarse reservas incompatibles.

La comprobación y la inserción no son una consulta seguida de un guardado sin
protección. `ReservaRepository.AgregarSiDisponibleAsync` abre una transacción,
toma un bloqueo exclusivo de aplicación de SQL Server (`sp_getapplock`) por
fecha y comprueba el solapamiento. El bloqueo, la consulta y la inserción usan
la misma conexión y la misma transacción, y los parámetros SQL se envían
parametrizados. Solo se consulta e inserta cuando el código de retorno de
`sp_getapplock` es mayor o igual que cero; un código negativo revierte la
transacción y devuelve `BloqueoNoAdquirido`, que es una causa distinta de
`ConflictoHorario`. Dos creaciones concurrentes incompatibles se serializan: la
segunda ve la fila de la primera y se rechaza con `ConflictoHorario`. La prueba
de persistencia
`Crear_ConcurrentIncompatibleAttempts_LeavesOnlyOneReservation` coordina ambas
operaciones hasta que las dos alcanzan `sp_getapplock`, usa contextos y
conexiones independientes y verifica una creación, un conflicto y una única
fila. La prueba `Crear_WhenDateLockIsHeld_ReturnsLockNotAcquiredWithoutInsert`
retiene el bloqueo desde otra transacción y verifica que no se inserta nada y
que el resultado no es un conflicto de horario.

Las consultas se resuelven en el repositorio (no en memoria) y ordenan por
`Fecha`, `HoraInicio` e `Id` para que el resultado sea determinista.

Las condiciones del actor para cada transición y las veinte combinaciones
prohibidas están en `docs/maquina-de-estados.md`; la fuente única en código es
`Core/Reservations/TransicionesReserva.cs`. No se repiten aquí para no duplicar
reglas.

Un rechazo por transición inválida, por condición incumplida o por actor
inválido devuelve un resultado controlado (`TransicionProhibida`,
`CondicionIncumplida` o `ActorInvalido`), conserva el estado original y **no**
ejecuta la actualización del repositorio. Una reserva inexistente devuelve
`ReservaNoEncontrada`.

## Integración verificada

El flujo completo está cubierto sin levantar la aplicación y sin sustituir
los servicios del flujo:

1. Un Estándar sin permiso intenta crear: se rechaza y no se inserta.
2. Solicita `Permiso.CrearReserva` con `CrearSolicitudPermisoService`.
3. Mientras la solicitud está `Pendiente`, crear sigue rechazándose.
4. Otro usuario Administrador aprueba con `ResolucionSolicitudService`.
5. La aprobación crea el `PermisoOtorgado` y
   `AutorizacionPermisosService.PuedeAsync` lo confirma.
6. Con los mismos datos válidos, `CrearReservaService` permite la creación y
   la reserva queda en `Solicitada`.

Las dos versiones (repositorios de prueba y EF Core sobre LocalDB) verifican
resultados y efectos: ausencia de filas antes de la aprobación, permiso
persistido y reserva persistida en estado `Solicitada`.

## Fuera del alcance de la práctica 3

No forman parte de esta entrega ni se consideran pendientes obligatorios de la
práctica 3:

- Notificaciones propias del dominio (RF-NEG-07 / RF-NOT-03): pieza de
  Notificaciones (S11-S12).
- Reportes agregados (RF-NEG-08 / RF-REP-02): pieza de Reportes (S12).
- Adjuntos de documentos (RF-NEG-10): pieza de Documentos (S9).

RF-NEG-02 (cinco funcionalidades separables) y RF-NEG-06 (operaciones
protegidas por Control de Acceso) quedan **verificados** en esta entrega: las
siete operaciones están implementadas como servicios, expuestas por endpoints
protegidos y cubiertas por pruebas que ejecutan el filtro de rol real; además,
el flujo solicitud–aprobación–reserva se verifica con los servicios reales y
con persistencia EF Core sobre LocalDB. La evidencia concreta está en
`docs/matriz-requisitos.md`.
