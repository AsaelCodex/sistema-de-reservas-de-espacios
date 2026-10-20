# Matriz de Requisitos — Sistema de Reservas de Espacios

## Estados

* **Pendiente:** todavía no se ha implementado.
* **En desarrollo:** se está trabajando en el requisito.
* **Implementado:** existe la implementación, pero falta verificarla.
* **Verificado:** la implementación pasó el criterio de aceptación.
* **Bloqueado:** existe una dependencia que impide terminarlo.

## Requisitos de diseño

| ID    | Requisito                                                            | Etapa            | Estado    | Evidencia |
| ----- | -------------------------------------------------------------------- | ---------------- | --------- | --------- |
| RD-01 | Componentes del Core con responsabilidad única e interfaz explícita. | Todo el proyecto | Pendiente | —         |
| RD-02 | La lógica de negocio no está en presentación/manejadores.            | Todo el proyecto | Pendiente | —         |
| RD-03 | El Core no depende del módulo de negocio.                            | Todo el proyecto | Pendiente | —         |
| RD-04 | Las transiciones de estado se resuelven en un solo punto.            | S4/S8            | Pendiente | —         |
| RD-05 | Contraseñas almacenadas con hash.                                    | S2-S4            | Pendiente | —         |
| RD-06 | Autorización verificada del lado del servidor.                       | Todo el proyecto | Pendiente | —         |
| RD-07 | Toda entrada externa es validada.                                    | Todo el proyecto | Pendiente | —         |
| RD-08 | Errores sin exposición de información interna.                       | Todo el proyecto | Pendiente | —         |
| RD-09 | Persistencia de datos fuera del proceso.                             | Todo el proyecto | Pendiente | —         |
| RD-10 | Credenciales mediante variables de entorno.                          | Todo el proyecto | Pendiente | —         |
| RD-11 | Criterio uniforme para fechas y horas.                               | Todo el proyecto | Pendiente | —         |
| RD-12 | Cada pieza puede probarse independientemente.                        | Todo el proyecto | Pendiente | —         |

## Control de Acceso

