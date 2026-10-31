# Máquina de estados del negocio (Reserva)

Requisitos: RF-NEG-03, RF-NEG-04, RF-NEG-05 y RF-NEG-09. Decisiones
centralizadas según RD-04. Las operaciones que usarán esta máquina están en
`docs/contrato-negocio.md`.

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

## Transiciones permitidas

Tabla única en `SistemaDeReservas.Core/Reservations/TransicionesReserva.cs`.
Cada fila incluye la condición que debe cumplir el actor; la primera fila
permitida con condición es `Solicitada → Confirmada` (Administrador).

| Desde | Hacia | Condición |
|---|---|---|
| Solicitada | Confirmada | El actor es Administrador. |
| Solicitada | Rechazada | El actor es Administrador. |
| Solicitada | Cancelada | El actor es propietario de la reserva o Administrador. |
| Confirmada | Finalizada | El actor es Administrador. |
| Confirmada | Cancelada | El actor es propietario de la reserva o Administrador. |

## Transiciones prohibidas

Toda combinación que no aparezca en la tabla anterior está prohibida, incluidas
las transiciones al mismo estado. El sistema la rechaza y el estado no cambia
(RF-NEG-04).

| # | Desde | Hacia |
|---:|---|---|
| 1 | Solicitada | Finalizada |
| 2 | Solicitada | Solicitada |
| 3 | Confirmada | Solicitada |
| 4 | Confirmada | Confirmada |
| 5 | Confirmada | Rechazada |
| 6 | Rechazada | Solicitada |
| 7 | Rechazada | Confirmada |
| 8 | Rechazada | Rechazada |
| 9 | Rechazada | Cancelada |
| 10 | Rechazada | Finalizada |
| 11 | Cancelada | Solicitada |
| 12 | Cancelada | Confirmada |
| 13 | Cancelada | Rechazada |
| 14 | Cancelada | Cancelada |
| 15 | Cancelada | Finalizada |
| 16 | Finalizada | Solicitada |
| 17 | Finalizada | Confirmada |
| 18 | Finalizada | Rechazada |
| 19 | Finalizada | Cancelada |
| 20 | Finalizada | Finalizada |

## Estados terminales (RF-NEG-05)

`Rechazada`, `Cancelada` y `Finalizada` no tienen transiciones de salida:
`TransicionesReserva.EsTerminal` devuelve `true` y `DestinosPosibles` está vacío
para ellos.

## Independencia (RF-NEG-09)

La máquina vive en `Core/Reservations` y `Business/Reservas` con sus
propios tipos (`EstadoReserva`, `TransicionesReserva`,
`TransicionReservaService`). No comparte estados ni transiciones con la
máquina de Gestión de permisos (Pieza 2): cambiar los estados de una no
obliga a cambiar los de la otra.

## Lógica centralizada (RD-04)

- `Core/Reservations/EstadoReserva.cs`: los cinco estados.
- `Core/Reservations/TransicionesReserva.cs`: la tabla de transiciones, las
  condiciones del actor y la decisión (`Permitida`, `Prohibida`,
  `CondicionIncumplida`).
- `Core/Reservations/ActorReserva.cs`: el actor que ejecuta la operación,
  construido desde la sesión validada.
- `Business/Reservas/TransicionReservaService.cs`: ejecuta la decisión de la
  tabla; no repite reglas ni estados.

Ningún controlador, servicio ni documento de configuración duplica la tabla.

## Cómo se aplica

`TransicionReservaService.TransicionarAsync(reservaId, destino, actor)`
consulta la reserva y evalúa la transición contra la tabla única. Un rechazo
por transición inválida, por condición incumplida o por actor inválido:

- devuelve un resultado controlado (`TransicionProhibida`,
  `CondicionIncumplida` o `ActorInvalido`) con mensaje comprensible;
- conserva el estado original de la reserva;
- no ejecuta la actualización del repositorio.

Un actor ausente, sin identificador (`Guid.Empty`) o con un rol fuera del enum
se rechaza como `ActorInvalido` antes de evaluar la tabla. Una reserva
inexistente devuelve `ReservaNoEncontrada` sin tocar el repositorio. La reserva
nace en `Solicitada` y se persiste en la tabla `Reservas` (migración
`AddReservas`).
