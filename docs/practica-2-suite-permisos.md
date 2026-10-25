# Práctica 2 — Una suite que detecta defectos: Gestión de permisos

Rama: `feature/practica-2-suite-permisos`

## 1. Alcance

Esta práctica revisa y completa la suite de pruebas del módulo de
**Gestión de permisos** (RF-GP-01 a RF-GP-07), sin rehacer funcionalidades
existentes:

- Verificar que existe al menos una prueba por requisito, con el ID
  identificado en el nombre o en un comentario, conservando los traits
  existentes.
- Reutilizar las pruebas que ya comprobaban el comportamiento y añadir o
  reforzar únicamente las que faltaban.
- Validar que la suite detecta los seis defectos publicados de la
  **variante A**, introduciéndolos temporalmente uno por uno sobre la base
  correcta y restaurando el código antes del siguiente.

No se refactorizó la arquitectura, no se cambiaron reglas de negocio y no
se agregaron funcionalidades.

## 2. Relación RF-GP ↔ pruebas

Todos los nombres indicados llevan además el comentario `// RF-GP-0X: …`
y el trait `[Trait("Requerimiento", "RF-GP-0X")]`.

### RF-GP-01 — El Estándar crea una solicitud y nace Pendiente

| Prueba | Qué comprueba |
|---|---|
| `CrearSolicitudPermisoServiceTests.Crear_WithKnownPermiso_CreatesRequestInPendingState` | Servicio: estado `Pendiente`, sin aprobador ni fecha. |
| `SolicitudesPermisosTests.Post_AsEstandarUser_CreatesPendingRequest` | API: un Estándar crea la solicitud (201) y nace Pendiente. |
| `SolicitudesPermisosTests.Post_AsAdministrator_IsAlsoAllowed` | API: el Administrador también puede crearla. |

### RF-GP-02 — El Administrador consulta pendientes; el Estándar solo lo propio

| Prueba | Qué comprueba |
|---|---|
| `ListadoSolicitudesServiceTests.ListarPendientes_ReturnsOnlyPendingRequests` | La cola de pendientes excluye las resueltas. |
| `ListadoSolicitudesServiceTests.ListarPendientes_WithNoRequests_ReturnsEmpty` | Cola vacía. |
| `ListadoSolicitudesServiceTests.ListarPropias_NeverIncludesOtherUsersRequests` | Listado propio con **dos solicitantes**: contiene los propios y excluye los ajenos. |
| `SolicitudesPermisosTests.GetPendientes_AsAdministrator_ReturnsOnlyPendingRequests` | API: el Administrador ve solo pendientes. |
| `SolicitudesPermisosTests.GetMias_AsEstandarUser_ReturnsOnlyOwnRequests` | API: el Estándar no ve solicitudes de otros usuarios. |
| `ListadoSolicitudesPersistenciaTests.ListarPropias_WithTwoApplicants_ReturnsOnlyOwnRequests` | Repositorio EF real sobre LocalDB, dos solicitantes, tres propias y dos ajenas. |

### RF-GP-03 — Al aprobar se registra quién y cuándo

| Prueba | Qué comprueba |
|---|---|
| `ResolucionSolicitudServiceTests.Aprobar_FromPendingRequest_RecordsApproverAndDate` | Quedan `AprobadorId` y `ResueltaEn`. |
| `ResolucionSolicitudServiceTests.Aprobar_FromApprovedRequest_IsRejectedAndKeepsFirstApproval` | La aprobación posterior conserva el **primer** aprobador y la primera fecha. |
| `SolicitudesPermisosTests.PutAprobar_AsAdministrator_AppliesRequestAndGrantsPermission` | API: aprobador, fecha, estado Aplicada y permiso. |
| `SolicitudResolucionPersistenciaTests.Aprobar_PersistsRequestAndGrantInAnotherContext` | Aprobador, fecha y permiso persisten en SQL Server (contexto nuevo). |

### RF-GP-04 — El rechazo exige motivo y lo guarda