| ID       | Requisito                                                    | Etapa     | Estado    | Evidencia |
| -------- | ------------------------------------------------------------ | --------- | --------- | --------- |
| RF-CA-01 | Registro con correo único.                                   | S2-S4     | Verificado | Tests `RegisterUser_WithNewEmail_CreatesUser` y `RegisterUser_WithExistingEmail_ReturnsConflict` (SistemaDeReservas.Tests) + endpoint `POST /api/usuarios` (409 en correo duplicado) |
| RF-CA-02 | Contraseña almacenada con hash.                              | S2-S4     | Verificado | Tests `Hash_ReturnsValueDifferentFromPassword`, `Verificar_WithOriginalPassword_ReturnsTrue` y `RegisterUser_StoresPasswordHashedNotInPlainText` (SistemaDeReservas.Tests) + `Pbkdf2PasswordHasher` |
| RF-CA-03 | Inicio de sesión con credencial de sesión.                   | S2-S4     | Verificado | Tests `Iniciar_WithValidCredentials_ReturnsPersistedSessionToken` e `Iniciar_WithWrongPassword_ReturnsSameResponseAsUnknownEmail` (SistemaDeReservas.Tests) + endpoint `POST /api/sesiones` |
| RF-CA-04 | Roles Administrador y Estándar.                              | S2-S4     | Verificado | Tests `Roles_ContainsExactlyAdministradorAndEstandar`, `NewUser_DefaultsToEstandarRole` y `RegisterUser_AssignsEstandarRole` (SistemaDeReservas.Tests) + `Rol` con omisión `Estandar` en `Usuario` |
| RF-CA-05 | Cada operación declara el rol requerido.                     | S2-S4     | Verificado | Atributo `RequiereRol` y `DeclaracionOperaciones.ObtenerNivel` (API); tests `AllOperations_DeclareRequiredRole` y `Operations_DeclareExpectedLevel` (SistemaDeReservas.Tests) |
| RF-CA-06 | Estándar no puede ejecutar operaciones de Administrador.     | S2-S4     | Verificado | Filtro global `AutorizacionRolFilter` (lado del servidor); tests `AdminOperation_AsEstandarUser_ReturnsExplicitRejection`, `AdminOperation_WithoutSession_ReturnsUnauthorized` y `AdminOperation_AsAdministrator_IsAllowed` (SistemaDeReservas.Tests) |
| RF-CA-07 | Consulta del usuario autenticado y su rol.                   | S2-S4     | Verificado | Tests `Consultar_WithValidSession_ReturnsUserAndRole`, `Consultar_WithoutValidSessionHeader_ReturnsRechazado`, `Consultar_WithUnknownToken_ReturnsRechazado` y `Consultar_WithExpiredSession_ReturnsRechazado` (SistemaDeReservas.Tests) + endpoint `GET /api/usuarios/yo` |
| RF-CA-08 | Cambio de rol reservado al Administrador.                    | S2-S4     | Verificado | Endpoint `PUT /api/usuarios/{id}/rol` con `RequiereRol(Administrador)` + `AutorizacionRolFilter`; tests `EstandarUser_ChangingRol_IsRejectedByServer` y `Administrator_ChangingRol_AllowsAndUpdates` (SistemaDeReservas.Tests) |
| RF-CA-09 | Inicio de recuperación mediante correo.                      | S2-S4     | Verificado | Endpoint `POST /api/recuperacion-contrasena` con `RequiereRol(Publico)` + `RecuperacionContrasenaService.IniciarAsync`; tests `Post_WithRegisteredAndUnknownEmail_ReturnsIdenticalResponse` y `Post_WithInvalidEmail_ReturnsBadRequest` (SistemaDeReservas.Tests) |
| RF-CA-10 | Código de recuperación de un solo uso y vencimiento.         | S2-S4/S11 | Verificado | Entidad `CodigoRecuperacion` (`UsadoEn` + `VencimientoEn` + `Intentos`, límite 3 en `RecuperacionContrasena.LimiteIntentos`) + encola de correo (RF-NOT-08); el canje exige el correo del usuario y cuenta intentos fallidos; tests `Post_WithRegisteredEmail_GeneratesCodeAndEnqueuesEmail`, `Cambiar_WithCodeOfAnotherUser_ReturnsBadRequest`, `Cambiar_WithUnknownEmail_ReturnsBadRequest` y `Cambiar_AfterTooManyFailedAttempts_RejectsEvenTheCorrectCode` (SistemaDeReservas.Tests) |
| RF-CA-11 | Restablecimiento con código válido.                          | S2-S4     | Verificado | `ResultadoCambioContrasena` + `CambiarContrasenaAsync` (hash con `Pbkdf2PasswordHasher`, aplica RF-CA-14) en `POST api/recuperacion-contrasena/cambio`; tests `Cambiar_WithValidCode_ChangesPasswordAndMarksCodeUsed`, `Cambiar_MakesOldPasswordStopWorkingForLogin`, `Cambiar_WithUsedCode_ReturnsBadRequest` y `Cambiar_WithExpiredCode_ReturnsBadRequest` (SistemaDeReservas.Tests) |
| RF-CA-12 | Invalidación de sesiones anteriores al cambio de contraseña. | S2-S4     | Verificado | `ISesionRepository.EliminarPorUsuarioAsync` tras el canje en `CambiarContrasenaAsync`; test `CambiarContrasena_RejectsSessionIssuedBeforeChange` rechaza la credencial previa y acepta la nueva (SistemaDeReservas.Tests) |
| RF-CA-13 | Administrador puede forzar restablecimiento.                 | S4        | Verificado | `RestablecerContrasenaService` + `PUT api/usuarios/{id}/contrasena` (Administrador): genera un código de un solo uso y encola el correo al usuario; el usuario define la contraseña nueva con el flujo RF-CA-11 (aplica RF-CA-14 y RF-CA-12); auditoría RF-AUD-05 diferida a la pieza de auditoría; tests `Admin_ForcedReset_EnqueuesCodeWithoutChangingPassword` y `Admin_ForcedReset_WithRedemption_OldStopsWorkingAndSessionsRejected` (SistemaDeReservas.Tests) |
| RF-CA-14 | Política mínima de contraseña (8+ caracteres, letras y números). | S2-S4 | Verificado | Tests `PoliticaContraseñaTests` y `RegisterUser_WithPasswordThatViolatesPolicy_ReturnsDatosInvalidos` (SistemaDeReservas.Tests) + `PoliticaContraseña` |
| RF-CA-15 | Usuario inactivo al nacer + enlace de activación con token de un solo uso y vencimiento, enviado por cola. | S2-S4 | Verificado | Tests `RegisterUser_BornsInactive`, `RegisterUser_GeneratesSingleUseActivationTokenWithExpiration`, `RegisterUser_EnqueuesActivationEmailInsteadOfSendingIt` e `Iniciar_BeforeActivation_ReturnsCuentaNoActivaWithoutOpeningSession` (SistemaDeReservas.Tests) + migración `AddTokenActivacionYCorreosEnCola` |
| RF-CA-16 | Activación por enlace: la cuenta se activa; el enlace repetido o vencido se rechaza sin cambiar el estado. | S2-S4 | Verificado | Tests `Activar_WithValidToken_ActivatesAccount`, `Activar_Twice_RejectsSecondAttemptWithoutChangingState` y `Activar_WithExpiredToken_RejectsAndKeepsAccountInactive` (SistemaDeReservas.Tests), más `Iniciar_AfterActivation_OpensSession`, + endpoint `GET /activar?token=` |
| RF-CA-17 | Reenvío del enlace de activación con respuesta idéntica exista o no el correo; el reenvío invalida el enlace anterior. | S2-S4 | Verificado | Tests `Reenviar_WithRegisteredEmail_InvalidatesPreviousLinkAndEnqueuesNewOne` y `Reenviar_ReturnsSameResponseWhetherOrNotEmailExists` (SistemaDeReservas.Tests) + endpoint `POST /activar/reenviar` |
| RF-CA-18 | Cierre de sesión: la credencial cerrada deja de servir. | S2-S4 | Verificado | Tests `Cerrar_WithValidSession_DestroysCredential`, `Cerrar_Twice_RejectsSecondClose` y `Delete_WithoutValidSession_ReturnsUnauthorized` (SistemaDeReservas.Tests) + endpoint `DELETE /api/sesiones` |
| RF-CA-19 | Tras 5 intentos fallidos consecutivos, la cuenta queda bloqueada 15 minutos. | S2-S4 | Verificado | Tests `Iniciar_AfterFiveFailedAttempts_BlocksAccount`, `Iniciar_DuringLockout_RejectsEvenWithCorrectPassword`, `Iniciar_AfterSuccessfulLogin_ResetsFailureCounter` y `Post_AfterFiveFailures_ReturnsForbiddenOnSixthAttempt` (SistemaDeReservas.Tests) + migración `AddBloqueoIntentos` |
| RF-CA-20 | Un Administrador desactiva y reactiva usuarios. | S2-S4 | Verificado | Endpoint `PUT /api/usuarios/{id}/estado` con `RequiereRol(Administrador)` + revocación de sesiones (`ISesionRepository.EliminarPorUsuarioAsync`); tests `Admin_DeactivatesUser_SessionsStopWorkingAndLoginIsBlocked` y `Admin_CannotDeactivateSelf_ReturnsForbidden` (SistemaDeReservas.Tests) |
| RF-CA-21 | Un Administrador lista los usuarios con su rol y su estado. | S2-S4 | Verificado | Endpoint `GET /api/usuarios` con `RequiereRol(Administrador)` + `ListadoUsuariosService`; tests `Get_ReturnsAllUsersWithRolAndEstado`, `Get_NeverIncludesHashesNorTokens` y `EstandarUser_ListingUsers_IsRejectedByServer` (SistemaDeReservas.Tests) |
| RF-CA-22 | Un usuario con sesión cambia su propia contraseña indicando la actual. | S2-S4 | Verificado | `CambioContrasenaPropiaService` + `PUT api/usuarios/yo/contrasena` (Autenticado; verifica la contraseña actual, aplica RF-CA-14 y RF-CA-12); tests `Cambiar_WithCorrectCurrentPassword_UpdatesPasswordAndInvalidatesSession` y `Cambiar_WithIncorrectCurrentPassword_ReturnsBadRequest` (SistemaDeReservas.Tests) |

