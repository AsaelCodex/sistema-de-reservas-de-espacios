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

**Revisión posterior (esta versión del documento):** se confirmó la
dependencia de LocalDB de la detección de A4 y sus tres modos de
ejecución, se reforzaron las aserciones de aislamiento de la prueba de A4
(comparación exacta de identificadores), se confirmó el desglose de los
5 fallos de A1 reproduciéndolo, y se corrigió la clasificación de los
casos de prueba (métodos frente a casos) y el conteo de archivos
afectados. Las ejecuciones de la validación inicial y las de esta
revisión se listan por separado en la sección 4.

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
| `ListadoSolicitudesPersistenciaTests.ListarPropias_WithTwoApplicants_ReturnsOnlyOwnRequests` | Repositorio EF real sobre LocalDB, **dos solicitantes** con filas persistidas (3 propias y 2 ajenas): tamaño exacto, todo lo devuelto pertenece al usuario, coincidencia exacta de identificadores con las propias (sin orden impuesto), ninguna fila ajena y listado del segundo solicitante exactamente el suyo. |

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
del siguiente. En ningún caso hubo errores de compilación: todos los
fallos fueron aserciones o excepciones provocadas por el comportamiento
defectuoso, nunca fallos de infraestructura de la máquina de pruebas.

### A1 — RF-GP-06: «el estado no es Rechazada» en `AprobarAsync`

Archivo: `Business/GestionPermisos/ResolucionSolicitudService.cs`
Cambio: `!EsValida(solicitud.Estado, Aprobada)` → `solicitud.Estado == Rechazada`.

**5 pruebas fallaron** (1 por aserción directa y 4 por excepción):

| Prueba | Tipo de fallo | Motivo observado |
|---|---|---|
| `ResolucionSolicitudServiceTests.Aprobar_FromApprovedRequest_IsRejectedAndKeepsFirstApproval` | **Aserción** | `Assert.Equal() Failure: Values differ` — `Expected: TransicionProhibida`, `Actual: Aprobada`. |
| `ResolucionSolicitudServiceTests.Aprobar_AppliedRequest_IsRejectedAndKeepsFirstApproval` | Excepción | `System.InvalidOperationException : Ya existe un PermisoOtorgado para ese usuario y permiso.` (repositorio falso, espejo del índice único). |
| `AprobarAplicaPermisoTests.Aprobar_Twice_DoesNotCreateASecondGrant` | Excepción | La misma `InvalidOperationException` del repositorio falso. |
| `SolicitudesPermisosTests.PutAprobar_AppliedRequest_ReturnsConflict` | Excepción | La misma `InvalidOperationException` del repositorio falso (la aprobación repetida llegó a intentar crear el permiso duplicado en vez de devolver 409). |
| `SolicitudResolucionPersistenciaTests.Aprobar_Twice_KeepsOneGrantAndFirstApprover` | Excepción | `Microsoft.EntityFrameworkCore.DbUpdateException` → `SqlException: Cannot insert duplicate key row … index 'IX_PermisosOtorgados_UsuarioId_Permiso'` en SQL Server. |

Desglose confirmado: **1 fallo por aserción + 4 por excepción**
(3 `InvalidOperationException` del repositorio falso y 1
`DbUpdateException`/`SqlException` de LocalDB). Los cuatro por excepción
siguen siendo detección: el comportamiento defectuoso hizo avanzar la
aprobación hasta chocar con la restricción de integridad; en ningún caso
fue un error de compilación.

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
| `ListadoSolicitudesPersistenciaTests.ListarPropias_WithTwoApplicants_ReturnsOnlyOwnRequests` | `Assert.Equal() Failure: Values differ` — `Expected: 3`, `Actual: 2` (primera aserción de resultados, el tamaño exacto). |

El defecto vive en la consulta EF real, no en el servicio: las pruebas
sin LocalDB usan `FakeSolicitudPermisoRepository` y no lo alcanzaban. Por
eso se añadió esta única prueba de persistencia (ver «Limitaciones»).

#### Aserciones concretas de aislamiento de la prueba

La prueba crea **dos solicitantes** y persiste cinco filas con el
repositorio EF real (tres del usuario que consulta y dos del otro), y
después lista con un contexto nuevo e independiente del que escribió:

