# Gestión de permisos (Pieza 2) — Decisiones de diseño

**Requisitos oficiales:** RF-GP-01 a RF-GP-07 (`docs/requerimientos-core.md`, sección 4)
**Etapa:** semanas 6 a 8 (Asignación 2, Práctica 2)
**Fuente de verdad:** el documento oficial. Este archivo registra decisiones de
diseño del proyecto y **no modifica ni amplía ningún requisito oficial**: las
reglas propias del proyecto se identifican con el prefijo D-GP-XX.

---

## 1. Catálogo de permisos (D-GP-01)

Existe un catálogo de permisos propio, declarado en un solo lugar:

```csharp
// SistemaDeReservas.Core/Permissions/Permiso.cs
public enum Permiso
{
    CrearReserva   // primer permiso concreto; el catálogo es extensible
}
```

Reglas derivadas:

- El **permiso** es una capacidad de negocio; el **rol** (RF-CA-04) sigue
  siendo el mecanismo de control de acceso. Ninguna operación de Control de
  Acceso pasa a exigir permisos: así no se duplican RF-CA-05 ni RF-CA-06.
- `Permiso.CrearReserva` es solo un identificador en `Core/Permissions`: este
  namespace **no** referencia `Core/Reservations` ni ningún otro del módulo de
  negocio (RD-03, RF-NEG-09).

## 2. Máquina de estados (D-GP-06)

Transiciones oficiales (copia literal de `requerimientos-core.md`, sección 4):

| Desde | Hacia | Quién la ejecuta | Condición |
|---|---|---|---|
| Pendiente | Aprobada | Administrador | La solicitud está en Pendiente. |
| Aprobada | Aplicada | El sistema | Automática, inmediatamente después de aprobar. |
| Pendiente | Rechazada | Administrador | Exige un motivo escrito. |
| Cualquier otra combinación | — | — | Prohibida. El sistema la rechaza y el estado no cambia. |

- Componente único: `SistemaDeReservas.Core/Permissions/TransicionesSolicitudPermiso.cs`
  (`EsValida`, `DestinosPosibles`, `EsTerminal`), espejo de
  `TransicionesReserva` (RD-04).
- `Aprobada` es un estado atravesado **dentro de la misma operación** de
  aprobación: `AprobarAsync` valida `Pendiente → Aprobada` y `Aprobada →
  Aplicada` en el mismo llamado y persiste la solicitud como `Aplicada` con
  `AprobadorId`, `ResueltaEn` (RF-GP-03) y el `PermisoOtorgado` creado
  (RF-GP-05). Aprobar dos veces fracasa porque la solicitud ya está `Aplicada`
  (RF-GP-06).
- Semántica de `AprobadorId` (esquema oficial: un único campo de resolutor):
  registra al administrador que **resolvió** la solicitud, sea aprobando
  (RF-GP-03) o rechazando (RF-GP-04), siempre junto a `ResueltaEn`. Al
  rechazar, el motivo es obligatorio (`MotivoRechazo`, RD-07) y no se crea
  `PermisoOtorgado`; al aprobar, `MotivoRechazo` queda `null`. En una
  solicitud sin resolver el campo es `null`. No existe un campo «rechazador»
  aparte: así se evita que la semántica dependa de la pieza de Auditoría.
- Las máquinas de Gestión de permisos y de reservas permanecen independientes:
  no comparten estados ni componentes (RF-NEG-09).

## 3. Decisiones

