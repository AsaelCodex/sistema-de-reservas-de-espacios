# Máquina de estados del negocio (Reserva)

Requisitos: RF-NEG-03, RF-NEG-04, RF-NEG-05 y RF-NEG-09.

## Estados

Declarados en un solo lugar del código:
`SistemaDeReservas.Core/Reservations/EstadoReserva.cs`.

| Estado | Significado |
|---|---|
| Solicitada | Estado inicial de toda reserva. |
| Confirmada | La reserva fue confirmada y ocupa el espacio. |
| Rechazada | Terminal: la solicitud no se aceptó. |
| Cancelada | Terminal: se liberó el espacio reservado. |
| Finalizada | Terminal: la reserva ya ocurrió. |

## Transiciones

Tabla única en `SistemaDeReservas.Core/Reservations/TransicionesReserva.cs`:

| Desde | Hacia | Condición |
|---|---|---|
| Solicitada | Confirmada | Permitida. |
| Solicitada | Rechazada | Permitida. |
| Solicitada | Cancelada | Permitida. |
| Confirmada | Finalizada | Permitida. |
| Confirmada | Cancelada | Permitida. |
| Cualquier otra combinación | — | Prohibida (RF-NEG-04). El sistema la rechaza y el estado no cambia. |

Ejemplos de transiciones prohibidas que el sistema rechaza:
`Solicitada → Finalizada`, `Rechazada → Confirmada`,
`Confirmada → Confirmada` y `Finalizada → Cancelada`.

## Estados terminales (RF-NEG-05)

`Rechazada`, `Cancelada` y `Finalizada` no tienen transiciones de
salida: `TransicionesReserva.EsTerminal` devuelve `true` y
`DestinosPosibles` está vacío para ellos.

## Independencia (RF-NEG-09)

La máquina vive en `Core/Reservations` y `Business/Reservas` con sus
propios tipos (`EstadoReserva`, `TransicionesReserva`,
`TransicionReservaService`). No comparte estados ni transiciones con la
máquina de Gestión de permisos (Pieza 2): cambiar los estados de una no
obliga a cambiar los de la otra.

## Cómo se aplica

`TransicionReservaService.TransicionarAsync(reservaId, destino)` valida
el cambio contra la tabla única de transiciones. Una transición
prohibida responde `EstadoTransicion.TransicionProhibida` con mensaje
controlado y la reserva conserva su estado. La reserva nace en
`Solicitada` y se persiste en la tabla `Reservas` (migración
`AddReservas`).