1. `Assert.Equal(propias.Length, resultado.Count)` — tamaño exacto:
   ni las ajenas amplían la lista ni faltan propias.
2. `Assert.All(resultado, s => Assert.Equal(propio.Id, s.SolicitanteId))`
   — todo lo devuelto pertenece a quien consulta.
3. Comparación de conjuntos de identificadores **ordenados a ambos lados**
   (`esperados.OrderBy(id => id)` vs `resultado.Select(s => s.Id).OrderBy(id => id)`):
   coincidencia exacta, sin solicitudes ajenas ni elementos faltantes, sin
   depender del orden del listado (el contrato `ObtenerPorSolicitanteAsync`
   ordena por `CreadaEn` descendente, pero la prueba no lo exige).
4. `Assert.DoesNotContain(resultado, s => s.SolicitanteId == ajeno.Id)`
   — ninguna fila del otro solicitante aparece.
5. El mismo listado consultado **del otro solicitante** devuelve
   exactamente sus dos identificadores, con `Assert.All` sobre su
   `SolicitanteId`: confirma que ambos solicitantes tienen filas
   persistidas y que el filtro es simétrico.

#### Evidencia de detección (revisado)

- Con el código correcto: la prueba pasa (1/1 correcta).
- Introducido **solo** el cambio A4 en el repositorio real y sin tocar las
  pruebas: la misma prueba **falla por aserción de resultados**
  (`Expected: 3 / Actual: 2`), no por compilación ni por infraestructura.
- Restaurado exactamente ese cambio (`git checkout -- …/SolicitudPermisoRepository.cs`):
  la prueba vuelve a pasar y `git diff` de producción queda vacío.

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

Las filas 1 a 10 corresponden a la **validación inicial** de los defectos
(registros en `%TEMP%\practica2\A1.log` … `A6.log`).

### Ejecuciones de esta revisión (posteriores a la validación inicial)

| # | Ejecución | Resultado observado | Tipo |
|---|---|---|---|
| 11 | Prueba A4 con el código correcto (tras reforzar sus aserciones) | 1 correcta de 1 | Comportamiento |
| 12 | Prueba A4 con **solo** A4 introducido | **Fallida por aserción**: `Expected: 3 / Actual: 2` | Comportamiento |
| 13 | Prueba A4 tras restaurar A4 | 1 correcta de 1 | Comportamiento |
| 14 | Suite completa con **solo** A1 introducido (A4 reforzada ya incluida) | 303 totales: **298 correctas, 5 incorrectas**, 0 omitidas — desglose de A1 confirmado | Comportamiento |
| 15 | Suite sin LocalDB, modo normal (sonda `LOCALDB_PROBE_DATASOURCE=servidor-inexistente`) | 303 totales: **294 correctas, 0 incorrectas, 9 omitidas** | Omisión |
| 16 | Suite sin LocalDB, `REQUIERE_PERSISTENCIA=1` | 303 totales: **294 correctas, 9 incorrectas** — `InvalidOperationException` de `BasePruebaPersistencia.InitializeAsync` (LocalDB no disponible) | **Infraestructura** |
| 17 | Suite completa con LocalDB y `REQUIERE_PERSISTENCIA=1`, código correcto | **303 correctas, 0 incorrectas, 0 omitidas** | Comportamiento |
| 18 | Repetición de la corrida estricta (tras editar solo comentarios del código de prueba) | 302 correctas / **1 incorrecta**: `PermisoOtorgadoPersistenciaTests.Esquema_CreaIndiceUnicoDeUsuarioYPermiso` con `DbUpdateException` → `SqlException` de **tiempo de espera agotado** (55 s) | **Infraestructura** (transitorio) |
| 19 | Re-ejecución inmediata de la corrida estricta | **303 correctas, 0 incorrectas, 0 omitidas** | Comportamiento |
| 20 | Verificación final, corrida 1 (LocalDB + `REQUIERE_PERSISTENCIA=1`) | **303 correctas, 0 incorrectas, 0 omitidas** | Comportamiento |
| 21 | Verificación final, corrida 2 (LocalDB + `REQUIERE_PERSISTENCIA=1`) | **303 correctas, 0 incorrectas, 0 omitidas** | Comportamiento |
| 22 | Cierre, corrida de base con LocalDB + `REQUIERE_PERSISTENCIA=1` (`cierre-base-1.log`) | **303 correctas, 0 incorrectas, 0 omitidas** | Comportamiento |
| 23 | Cierre, mutante **A4** con LocalDB + `REQUIERE_PERSISTENCIA=1` (`cierre-A4.log`) | 303 totales: 302 correctas / **1 incorrecta** — `ListadoSolicitudesPersistenciaTests.ListarPropias_WithTwoApplicants_ReturnsOnlyOwnRequests`, `Assert.Equal(): Expected: 3, Actual: 2`, **0 omitidas** | Comportamiento (atribuible a A4) |
| 24 | Cierre, corrida de base tras restaurar A4 (`cierre-base-2.log`) | **303 correctas, 0 incorrectas, 0 omitidas** | Comportamiento |