| Prueba | Qué comprueba |
|---|---|
| `ResolucionSolicitudServiceTests.Rechazar_WithoutMotivo_DoesNotCompleteRejection` | Teoría con motivo ausente, vacío y solo espacios: no se completa y el estado no cambia. |
| `ResolucionSolicitudServiceTests.Rechazar_WithMotivo_PersistsRejectionAndReason` | Motivo válido guardado (recortado) con resolutor y fecha. |
| `SolicitudesPermisosTests.PutRechazar_WithoutMotivo_ReturnsBadRequest` | API: motivo de solo espacios → 400 y sigue Pendiente. |
| `SolicitudesPermisosTests.PutRechazar_AsAdministrator_RejectsWithMotivo` | API: motivo válido guardado. |
| `SolicitudResolucionPersistenciaTests.Rechazar_WithMotivo_PersistsReasonInAnotherContext` | El motivo persiste en SQL Server (contexto nuevo). |

### RF-GP-05 — Al aprobar la solicitud llega a Aplicada y el permiso es efectivo

| Prueba | Qué comprueba |
|---|---|
| `AprobarAplicaPermisoTests.Aprobar_GrantsPermissionSoApplicantCanActWithoutFurtherSteps` | Estado `Aplicada` y el solicitante puede actuar sin pasos adicionales. |
| `AprobarAplicaPermisoTests.Aprobar_RegistersGrantWithItsOrigin` | El permiso queda registrado con su origen. |
| `AprobarAplicaPermisoTests.Aprobar_DoesNotGrantPermissionToOtherUsers` | El permiso es del solicitante. |
| `AprobarAplicaPermisoTests.Aprobar_WhenGrantCannotBeStored_PersistsNoChangesAtAll` | Si el permiso no se guarda, no persiste ningún cambio. |
| `AutorizacionPermisosServiceTests.Puede_*` (4 pruebas) | Con permiso → sí; sin permiso o de otro usuario → no; Administrador → sí. |
| `ResolucionSolicitudServiceTests.Aprobar_RejectedRequest_IsRejectedAndKeepsState` | Una solicitud Rechazada no crea permiso. |
| `SolicitudesPermisosTests.PutAprobar_AsAdministrator_AppliesRequestAndGrantsPermission` | Fin del camino por API. |
| `PermisoOtorgadoPersistenciaTests.*` (3 pruebas) | Índice único y atomicidad en la base real. |
| `SolicitudResolucionPersistenciaTests.Aprobar_PersistsRequestAndGrantInAnotherContext`, `Aprobar_RejectedRequest_PersistsNoGrant` | Permiso presente/ausente en SQL Server. |

### RF-GP-06 — Solo las tres transiciones oficiales

| Prueba | Qué comprueba |
|---|---|
| `TransicionesSolicitudPermisoTests.EsValida_WithTransitionFromOfficialTable_AllowsIt` | Las 3 transiciones oficiales se permiten. |
| `TransicionesSolicitudPermisoTests.EsValida_WithCombinationOutsideTable_DeniesIt` | Las **13** combinaciones fuera de la tabla se rechazan, incluidas `Rechazada → Aplicada` y `Aprobada → Pendiente`. |
| `TransicionesSolicitudPermisoTests.ResolvedStates_HaveNoOutgoingTransitions` | `Rechazada` y `Aplicada` no tienen salidas. |
| `TransicionSolicitudPermisoServiceTests.Transicionar_WithTransitionFromTable_ChangesAndPersistsState` | Las 3 oficiales cambian y persisten el estado. |
| `TransicionSolicitudPermisoServiceTests.Transicionar_WithCombinationOutsideTable_IsRejectedAndKeepsState` | Cada combinación prohibida se rechaza y **conserva el estado** (incluidas `Rechazada → Aplicada` y `Aprobada → Pendiente`). |
| `TransicionSolicitudPermisoServiceTests.Transicionar_ApprovedTwice_SecondAttemptIsRejectedAndKeepsState` | `Aprobada → Aprobada` no existe. |
| `ResolucionSolicitudServiceTests.Aprobar_FromApprovedRequest_IsRejectedAndKeepsFirstApproval` | Aprobar desde `Aprobada` se rechaza conservando aprobador, fecha y permisos. |
| `ResolucionSolicitudServiceTests.Aprobar_AppliedRequest_IsRejectedAndKeepsFirstApproval` | Aprobar desde `Aplicada` se rechaza conservando aprobador, fecha y permisos. |
| `ResolucionSolicitudServiceTests.Aprobar_RejectedRequest_IsRejectedAndKeepsState` | `Rechazada` no vuelve a aprobarse. |
| `ResolucionSolicitudServiceTests.Rechazar_AppliedRequest_IsRejectedAndKeepsState` | `Aplicada → Rechazada` se rechaza. |
| `ResolucionSolicitudServiceTests.Rechazar_Twice_KeepsFirstRejectionAndMotivo` | Se conserva el primer motivo y el primer resolutor. |
| `AprobarAplicaPermisoTests.Aprobar_Twice_DoesNotCreateASecondGrant` | La segunda aprobación no crea un segundo permiso. |
| `SolicitudesPermisosTests.PutAprobar_AppliedRequest_ReturnsConflict` | API: 409 y sin permiso adicional. |
| `SolicitudResolucionPersistenciaTests.Aprobar_Twice_KeepsOneGrantAndFirstApprover`, `Aprobar_RejectedRequest_PersistsNoGrant` | Un solo permiso y primer aprobador en la base real. |