| ID | Decisión | Justificación |
|---|---|---|
| D-GP-01 | Catálogo `enum Permiso { CrearReserva }` en Core. | RF-GP-01/05; separación permiso (negocio) vs. rol (RF-CA-04). |
| D-GP-02 | Los permisos otorgados se persisten en tabla nueva `PermisoOtorgado(UsuarioId, Permiso, SolicitudPermisoId, OtorgadoEn)` con FK al `Usuario` creada **desde la tabla nueva** (mismo patrón que `Reserva` en `AppDbContext`) e índice único `(UsuarioId, Permiso)`. No se modifica `Core/Users/Usuario.cs`. | RF-GP-05, RD-09; cero riesgo para RF-CA-01 a RF-CA-22. |
| D-GP-03 | RF-GP-05 se verifica con `AutorizacionPermisosService.PuedeAsync(usuarioId, permiso)`. La futura operación de creación de reservas inyectará este servicio. **No se crean endpoints de reservas en esta pieza.** | RF-GP-05, RD-01, RD-12; no inventar funcionalidad fuera de alcance. Ver §4. |
| D-GP-04 | Visibilidad: `GET /api/solicitudes-permisos/mias` (Autenticado, filtrado por la sesión) y `GET /api/solicitudes-permisos/pendientes` (Administrador). | RF-GP-02 y su criterio "un usuario Estándar no puede ver las solicitudes de otros usuarios". |
| D-GP-05 | Duplicados: (a) una única solicitud `Pendiente` por usuario y permiso; (b) no se solicita un permiso ya concedido **explícitamente** (existe `PermisoOtorgado`); los permisos implícitos del Administrador (D-GP-07) no bloquean la solicitud; (c) tras un rechazo sí se permite una nueva solicitud. | El documento oficial no trata duplicados: son reglas propias, validadas en servicio (RD-07) e índice único filtrado en BD. El matiz de (b) es lo que hace que RF-GP-07 siga siendo provocable vía API con D-GP-07. |
| D-GP-06 | Máquina de estados idéntica a la tabla oficial, en un solo componente de Core (§2). | RF-GP-06, RD-04, RF-NEG-09. |
| D-GP-07 | El rol `Administrador` implica todos los permisos del catálogo: `PuedeAsync` devuelve `true` sin consultar `PermisoOtorgado`; el resto de usuarios consultan la tabla. **Punto de cambio:** cuando existan al menos dos administradores reales, puede pasarse a "todos requieren `PermisoOtorgado`" cambiando únicamente `AutorizacionPermisosService`, sus pruebas y este documento (entidades, tablas y servicios no cambian). El momento adecuado es **antes** de que exista la operación real de creación de reservas. | RF-GP-05; evita que un administrador único quede bloqueado (no puede aprobar su propia solicitud por RF-GP-07). |
| D-GP-08 | La base de datos arranca con **dos cuentas de administrador** creadas por un seeder idempotente (§5). | Hace verificable RF-GP-07 de extremo a extremo y habilita la demostración de RF-GP-02/03/04/06 por API. |

## 4. Verificación de RF-GP-05 (D-GP-03)

Criterio oficial: "Tras la aprobación el solicitante puede ejecutar la acción
que pidió, sin ninguna intervención manual adicional."

Evidencia de esta pieza: tras `AprobarAsync`, el permiso está concedido y es
comprobable de inmediato con `AutorizacionPermisosService.PuedeAsync(...)` en
pruebas automatizadas, sin ninguna acción posterior. La **ejecución real** de
la acción (`POST /api/reservas`) quedará demostrada cuando exista el módulo de
reservas; ese servicio deberá consultar `AutorizacionPermisosService` en lugar
de duplicar la comprobación. Este alcance de la evidencia queda declarado para
no sobrevender el requisito en la matriz de trazabilidad.

**Consistencia de la persistencia:** `AprobarAsync` persiste la solicitud
resuelta y su `PermisoOtorgado` en una sola operación
(`ISolicitudPermisoRepository.GuardarResolucionAsync`, un único `SaveChanges`
de EF). Si la inserción del permiso falla —por ejemplo, por el índice único
`(UsuarioId, Permiso)`— no se persiste **ningún** cambio: la solicitud sigue
`Pendiente`, sin aprobador ni fecha, y no queda el estado inconsistente
"Aplicada sin permiso" que permitían dos guardadas separadas. Verificación:
`Aprobar_WhenGrantCannotBeStored_PersistsNoChangesAtAll` en
`AprobarAplicaPermisoTests`, más la prueba versionada
`Aprobar_WhenUniqueIndexViolated_PersistsNoChanges`
(`SistemaDeReservas.Tests/Persistencia`), que provoca el duplicado contra
SQL Server LocalDB real (una base por prueba) y comprueba con un contexto
nuevo que la solicitud sigue `Pendiente`, sin aprobador ni fecha, y que
existe un solo `PermisoOtorgado`.