## Gestión de permisos

| ID       | Requisito                                                                        | Etapa | Estado    | Evidencia |
| -------- | -------------------------------------------------------------------------------- | ----- | --------- | --------- |
| RF-GP-01 | Un usuario Estándar crea una solicitud de permiso.                               | S6-S8 | Verificado | Tests `Crear_WithKnownPermiso_CreatesRequestInPendingState`, `Post_AsEstandarUser_CreatesPendingRequest` y `Post_AsAdministrator_IsAlsoAllowed` (SistemaDeReservas.Tests) + endpoint `POST /api/solicitudes-permisos` (201, estado `Pendiente`); rechazos `Post_WithoutSession_ReturnsUnauthorized` (RD-06), `Post_WithUnknownPermiso_ReturnsBadRequest` (RD-07) y duplicados D-GP-05 (`Post_WithDuplicatePendingRequest_ReturnsConflict`); decisiones en `docs/gestion-permisos.md` |
| RF-GP-02 | Un Administrador consulta las solicitudes pendientes.                            | S6-S8 | Verificado | Tests `ListarPendientes_ReturnsOnlyPendingRequests`, `GetPendientes_AsAdministrator_ReturnsOnlyPendingRequests` y `GetPendientes_AsEstandarUser_IsRejectedByServer` (SistemaDeReservas.Tests) + endpoint `GET /api/solicitudes-permisos/pendientes` (Administrador); el Estándar solo consulta las suyas con `GET /api/solicitudes-permisos/mias` (tests `ListarPropias_NeverIncludesOtherUsersRequests` y `GetMias_AsEstandarUser_ReturnsOnlyOwnRequests`, decisión D-GP-04) |
| RF-GP-03 | Un Administrador aprueba una solicitud pendiente.                                | S6-S8 | Verificado | `ResolucionSolicitudService.AprobarAsync` (Business/GestionPermisos) registra `AprobadorId` y `ResueltaEn` y persiste la solicitud como `Aplicada` en una sola operación (D-GP-06); tests `Aprobar_FromPendingRequest_RecordsApproverAndDate`, `Aprobar_FromPendingRequest_PersistsRequestAsAplicada` y `Aprobar_AppliedRequest_IsRejectedAndKeepsState` (SistemaDeReservas.Tests); la exposición por API a Administrador llega con el endpoint `PUT /api/solicitudes-permisos/{id}/aprobar` del cierre de la pieza |
| RF-GP-04 | Un Administrador rechaza una solicitud pendiente indicando el motivo.            | S6-S8 | Pendiente | —         |
| RF-GP-05 | Al aprobar, el sistema aplica el cambio de permiso automáticamente.              | S6-S8 | Pendiente | —         |
| RF-GP-06 | Solo se permiten las transiciones de la tabla de transiciones.                   | S6-S8 | Verificado | Componentes `TransicionesSolicitudPermiso` (tabla oficial en Core) y `TransicionSolicitudPermisoService`; tests `TransicionesSolicitudPermisoTests` y `TransicionSolicitudPermisoServiceTests` (SistemaDeReservas.Tests): las 16 combinaciones de estados se prueban; las 13 prohibidas —incluidas `Rechazada → Aplicada` y `Aplicada → Aprobada` (aprobar dos veces)— se rechazan y el estado persistido no cambia |
| RF-GP-07 | Nadie resuelve su propia solicitud.                                              | S6-S8 | Pendiente | —         |