Registros de esta revisión: `%TEMP%\practica2\A1-revision.log`,
`estricto-sin-localdb.log`, `final-estricto.log`, `final-2.log`,
`final-3.log`, `verificacion-final-1.log`, `verificacion-final-2.log`,
`cierre-base-1.log`, `cierre-A4.log` y `cierre-base-2.log`. Las variables
`LOCALDB_PROBE_DATASOURCE` y `REQUIERE_PERSISTENCIA` se definieron y se
eliminaron dentro de la misma sesión de PowerShell; el entorno quedó con
ambas sin definir.

La fila 18 es un **fallo de infraestructura** (timeout de SQL Server
LocalDB, sin relación con el código de negocio ni con esta revisión): las
corridas siguientes con el mismo código (19, 20, 21 y 22) volvieron a 303/303.
**Se conserva como corrida fallida**: no se elimina, no se repite para
reemplazarla ni se reclasifica como correcta; el timeout aislado no
justifica modificar la implementación. No cuenta como detección de
ningún defecto ni invalida las demás filas.

### Condiciones de verificación de las 7 etiquetas (pendientes de crear)

La entrega se materializará con **7 etiquetas**: `practica-2` (base
correcta) y `practica-2-defecto-1` … `practica-2-defecto-6` (un mutante
cada una). Aún no están creadas ni los commits asociados.

Condiciones acordadas para verificarlas:

1. **SQL Server LocalDB debe estar disponible y la verificación se hace
   con `REQUIERE_PERSISTENCIA=1`** en las 7 etiquetas, de modo que ninguna
   prueba necesaria para A4 (la de persistencia) quede omitida. Con
   LocalDB presente el modo estricto no altera la ejecución —solo cambia
   el comportamiento cuando falta la base (omisión → fallo de
   infraestructura)—, por lo que las corridas de la validación inicial,
   hechas con LocalDB disponible, siguen siendo válidas.
2. **Criterio de aceptación:** una ejecución completa **satisfactoria
   sobre la base** (303 correctas, 0 incorrectas, 0 omitidas; corridas 22
   y 24 ya lo cumplen) y, en cada mutante, **fallos atribuibles a ese
   defecto** con 0 omitidas (corrida 23 lo cumple para A4 con la
   configuración exigida; A1…A6 cumplieron en su validación inicial,
   filas 3 a 8, con LocalDB disponible).
3. **Toda corrida fallida se conserva como fallida** en el registro, con
   su causa (comportamiento o infraestructura).

### Entorno y modos de ejecución con LocalDB

- **Entorno mínimo** (294 casos sin LocalDB): .NET SDK 10; pruebas de
  negocio con fakes, de seguridad, de correo y de API. Esta suite no usa
  `WebApplicationFactory` ni un servidor HTTP: las pruebas de API invocan
  los controladores en memoria.
- **Entorno con persistencia** (9 casos `[LocalDbFact]`): SQL Server
  LocalDB accesible en `(localdb)\MSSQLLocalDB`; cada prueba crea y borra
  su propia base `Reservas_Tests_*`.
- **Comando único en ambos casos**:
  `dotnet test SistemaDeReservas/SistemaDeReservas.sln`.
- **Modo estricto** (PowerShell, antes del mismo comando):
  `$env:REQUIERE_PERSISTENCIA = "1"`.