También queda afirmado el caso negativo: una solicitud `Rechazada`, una
resolución prohibida (doble aprobación) o una solicitud inexistente **no
crean** `PermisoOtorgado` (`ResolucionSolicitudServiceTests`, que observa el
repositorio de permisos directamente), y en SQL Server lo confirma
`Aprobar_RejectedRequest_PersistsNoGrant` (cero `PermisoOtorgado` en la base,
leído con un contexto nuevo).

## 5. Base de dos administradores (D-GP-08)

**Situación verificada en el código actual:**

- No existe ningún seed (`Seed`, `EnsureCreated`, `.Migrate(` no aparecen en el
  código; las migraciones se aplican manualmente con `dotnet ef database update`).
- `RegistroUsuarioService.cs:72-73` crea siempre usuarios con
  `Rol = Rol.Estandar` y `Activo = false`.
- `CambioRolService` solo es invocable con rol Administrador
  (`AutorizacionRolFilter`).

Conclusión: hoy **no hay forma de crear el primer administrador** por la API, y
sin administradores la pieza no puede demostrarse por endpoints.

**Decisión:** seeder idempotente que, al iniciar la API (tras aplicar las
migraciones), crea dos cuentas de administrador **solo si sus correos no
existen**:

- `Activo = true`: el seeder **no es** la operación de registro
  (`POST /api/usuarios`), por lo que no recorre el enlace de activación de
  RF-CA-15. Se declara explícitamente para que no se le exija ese criterio.
- Contraseña guardada con `IPasswordHasher` (`Pbkdf2PasswordHasher`):
  RF-CA-02 / RD-05.
- Correos y contraseñas **solo por variables de entorno**
  (`Seed__Administradores__0__Correo`, `Seed__Administradores__1__Correo`,
  `Seed__Password`): RD-10. Si no están definidas, el seeder no crea nada y la
  API arranca igual.
- Idempotente por correo único (RF-CA-01): reiniciar no duplica cuentas.

**Uso para RF-GP-07:** el administrador A crea su propia solicitud (D-GP-05(b)
no se lo impide) e intenta aprobarla o rechazarla → recibe el rechazo exigido
por el criterio; el administrador B sí puede resolverla. Con dos
administradores además ninguno queda bloqueado si se aplica el "punto de
cambio" de D-GP-07.

**Interacción con RF-CA-20:** cada administrador no puede desactivarse a sí
mismo, pero sí al otro; coherente con el requisito, sin cambios.

## 6. Endpoints previstos

| Endpoint | Nivel (`RequiereRol`) | Requisitos |
|---|---|---|
| `POST /api/solicitudes-permisos` | Autenticado (la sesión se resuelve en el controller, patrón `UsuariosController.Get`) | RF-GP-01 |
| `GET /api/solicitudes-permisos/mias` | Autenticado | D-GP-04, RF-GP-02 (parcial) |
| `GET /api/solicitudes-permisos/pendientes` | Administrador | RF-GP-02, RF-CA-06 |
| `PUT /api/solicitudes-permisos/{id}/aprobar` | Administrador | RF-GP-03, RF-GP-05, RF-GP-06, RF-GP-07 |
| `PUT /api/solicitudes-permisos/{id}/rechazar` | Administrador | RF-GP-04, RF-GP-06, RF-GP-07 |

Toda acción declara `[RequiereRol]` (RF-CA-05) y el filtro global hace el
rechazo del lado del servidor (RF-CA-06). Las validaciones de entrada
(permiso desconocido, motivo ausente, identificadores inválidos) producen
respuestas controladas (RD-07, RD-08).

## 7. Componentes previstos