### RF-GP-07 — Nadie resuelve su propia solicitud

| Prueba | Qué comprueba |
|---|---|
| `ResolucionPropiaTests.Aprobar_OwnRequest_IsRejectedWithoutChanges` | `ResolucionPropia`; sigue Pendiente, sin aprobador, fecha ni permisos. |
| `ResolucionPropiaTests.Rechazar_OwnRequest_IsRejectedWithoutChanges` | `ResolucionPropia`; sigue Pendiente, sin motivo ni permisos. |
| `ResolucionPropiaTests.Aprobar_RequestOfAnotherAdministrator_IsAllowed` | La regla compara IDs, no roles: otro Administrador sí puede. |
| `SolicitudesPermisosTests.PutAprobar_OwnRequest_ReturnsForbiddenAndOtherAdminCanApprove` | API: 403 y sin cambios; el otro Administrador aprueba. |
| `SolicitudesPermisosTests.PutRechazar_OwnRequest_ReturnsForbiddenAndOtherAdminCanReject` | API: 403 y sin cambios; el otro Administrador rechaza. |

## 3. Relación defectos A1…A6 ↔ pruebas que fallaron

Cada defecto se introdujo **uno por uno** sobre el código correcto, con la
suite sin modificar, y se restauró con `git checkout -- <archivo>` antes
del siguiente. En ningún caso hubo errores de compilación ni de
infraestructura: todos los fallos fueron aserciones o excepciones de
negocio.

### A1 — RF-GP-06: «el estado no es Rechazada» en `AprobarAsync`

Archivo: `Business/GestionPermisos/ResolucionSolicitudService.cs`
Cambio: `!EsValida(solicitud.Estado, Aprobada)` → `solicitud.Estado == Rechazada`.

**5 pruebas fallaron:**

| Prueba | Motivo del fallo |
|---|---|
| `ResolucionSolicitudServiceTests.Aprobar_FromApprovedRequest_IsRejectedAndKeepsFirstApproval` | `Assert.Equal`: esperado `TransicionProhibida`, obtenido `Aprobada`. |
| `ResolucionSolicitudServiceTests.Aprobar_AppliedRequest_IsRejectedAndKeepsFirstApproval` | `InvalidOperationException`: «Ya existe un PermisoOtorgado para ese usuario y permiso». |
| `AprobarAplicaPermisoTests.Aprobar_Twice_DoesNotCreateASecondGrant` | `InvalidOperationException`: duplicado en el repositorio falso (espejo del índice único). |
| `SolicitudesPermisosTests.PutAprobar_AppliedRequest_ReturnsConflict` | Se esperaba 409; la aprobación repetida siguió adelante. |
| `SolicitudResolucionPersistenciaTests.Aprobar_Twice_KeepsOneGrantAndFirstApprover` | `DbUpdateException` por el índice único `(UsuarioId, Permiso)` en SQL Server. |

