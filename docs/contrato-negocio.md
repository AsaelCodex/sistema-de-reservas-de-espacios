# Contrato del módulo de negocio — Reservas

Requisitos: RF-NEG-02, RF-NEG-06 y RD-04. Complementa
`docs/maquina-de-estados.md` y `docs/requerimientos-core.md`.

Este documento define las operaciones previstas del módulo de reservas y su
autorización. Distingue lo implementado en la práctica 3 de lo que queda para
bloques posteriores.

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
cada futuro endpoint, que deberá validar la credencial con `SesionService` y
crear el `ActorReserva` a partir del usuario autenticado. Un `ActorReserva`
armado con datos de la petición no cuenta como autorización.

Un actor ausente, sin identificador (`Guid.Empty`) o con un rol fuera del enum
se rechaza con un resultado controlado (`ActorInvalido`) antes de evaluar la
operación, aunque su identificador coincida con el propietario de la reserva.

## Operaciones previstas

| Operación | Autorización | Estado en la práctica 3 |
|---|---|---|
| Crear reserva | Usuario autenticado; la operación de creación consulta `AutorizacionPermisosService` para `Permiso.CrearReserva`. | Servicio implementado (`CrearReservaService`); endpoint futuro. |
| Consultar reservas propias | Usuario autenticado; solo sus reservas. | Servicio implementado (`ListadoReservasService`); endpoint futuro. |
| Consultar reservas para gestión | Administrador. | Servicio implementado (`ListadoReservasService`); endpoint futuro. |
| Confirmar reserva | Administrador. | Regla de transición implementada; operación y endpoint futuros. |
| Rechazar reserva | Administrador. | Regla de transición implementada; operación y endpoint futuros. |
| Cancelar reserva | Propietario de la reserva o Administrador. | Regla de transición implementada; operación y endpoint futuros. |
| Finalizar reserva | Administrador. | Regla de transición implementada; operación y endpoint futuros. |

La creación consulta el permiso `Permiso.CrearReserva` en el servicio real de
autorización y rechaza sin guardar cuando no lo tiene. El rol Administrador
conserva el comportamiento implícito del catálogo. El servicio de transiciones
no evalúa ese permiso: mezclar ambas responsabilidades duplicaría reglas y
rompería RD-01.

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
  guardarlo y antes de compararlo. El valor canónico se persiste, de modo que
  diferencias de mayúsculas o espacios externos no evaden la comprobación.

La comprobación y la inserción no son una consulta seguida de un guardado sin
protección. `ReservaRepository.AgregarSiDisponibleAsync` abre una transacción,
toma un bloqueo exclusivo de aplicación de SQL Server (`sp_getapplock`) por
espacio normalizado y fecha, comprueba el solapamiento y, solo si no hay
conflicto, inserta y confirma. Dos creaciones concurrentes incompatibles se
serializan: la segunda ve la fila de la primera y se rechaza con
`ConflictoHorario`. La prueba de persistencia
`Crear_ConcurrentIncompatibleAttempts_LeavesOnlyOneReservation` verifica que
solo queda una fila.

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

## Operaciones pendientes del módulo

Todavía **no** están implementadas y no forman parte de esta tarea:

- Endpoints de reservas y su exposición protegida.
- Integración completa del flujo solicitud–aprobación–reserva.
- Prueba de integración de ese flujo completo.

## Fuera del alcance de la práctica 3

No forman parte de esta entrega ni se consideran pendientes obligatorios de la
práctica 3:

- Notificaciones propias del dominio (RF-NEG-07 / RF-NOT-03): pieza de
  Notificaciones (S11-S12).
- Reportes agregados (RF-NEG-08 / RF-REP-02): pieza de Reportes (S12).
- Adjuntos de documentos (RF-NEG-10): pieza de Documentos (S9).

Por eso RF-NEG-02 (cinco funcionalidades separables) y RF-NEG-06 (operaciones
protegidas por Control de Acceso) quedan **en desarrollo**: la creación y las
consultas existen como servicios probados, pero faltan los endpoints
protegidos y la integración completa del flujo.
