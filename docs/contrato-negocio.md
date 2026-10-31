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
transición, aunque su identificador coincida con el propietario de la reserva.

## Operaciones previstas

| Operación | Autorización | Estado en la práctica 3 |
|---|---|---|
| Crear reserva | Usuario autenticado; la operación de creación consulta `AutorizacionPermisosService` para `Permiso.CrearReserva`. | Futura: no implementada. |
| Consultar reservas propias | Usuario autenticado; solo sus reservas. | Futura: no implementada. |
| Consultar reservas para gestión | Administrador. | Futura: no implementada. |
| Confirmar reserva | Administrador. | Regla de transición implementada; operación y endpoint futuros. |
| Rechazar reserva | Administrador. | Regla de transición implementada; operación y endpoint futuros. |
| Cancelar reserva | Propietario de la reserva o Administrador. | Regla de transición implementada; operación y endpoint futuros. |
| Finalizar reserva | Administrador. | Regla de transición implementada; operación y endpoint futuros. |

La consulta del permiso `Permiso.CrearReserva` pertenece a la futura operación
de creación. El servicio de transiciones no la evalúa: mezclar ambas
responsabilidades duplicaría reglas y rompería RD-01.

## Reglas implementadas en esta tarea

| Elemento | Ubicación | Requisito |
|---|---|---|
| Los cinco estados de la reserva | `Core/Reservations/EstadoReserva.cs` | RF-NEG-03 |
| Tabla única de transiciones con condiciones del actor | `Core/Reservations/TransicionesReserva.cs` | RF-NEG-04, RD-04 |
| Actor construido desde la sesión validada | `Core/Reservations/ActorReserva.cs` | RF-NEG-06 |
| Ejecución de la transición y rechazos controlados | `Business/Reservas/TransicionReservaService.cs` | RF-NEG-05, RD-07 |

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

- Creación de reservas y consulta del permiso `Permiso.CrearReserva`.
- Consulta de reservas propias y consulta para gestión.
- Endpoints de reservas y su exposición protegida.
- Integración completa reservas–permisos.

## Fuera del alcance de la práctica 3

No forman parte de esta entrega ni se consideran pendientes obligatorios de la
práctica 3:

- Notificaciones propias del dominio (RF-NEG-07 / RF-NOT-03): pieza de
  Notificaciones (S11-S12).
- Reportes agregados (RF-NEG-08 / RF-REP-02): pieza de Reportes (S12).
- Adjuntos de documentos (RF-NEG-10): pieza de Documentos (S9).

Por eso RF-NEG-02 (cinco funcionalidades separables) y RF-NEG-06 (operaciones
protegidas por Control de Acceso) quedan **en desarrollo**: el contrato y la
regla de transición existen, pero faltan las operaciones y su exposición.