| Capa | Archivos |
|---|---|
| Core (`Core/Permissions/`) | `Permiso.cs`, `EstadoSolicitudPermiso.cs`, `SolicitudPermiso.cs`, `PermisoOtorgado.cs`, `TransicionesSolicitudPermiso.cs`, `ISolicitudPermisoRepository.cs`, `IPermisoOtorgadoRepository.cs` |
| Business (`Business/GestionPermisos/`) | `CrearSolicitudPermisoService.cs` + resultado, `ListadoSolicitudesService.cs`, `TransicionSolicitudPermisoService.cs` + resultado, `ResolucionSolicitudService.cs` + resultado, `AutorizacionPermisosService.cs` |
| Infrastructure | `SolicitudPermisoRepository.cs`, `PermisoOtorgadoRepository.cs`, `DbSet` en `AppDbContext`, migración `AddSolicitudesPermisos`, seeder de administradores |
| API | `SolicitudesPermisosController.cs`, `Models/CrearSolicitudPermisoRequest.cs`, `Models/RechazarSolicitudPermisoRequest.cs`, registros de DI en `Program.cs` |
| Tests | `Fakes/FakeSolicitudPermisoRepository.cs`, `Fakes/FakePermisoOtorgadoRepository.cs`, carpeta `GestionPermisos/` y `API/SolicitudesPermisosTests.cs` |

## 8. Trazabilidad

| Requisito | Decisiones | Verificación principal |
|---|---|---|
| RF-GP-01 | D-GP-01, D-GP-05 | `CrearSolicitudPermisoTests` (nace `Pendiente`) |
| RF-GP-02 | D-GP-04, D-GP-08 | `SolicitudesPermisosTests` (Admin 200 / Estándar 403 / sin sesión 401) |
| RF-GP-03 | D-GP-06 | `ResolucionSolicitudServiceTests` (registra aprobador y fecha, persiste `Aplicada`; doble aprobación y rechazada no avanzan) + `SolicitudResolucionPersistenciaTests.Aprobar_PersistsRequestAndGrantInAnotherContext` (SQL Server, contexto nuevo) |
| RF-GP-04 | D-GP-06 | `ResolucionSolicitudServiceTests.Rechazar_*` (sin motivo no se completa; con motivo persiste `MotivoRechazo` y el resolutor) + `SolicitudResolucionPersistenciaTests.Rechazar_WithMotivo_PersistsReasonInAnotherContext` (SQL Server, contexto nuevo) |
| RF-GP-05 | D-GP-03, D-GP-07 | `AprobarAplicaPermisoTests` + `AutorizacionPermisosServiceTests` (§4), incluida la persistencia atómica de solicitud y permiso, `ResolucionSolicitudServiceTests` (rechazos, prohibiciones e inexistencia sin `PermisoOtorgado`) y `SolicitudResolucionPersistenciaTests` + `PermisoOtorgadoPersistenciaTests` (SQL Server real: contexto nuevo, duplicados, índice único y rollback total; §4) |
| RF-GP-06 | D-GP-06 | `TransicionesSolicitudPermisoTests` (tabla) y `TransicionSolicitudPermisoServiceTests` (las 16 combinaciones, aprobación doble y `Rechazada → Aplicada` rechazadas sin cambiar el estado); en SQL Server, `Aprobar_Twice_KeepsOneGrantAndFirstApprover` y `Aprobar_RejectedRequest_PersistsNoGrant` (contexto nuevo) |
| RF-GP-07 | D-GP-05(b), D-GP-08 | `ResolucionPropiaTests` (aprobar y rechazar la propia solicitud se rechazan; otro administrador sí puede resolverla) + prueba de API con los dos administradores |

## 9. Fuera de alcance y puntos señalados

- **No se modifica `docs/requerimientos-core.md`** (documento oficial).
- **RF-NOT-01/02** (notificar al crear y al resolver) y **RF-AUD-02**
  (auditar resolución) se implementarán en sus piezas (S11-S12 y S14); los
  nombres y parámetros de los servicios no cierran esa puerta. No se
  implementan ahora.
- **RF-REP-01** (solicitudes por estado y período) se apoyará en `CreadaEn` y
  `ResueltaEn` de `SolicitudPermiso`.
- **Punto de cambio de D-GP-07** documentado en §3.
- **Pendiente de confirmación:** si se desea persistir también el estado
  intermedio `Aprobada` (dos guardados con transacción) además del enfoque
  actual de un solo guardado con estado final `Aplicada`.
- El seeder (D-GP-08) es un componente nuevo justificado por la verificación
  de RF-GP-07 y RF-GP-02/03/04/06 (RD-12); no forma parte de los requisitos
  oficiales de la pieza.