### A2 — RF-GP-06: `Rechazada → Aplicada` permitida

Archivo: `Core/Permissions/TransicionesSolicitudPermiso.cs`
Cambio: `[Rechazada] = Array.Empty<…>()` → `[Rechazada] = [Aplicada]`.

**3 pruebas fallaron:**

| Prueba | Motivo del fallo |
|---|---|
| `TransicionesSolicitudPermisoTests.EsValida_WithCombinationOutsideTable_DeniesIt(Rechazada, Aplicada)` | `Assert.False` recibió `true`. |
| `TransicionSolicitudPermisoServiceTests.Transicionar_WithCombinationOutsideTable_IsRejectedAndKeepsState(Rechazada, Aplicada)` | Esperado `TransicionProhibida`, obtenido `Transicionada`. |
| `TransicionesSolicitudPermisoTests.ResolvedStates_HaveNoOutgoingTransitions` | `Rechazada` dejó de ser estado final. |

### A3 — RF-GP-07: eliminada la verificación de resolución propia

Archivo: `Business/GestionPermisos/ResolucionSolicitudService.cs`
Cambio: eliminados los dos bloques `if (solicitud.SolicitanteId == …Id) return ResolucionPropia();`.

**4 pruebas fallaron:**

| Prueba | Motivo del fallo |
|---|---|
| `ResolucionPropiaTests.Aprobar_OwnRequest_IsRejectedWithoutChanges` | Esperado `ResolucionPropia`, obtenido `Aprobada`. |
| `ResolucionPropiaTests.Rechazar_OwnRequest_IsRejectedWithoutChanges` | Esperado `ResolucionPropia`, obtenido `Rechazada`. |
| `SolicitudesPermisosTests.PutAprobar_OwnRequest_ReturnsForbiddenAndOtherAdminCanApprove` | Esperado 403, obtenido 200. |
| `SolicitudesPermisosTests.PutRechazar_OwnRequest_ReturnsForbiddenAndOtherAdminCanReject` | Esperado 403, obtenido 200. |

### A4 — RF-GP-02: filtro de solicitudes propias invertido

Archivo: `Infrastructure/Repositories/SolicitudPermisoRepository.cs`
Cambio: `Where(s => s.SolicitanteId == solicitanteId)` → `!=`.

**1 prueba falló:**

| Prueba | Motivo del fallo |
|---|---|
| `ListadoSolicitudesPersistenciaTests.ListarPropias_WithTwoApplicants_ReturnsOnlyOwnRequests` | `Assert.Equal`: esperado 3 (las propias), obtenido 2 (las ajenas). |

El defecto vive en la consulta EF real, no en el servicio: las pruebas de
unidad usan `FakeSolicitudPermisoRepository` y no lo alcanzaban. Por eso se
añadió esta única prueba de persistencia (ver «Limitaciones»).

### A5 — RF-GP-06: `Aprobada → Pendiente` permitida

Archivo: `Core/Permissions/TransicionesSolicitudPermiso.cs`
Cambio: `[Aprobada] = [Aplicada]` → `[Aplicada, Pendiente]`.

**2 pruebas fallaron:**

| Prueba | Motivo del fallo |
|---|---|
| `TransicionSolicitudPermisoServiceTests.Transicionar_WithCombinationOutsideTable_IsRejectedAndKeepsState(Aprobada, Pendiente)` | Esperado `TransicionProhibida`, obtenido `Transicionada`. |
| `TransicionesSolicitudPermisoTests.EsValida_WithCombinationOutsideTable_DeniesIt(Aprobada, Pendiente)` | `Assert.False` recibió `true`. |

### A6 — RF-GP-04: eliminado el guardado del motivo

Archivo: `Business/GestionPermisos/ResolucionSolicitudService.cs`
Cambio: eliminada la línea `solicitud.MotivoRechazo = motivo.Trim();`.

**4 pruebas fallaron:**