- Sin LocalDB y sin modo estricto las 9 pruebas se **omiten** y A4 queda
  sin detectar; con el modo estricto **fallan por infraestructura**, y un
  fallo por ausencia de LocalDB **no constituye la detección de un
  defecto** (solo indica que la máquina de pruebas no tiene base de datos).

### Conteos: métodos frente a casos

- **213 métodos de prueba**: 179 `[Fact]`, 25 `[Theory]` y 9 `[LocalDbFact]`.
- **303 casos**: las teorías se expanden en 115 casos con `[InlineData]`
  (179 + 115 + 9 = 303).
- **294 casos sin LocalDB** (179 `[Fact]` + 115 casos de teoría) y
  **9 casos de persistencia LocalDB**. «Unitarias» no es una clasificación
  exacta aquí: hay casos que ejercitan controladores API en memoria, que
  también corren sin base de datos.

Detalle:

- Sin LocalDB, los 9 casos de persistencia se **omiten** con un mensaje
  claro; ninguno falla. Los 294 restantes siguen ejecutándose.
- Los fallos de A1…A6 de la validación inicial se registraron en
  `%TEMP%\practica2\A1.log` … `A6.log`.
- Tras cada mutación el archivo se restauró y se comprobó que `git diff`
  de los proyectos de producción quedara vacío antes de pasar al
  siguiente.

## 5. Limitaciones y verificaciones pendientes

1. **A4 depende de LocalDB.** El filtro «solo mis solicitudes» vive en
   `SolicitudPermisoRepository.ObtenerPorSolicitanteAsync`, y solo una
   prueba de persistencia lo ejercita contra EF Core real (verificado por
   búsqueda: el resto de pruebas pasan por `FakeSolicitudPermisoRepository`
   o por `ListadoSolicitudesService` con el falso). En un entorno sin
   LocalDB y en modo normal esa prueba se **omite** y el defecto A4 **no se
   detectaría** (los 294 casos restantes permanecerían verdes); con
   `REQUIERE_PERSISTENCIA=1` la prueba **falla por infraestructura**, lo
   que tampoco es una detección. A4 solo se detecta con LocalDB
   disponible, cuando su aserción de resultados falla. **No se añadieron
   proveedores de BD ni se refactorizó la persistencia** para eliminar esa
   dependencia.
2. **Cuatro de los cinco fallos de A1 se manifiestan por excepción**
   (3 `InvalidOperationException` del repositorio falso y 1
   `DbUpdateException`/`SqlException` del índice único en LocalDB) y no
   por una aserción de estado. Detectan el defecto igualmente, porque la
   aprobación repetida avanza hasta intentar crear un permiso duplicado,
   pero el mensaje de fallo es el de la restricción de integridad.
3. **El quinto fallo de A1 se detecta por aserción** en
   `Aprobar_FromApprovedRequest_IsRejectedAndKeepsFirstApproval`
   (`TransicionProhibida` vs `Aprobada`), que es la señal más directa.
   El desglose fue confirmado reproducindo A1 durante la revisión
   (ejecución 14): 298 correctas / 5 incorrectas / 0 omitidas.
4. No se añadieron pruebas de BD por requisito: solo se creó
   `ListadoSolicitudesPersistenciaTests` porque es la única evidencia
   posible para A4.
5. **No se crearon tags `practica-2` ni `practica-2-defecto-N`, no se
   hizo `push` y no quedó ningún defecto temporal en el código.** La
   entrega en Git (1 commit base + 6 commits/etiquetas de defecto) sigue
   pendiente; las condiciones de verificación quedaron descritas en la
   sección 4 («Condiciones de verificación de las 7 etiquetas»):
   LocalDB disponible y `REQUIERE_PERSISTENCIA=1` en las 7, ejecución
   satisfactoria de 303/303/0 omitidas sobre la base y fallos atribuibles
   al defecto en cada mutante.
6. **Timeout aislado de LocalDB.** La fila 18 (`Esquema_CreaIndiceUnicoDeUsuarioYPermiso`,
   `SqlException` de tiempo de espera tras 55 s) se conserva como corrida
   fallida. Es infraestructura y no justifica modificar la implementación
   ni el esquema de la prueba; se acepta solo sobre la base de contar con
   corridas satisfactorias posteriores con el mismo código (19-22 y 24).
