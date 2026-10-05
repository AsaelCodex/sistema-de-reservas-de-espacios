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
| RF-CA-05 | Cada operación declara el rol requerido.                     | S2-S4     | Pendiente | —         |
| RF-CA-06 | Estándar no puede ejecutar operaciones de Administrador.     | S2-S4     | Pendiente | —         |
| RF-CA-07 | Consulta del usuario autenticado y su rol.                   | S2-S4     | Verificado | Tests `Consultar_WithValidSession_ReturnsUserAndRole`, `Consultar_WithoutValidSessionHeader_ReturnsRechazado`, `Consultar_WithUnknownToken_ReturnsRechazado` y `Consultar_WithExpiredSession_ReturnsRechazado` (SistemaDeReservas.Tests) + endpoint `GET /api/usuarios/yo` |
| RF-CA-08 | Cambio de rol reservado al Administrador.                    | S2-S4     | Pendiente | —         |
| RF-CA-09 | Inicio de recuperación mediante correo.                      | S2-S4     | Pendiente | —         |
| RF-CA-10 | Código de recuperación de un solo uso y vencimiento.         | S2-S4/S11 | Pendiente | —         |
| RF-CA-11 | Restablecimiento con código válido.                          | S2-S4     | Pendiente | —         |
| RF-CA-12 | Invalidación de sesiones anteriores al cambio de contraseña. | S2-S4     | Pendiente | —         |
| RF-CA-13 | Administrador puede forzar restablecimiento.                 | S4        | Pendiente | —         |
| RF-CA-14 | Política mínima de contraseña (8+ caracteres, letras y números). | S2-S4 | Verificado | Tests `PoliticaContraseñaTests` y `RegisterUser_WithPasswordThatViolatesPolicy_ReturnsDatosInvalidos` (SistemaDeReservas.Tests) + `PoliticaContraseña` |
| RF-CA-15 | Usuario inactivo al nacer + enlace de activación con token de un solo uso y vencimiento, enviado por cola. | S2-S4 | Verificado | Tests `RegisterUser_BornsInactive`, `RegisterUser_GeneratesSingleUseActivationTokenWithExpiration`, `RegisterUser_EnqueuesActivationEmailInsteadOfSendingIt` e `Iniciar_BeforeActivation_ReturnsCuentaNoActivaWithoutOpeningSession` (SistemaDeReservas.Tests) + migración `AddTokenActivacionYCorreosEnCola` |
| RF-CA-16 | Activación por enlace: la cuenta se activa; el enlace repetido o vencido se rechaza sin cambiar el estado. | S2-S4 | Verificado | Tests `Activar_WithValidToken_ActivatesAccount`, `Activar_Twice_RejectsSecondAttemptWithoutChangingState` y `Activar_WithExpiredToken_RejectsAndKeepsAccountInactive` (SistemaDeReservas.Tests), más `Iniciar_AfterActivation_OpensSession`, + endpoint `GET /activar?token=` |
| RF-CA-17 | Reenvío del enlace de activación con respuesta idéntica exista o no el correo; el reenvío invalida el enlace anterior. | S2-S4 | Verificado | Tests `Reenviar_WithRegisteredEmail_InvalidatesPreviousLinkAndEnqueuesNewOne` y `Reenviar_ReturnsSameResponseWhetherOrNotEmailExists` (SistemaDeReservas.Tests) + endpoint `POST /activar/reenviar` |
| RF-CA-18 | Cierre de sesión: la credencial cerrada deja de servir. | S2-S4 | Verificado | Tests `Cerrar_WithValidSession_DestroysCredential`, `Cerrar_Twice_RejectsSecondClose` y `Delete_WithoutValidSession_ReturnsUnauthorized` (SistemaDeReservas.Tests) + endpoint `DELETE /api/sesiones` |
| RF-CA-19 | Tras 5 intentos fallidos consecutivos, la cuenta queda bloqueada 15 minutos. | S2-S4 | Verificado | Tests `Iniciar_AfterFiveFailedAttempts_BlocksAccount`, `Iniciar_DuringLockout_RejectsEvenWithCorrectPassword`, `Iniciar_AfterSuccessfulLogin_ResetsFailureCounter` y `Post_AfterFiveFailures_ReturnsForbiddenOnSixthAttempt` (SistemaDeReservas.Tests) + migración `AddBloqueoIntentos` |
| RF-CA-20 | Un Administrador desactiva y reactiva usuarios. | S2-S4 | Pendiente | —         |
| RF-CA-21 | Un Administrador lista los usuarios con su rol y su estado. | S2-S4 | Pendiente | —         |
| RF-CA-22 | Un usuario con sesión cambia su propia contraseña indicando la actual. | S2-S4 | Pendiente | —         |

## Módulo de negocio — Reservas de espacios

| ID        | Requisito                                         | Etapa    | Estado    | Evidencia |
| --------- | ------------------------------------------------- | -------- | --------- | --------- |
| RF-NEG-01 | Al menos cuatro entidades relacionadas.           | S4-S8    | Pendiente | —         |
| RF-NEG-02 | Al menos cinco funcionalidades separables.        | S4-S8    | Pendiente | —         |
| RF-NEG-03 | Máquina de estados propia de 3-5 estados.         | S4/S8    | Pendiente | —         |
| RF-NEG-04 | Al menos una transición prohibida.                | S4/S8    | Pendiente | —         |
| RF-NEG-05 | Al menos un estado terminal.                      | S4/S8    | Pendiente | —         |
| RF-NEG-06 | Operaciones protegidas por Control de Acceso.     | Desde S4 | Pendiente | —         |
| RF-NEG-07 | Al menos una notificación propia del dominio.     | S11-S12  | Pendiente | —         |
| RF-NEG-08 | Al menos un reporte agregado del dominio.         | S12      | Pendiente | —         |
| RF-NEG-09 | Máquina de estados independiente de permisos.     | S4/S8    | Pendiente | —         |
| RF-NEG-10 | Adjuntos opcionales; si se usan, cumplen Pieza 3. | S9       | Pendiente | —         |

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