| Prueba | Motivo del fallo |
|---|---|
| `ResolucionSolicitudServiceTests.Rechazar_WithMotivo_PersistsRejectionAndReason` | `MotivoRechazo`: esperado «…», obtenido `null`. |
| `ResolucionSolicitudServiceTests.Rechazar_Twice_KeepsFirstRejectionAndMotivo` | «primer motivo» → `null`. |
| `SolicitudesPermisosTests.PutRechazar_AsAdministrator_RejectsWithMotivo` | El motivo devuelto por API no quedó guardado. |
| `SolicitudResolucionPersistenciaTests.Rechazar_WithMotivo_PersistsReasonInAnotherContext` | El motivo no persiste en SQL Server. |

Los seis defectos fueron **detectados**; ninguno sobrevivió a la suite.

## 4. Comandos ejecutados y resultados reales

Comando único (desde la raíz del repositorio):

```
dotnet test SistemaDeReservas/SistemaDeReservas.sln
```

Para los registros de la validación se añadieron los flags
`--nologo -v q --logger "console;verbosity=detailed"`, sin cambiar el
alcance de la ejecución.

| # | Ejecución | Resultado observado |
|---|---|---|
| 1 | Suite base, antes de los cambios | 292 correctas, 0 con error, 0 omitidas |
| 2 | Suite tras completar/reforzar las pruebas (código correcto) | **303 correctas, 0 con error, 0 omitidas** |
| 3 | Con A1 introducido | **5 fallidas** de 303 (ver A1) |
| 4 | Con A2 introducido | **3 fallidas** de 303 (ver A2) |
| 5 | Con A3 introducido | **4 fallidas** de 303 (ver A3) |
| 6 | Con A4 introducido | **1 fallida** de 303 (ver A4) |
| 7 | Con A5 introducido | **2 fallidas** de 303 (ver A5) |
| 8 | Con A6 introducido | **4 fallidas** de 303 (ver A6) |
| 9 | Ejecución final, código correcto y árbol limpio | **303 correctas, 0 con error, 0 omitidas** |
| 10 | Ejecución sin LocalDB (sonda apuntando a un servidor inexistente) | 294 correctas, 0 fallidas, **9 omitidas**, total 303 |

Detalle:

- Las 303 pruebas se componen de **294 unitarias** (servicios, controladores
  y fakes, sin infraestructura externa) y **9 de persistencia**
  (`[LocalDbFact]`) que requieren SQL Server LocalDB.
- Sin LocalDB, esas 9 se **omiten** con un mensaje claro; ninguna falla.
  Las 294 restantes siguen ejecutándose.
- Los fallos de A1…A6 se registraron en
  `%TEMP%\practica2\A1.log` … `A6.log`.
- Tras cada mutación el archivo se restauró y se comprobó
  `git status --porcelain` limpio antes de pasar al siguiente.

## 5. Limitaciones y verificaciones pendientes

1. **A4 depende de LocalDB.** El filtro «solo mis solicitudes» vive en
   `SolicitudPermisoRepository`, y solo una prueba de persistencia lo
   ejercita contra EF real. En un entorno sin LocalDB esa prueba se omite y
   el defecto A4 **no se detectaría** (294 pruebas restantes permanecerían
   verdes). Ejecutar con `REQUIERE_PERSISTENCIA=1` para convertir las
   omisiones en fallos cuando la persistencia sea obligatoria.
2. **Tres de los cinco fallos de A1 se manifiestan por excepción**
   (`InvalidOperationException` / `DbUpdateException` del índice único)
   y no por una aserción de estado. Detectan el defecto igualmente, porque
   la aprobación repetida avanza hasta intentar crear un permiso duplicado,
   pero el mensaje de fallo es el de la restricción de integridad.
3. **A1 también se detecta por aserción** en
   `Aprobar_FromApprovedRequest_IsRejectedAndKeepsFirstApproval`
   (`TransicionProhibida` vs `Aprobada`), que es la señal más directa.
4. No se añadieron pruebas de BD por requisito: solo se creó
   `ListadoSolicitudesPersistenciaTests` porque es la única evidencia
   posible para A4.
5. No se crearon tags `practica-2` ni `practica-2-defecto-N`, no se hizo
   `push` y no quedó ningún defecto temporal en el código.