Las decisiones de diseño de esta pieza (catálogo de permisos, duplicados,
visibilidad y base de administradores) están en `docs/gestion-permisos.md`.

## Módulo de negocio — Reservas de espacios

| ID        | Requisito                                         | Etapa    | Estado    | Evidencia |
| --------- | ------------------------------------------------- | -------- | --------- | --------- |
| RF-NEG-01 | Al menos cuatro entidades relacionadas.           | S4-S8    | Pendiente | —         |
| RF-NEG-02 | Al menos cinco funcionalidades separables.        | S4-S8    | Pendiente | —         |
| RF-NEG-03 | Máquina de estados propia de 3-5 estados.         | S4/S8    | Verificado | `EstadoReserva` (5 estados) y la tabla de transiciones `TransicionesReserva` en un solo lugar (`Core/Reservations`) + `TransicionReservaService`; tests `EstadoReserva_DeclaresBetweenThreeAndFiveStates` y `Transicionar_FromSolicitadaToConfirmada_ChangesState` (SistemaDeReservas.Tests); tabla en `docs/maquina-estados-negocio.md` |
| RF-NEG-04 | Al menos una transición prohibida.                | S4/S8    | Verificado | Toda combinación no listada en `TransicionesReserva.EsValida` se rechaza sin cambiar el estado; tests `Transicionar_FromSolicitadaToFinalizada_IsRejectedAndKeepsState` y `Transicionar_FromRechazadaToConfirmada_IsRejected` (SistemaDeReservas.Tests) |
| RF-NEG-05 | Al menos un estado terminal.                      | S4/S8    | Verificado | `Rechazada`, `Cancelada` y `Finalizada` sin transiciones de salida (`TransicionesReserva.EsTerminal`); tests `TerminalStates_HaveNoOutgoingTransitions` y `Transicionar_FromFinalizada_IsRejected` (SistemaDeReservas.Tests) |
| RF-NEG-06 | Operaciones protegidas por Control de Acceso.     | Desde S4 | Pendiente | —         |
| RF-NEG-07 | Al menos una notificación propia del dominio.     | S11-S12  | Pendiente | —         |
| RF-NEG-08 | Al menos un reporte agregado del dominio.         | S12      | Pendiente | —         |
| RF-NEG-09 | Máquina de estados independiente de permisos.     | S4/S8    | Verificado | Máquina en `Core/Reservations` + `Business/Reservas`, sin dependencias con Gestión de permisos (Pieza 2 sin implementar): la estructura está descrita en `docs/maquina-estados-negocio.md` |
| RF-NEG-10 | Adjuntos opcionales; si se usan, cumplen Pieza 3. | S9       | Pendiente | —         |

## Notificaciones y cola de correos

| ID        | Requisito                                         | Etapa    | Estado    | Evidencia |
| --------- | ------------------------------------------------- | -------- | --------- | --------- |
| RF-NOT-01 | Crear una solicitud de permiso notifica a los Administradores. | S11-S12 | Pendiente | —         |
| RF-NOT-02 | Resolver una solicitud de permiso notifica al solicitante. | S11-S12 | Pendiente | —         |
| RF-NOT-03 | Al menos un evento propio del módulo de negocio dispara una notificación. | S11-S12 | Pendiente | —         |
| RF-NOT-04 | Consultar las notificaciones propias, distinguiendo leídas de no leídas. | S11-S12 | Pendiente | —         |
| RF-NOT-05 | Marcar una notificación como leída.               | S11-S12 | Pendiente | —         |
| RF-NOT-06 | Toda notificación se entrega por dos canales.     | S11-S12 | Pendiente | —         |
| RF-NOT-07 | El destinatario puede desactivar el canal de correo sin perder el canal interno. | S11-S12 | Pendiente | —         |
| RF-NOT-08 | Los correos se encolan en la operación que los origina. | S11-S12 | Verificado | Los 4 servicios de Control de Acceso encolan con `IColaCorreosRepository.EncolarAsync` (estado `Pendiente`) sin enviar; tests `RegisterUser_EnqueuesActivationEmailInsteadOfSendingIt` y `Post_WithRegisteredEmail_GeneratesCodeAndEnqueuesEmail` (SistemaDeReservas.Tests) |
| RF-NOT-09 | Un proceso independiente toma los pendientes y los envía. | S11-S12 | Verificado | `ColaCorreosWorker` (`BackgroundService`, intervalo `Correo:IntervaloSegundos`) + `ProcesadorColaCorreos` + puerto `IEnvioCorreo` (`SmtpEnvioCorreo` o `ArchivoEnvioCorreo` de respaldo); tests `Procesar_SendsPendingEmailsAndMarksThemSent`, `ProcesarAsync_SendsQueuedEmailsWithoutRerunningTheOriginatingFlow` y `Enviar_WritesProcessedMessageToOutputDirectory` (SistemaDeReservas.Tests) |
| RF-NOT-10 | Reintentos limitados y luego marcado como fallido. | S11-S12 | Verificado | `ProcesadorColaCorreos.LimiteIntentos = 3`: en cada fallo `Intentos++` y `UltimoError`; al límite `Estado = Fallido` sin reintentos posteriores; tests `Procesar_WhenSendFails_IncrementsAttemptsAndKeepsPending` y `Procesar_WhenAttemptsReachLimit_MarksFailedAndStopsRetrying` (SistemaDeReservas.Tests) |
| RF-NOT-11 | Estado, intentos, fechas y último error consultables por un Administrador. | S11-S12 | Verificado | Entidad `CorreoEnCola` + endpoint `GET /api/correos` con `RequiereRol(Administrador)` + `ListadoCorreosService`; tests `Get_ReturnsQueueWithStatesAttemptsAndErrors`, `Get_WithoutSession_ReturnsUnauthorized` y `Get_AsEstandarUser_IsRejectedByServer` (SistemaDeReservas.Tests) |
| RF-NOT-12 | Un correo enviado no se vuelve a enviar.          | S11-S12 | Verificado | El procesador solo toma `Estado = Pendiente` y lo marca `Enviado` con `FechaEnvio`; test `Procesar_Twice_DoesNotSendTheSameEmailAgain` (SistemaDeReservas.Tests) |
| RF-NOT-13 | Credenciales del servidor de correo por variables de entorno. | S11-S12 | Verificado | `Program.cs` lee `CorreoSmtp:Host/Puerto/Usuario/Password/De/Ssl` (en producción, env vars `CorreoSmtp__*`); sin secretos en el repositorio (RD-10) |

## Evidencia

Cuando un requisito sea verificado, registrar algo concreto:

* Prueba automatizada.
* Endpoint.
* Clase o componente.
* Commit.
* Pull Request.
* Otro artefacto comprobable.

### Ejemplo

| ID       | Estado     | Evidencia                                                     |
| -------- | ---------- | ------------------------------------------------------------- |
| RF-CA-01 | Verificado | Test `RegisterUser_WithExistingEmail_ReturnsConflict` + PR #X |

## Regla

Esta matriz sirve para controlar el progreso. La fuente de verdad de los requisitos sigue siendo `docs/requerimientos-core.md`, basado en el documento oficial del curso.