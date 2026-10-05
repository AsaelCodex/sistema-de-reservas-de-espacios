# Requerimientos del Core

**Curso:** Programación III · ITLA · 2026-C-3  
**Documento:** Requerimientos del Core — Especificación funcional y de diseño

> Esta es una versión Markdown de consulta del documento oficial. El documento oficial sigue siendo la fuente de verdad.

---

## 1. Requisitos de diseño transversales

Estos requisitos aplican a todo el sistema, incluido el módulo de negocio.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RD-01 | Cada pieza del Core es un componente con una responsabilidad única y una interfaz explícita. | Puedes decir, para cada pieza, qué hace y qué operaciones expone, sin describir el funcionamiento interno de otra pieza. |
| RD-02 | La lógica de negocio no vive en la capa de presentación ni dentro del manejador de la petición. | Cambiar la interfaz de usuario no obliga a reescribir ninguna regla de negocio. |
| RD-03 | El Core no depende del módulo de negocio; la dependencia va en un solo sentido. | Si eliminas el módulo de negocio del proyecto, el Core sigue construyéndose y ejecutándose. |
| RD-04 | Las transiciones de estado se resuelven en un solo punto del código. | Agregar una transición nueva se hace modificando un único componente; no hay validaciones de estado repetidas en varios lugares. |
| RD-05 | Las contraseñas se almacenan con hash. | Leyendo el almacenamiento directamente no es posible recuperar ninguna contraseña. |
| RD-06 | La autorización se verifica del lado del servidor en cada operación. | Ocultar la opción en la interfaz no basta: invocar la operación directamente también se rechaza. |
| RD-07 | Toda entrada externa se valida antes de usarse. | Datos ausentes, de tipo incorrecto o fuera de rango producen un rechazo controlado, no una excepción sin manejar. |
| RD-08 | Los errores devuelven un mensaje comprensible sin exponer detalles internos. | Ningún mensaje visible al usuario contiene trazas de pila, rutas de archivos ni consultas. |
| RD-09 | Los datos persisten fuera del proceso. | Reiniciar la aplicación no pierde información. |
| RD-10 | Las credenciales y claves se leen de variables de entorno. | No hay ninguna credencial escrita en el repositorio ni en el historial de commits. |
| RD-11 | Las fechas y horas se registran con el mismo criterio en todo el sistema. | Dos registros creados en el mismo instante por piezas distintas muestran la misma hora. |
| RD-12 | Cada pieza del Core puede probarse sin levantar la aplicación completa. | Existe al menos una prueba de esa pieza que corre sin interfaz de usuario. |

---

## 2. Entidades del Core

Los siguientes son los atributos mínimos. Se pueden agregar los que el diseño necesite, pero no omitir ninguno.

| Entidad | Atributos mínimos |
|---|---|
| Usuario | Nombre · correo (único) · contraseña con hash · rol · activo |
| Rol | Administrador o Estándar |
| SolicitudPermiso | Solicitante · permiso solicitado · estado · aprobador · fecha de creación · fecha de resolución · motivo del rechazo |
| CodigoRecuperacion | Usuario · código · fecha de emisión · fecha de vencimiento · usado o no usado |
| CorreoEnCola | Destinatario · asunto · cuerpo · estado · intentos · fecha de creación · fecha de envío · último error |
| Documento | Nombre · tipo · tamaño · fecha de subida · usuario que lo subió · ruta de almacenamiento · marca de eliminado |
| Notificacion | Destinatario · tipo · mensaje · leída o no leída · fecha |
| RegistroAuditoria | Usuario que actuó · acción · entidad afectada · identificador de la entidad · fecha y hora · valor anterior · valor nuevo |

---

# 3. Pieza 1 — Control de acceso

**Semanas:** 2 a 4

Es la base de todo lo demás: ninguna otra pieza se considera terminada si sus operaciones no están protegidas por esta.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-CA-01 | Registro de usuario con correo único. | Un segundo registro con un correo ya existente se rechaza. |
| RF-CA-02 | La contraseña se guarda con hash, nunca en texto plano. | El valor almacenado no coincide con la contraseña escrita y no puede revertirse a ella. |
| RF-CA-03 | Inicio de sesión que entrega una credencial de sesión. | Credenciales correctas abren sesión; incorrectas se rechazan sin revelar cuál de los dos datos falló. |
| RF-CA-04 | Dos roles: Administrador y Estándar. | Todo usuario tiene exactamente un rol asignado. |
| RF-CA-05 | Cada operación del sistema declara qué rol puede ejecutarla. | Existe un punto del código donde se puede leer la exigencia de rol de cada operación. |
| RF-CA-06 | Un usuario Estándar que invoca una operación de Administrador recibe un rechazo explícito. | El rechazo ocurre también cuando la petición se construye a mano, sin pasar por la interfaz. |
| RF-CA-07 | Consulta del usuario autenticado y su rol. | Sin sesión válida la consulta se rechaza. |
| RF-CA-08 | Cambio de rol de un usuario, reservado al Administrador. | Un Estándar no puede cambiar ningún rol, ni el propio. El cambio queda auditado (RF-AUD-04). |
| RF-CA-09 | Un usuario inicia la recuperación de su contraseña indicando su correo registrado. | La respuesta del sistema es idéntica exista o no ese correo. |
| RF-CA-10 | La recuperación genera un código de un solo uso y con fecha de vencimiento, y lo envía al correo mediante la cola. | Usarlo dos veces, o después de vencido, se rechaza y la contraseña no cambia. El correo sale por la cola (RF-NOT-08), no dentro de la misma operación. |
| RF-CA-11 | Con un código válido el usuario define una contraseña nueva, que se guarda con hash. | La contraseña anterior deja de servir para iniciar sesión. |
| RF-CA-12 | Las sesiones abiertas antes del cambio de contraseña dejan de ser válidas. | Una credencial de sesión emitida antes del restablecimiento es rechazada. |
| RF-CA-13 | Un Administrador puede forzar el restablecimiento de la contraseña de un usuario. | El usuario no puede seguir usando su contraseña anterior, y la acción queda auditada (RF-AUD-05). |
| RF-CA-14 | Política mínima de contraseña: al menos 8 caracteres, con letras y números. | Una contraseña que no la cumple se rechaza con mensaje controlado, en el registro y en todo cambio de contraseña (RD-07). |
| RF-CA-15 | El usuario nace inactivo y recibe por correo un enlace de activación con token de un solo uso y fecha de vencimiento. | Antes de activar, el inicio de sesión se rechaza con un mensaje que indica que la cuenta no está activa. El correo sale por la cola (RF-NOT-08). |
| RF-CA-16 | Abrir el enlace activa la cuenta. | Tras activar, el inicio de sesión funciona. Usar el enlace dos veces, o después de vencido, se rechaza y el estado no cambia. |
| RF-CA-17 | El usuario puede pedir que se le reenvíe el enlace de activación indicando su correo. | La respuesta es idéntica exista o no ese correo. El reenvío invalida el enlace anterior. |
| RF-CA-18 | Cierre de sesión. | La credencial cerrada deja de servir: usarla después se rechaza. |
| RF-CA-19 | Tras 5 intentos fallidos consecutivos, la cuenta queda bloqueada 15 minutos. | El sexto intento, aun con la contraseña correcta, se rechaza durante el bloqueo. Un inicio de sesión correcto pone el contador en cero. |
| RF-CA-20 | Un Administrador desactiva y reactiva usuarios. | Un usuario desactivado no inicia sesión y sus sesiones abiertas dejan de ser válidas. Un Administrador no puede desactivarse a sí mismo. |
| RF-CA-21 | Un Administrador lista los usuarios con su rol y su estado. | Un Estándar recibe rechazo. El listado nunca incluye hashes ni tokens. |
| RF-CA-22 | Un usuario con sesión cambia su propia contraseña indicando la actual. | Con la contraseña actual incorrecta el cambio se rechaza. Al cambiarla aplican RF-CA-14 y RF-CA-12

### Dependencia de recuperación de contraseña

La recuperación de contraseña necesita la cola de correos de la pieza 4, que se construye en la semana 11.

Hasta entonces:

- Implementar el flujo completo.
- Dejar el correo encolado.
- Verificarlo leyendo la cola.

El restablecimiento forzado por un Administrador (`RF-CA-13`) no depende del correo y debe funcionar desde la semana 4.

---

# 4. Pieza 2 — Gestión de permisos

**Semanas:** 6 a 8

Esta es la máquina de estados del Core.

### Transiciones

| Desde | Hacia | Quién la ejecuta | Condición |
|---|---|---|---|
| Pendiente | Aprobada | Administrador | La solicitud está en Pendiente. |
| Aprobada | Aplicada | El sistema | Automática, inmediatamente después de aprobar. |
| Pendiente | Rechazada | Administrador | Exige un motivo escrito. |
| Cualquier otra combinación | — | — | Prohibida. El sistema la rechaza y el estado no cambia. |

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-GP-01 | Un usuario Estándar crea una solicitud de permiso. | La solicitud nace en estado Pendiente. |
| RF-GP-02 | Un Administrador consulta las solicitudes pendientes. | Un usuario Estándar no puede ver las solicitudes de otros usuarios. |
| RF-GP-03 | Un Administrador aprueba una solicitud pendiente. | Queda registrado quién aprobó y cuándo. |
| RF-GP-04 | Un Administrador rechaza una solicitud pendiente indicando el motivo. | Sin motivo escrito, el rechazo no se completa. |
| RF-GP-05 | Al aprobar, el sistema aplica el cambio de permiso automáticamente. | Tras la aprobación el solicitante puede ejecutar la acción que pidió, sin ninguna intervención manual adicional. |
| RF-GP-06 | Solo se permiten las transiciones de la tabla anterior. | Aprobar dos veces la misma solicitud, o pasar de Rechazada a Aplicada, se rechaza y el estado no cambia. |
| RF-GP-07 | Nadie resuelve su propia solicitud. | Un Administrador que intenta aprobar o rechazar una solicitud creada por él mismo recibe un rechazo. |

---

# 5. Pieza 3 — Manejador de documentos

**Semana:** 9

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-DOC-01 | Subir un documento registrando nombre, tipo, tamaño, fecha y quién lo subió. | El registro queda consultable con todos esos datos. |
| RF-DOC-02 | Listar los documentos que el usuario tiene permitido ver. | El listado no incluye documentos eliminados ni de otros usuarios, salvo para el Administrador. |
| RF-DOC-03 | Descargar un documento. | Solo el dueño o un Administrador lo logran; cualquier otro usuario recibe un rechazo. |
| RF-DOC-04 | Eliminar un documento de forma lógica, no física. | Deja de aparecer en el listado, pero el registro sigue existiendo en el almacenamiento. |
| RF-DOC-05 | El archivo se guarda en un volumen persistente, fuera de la imagen del contenedor. | Destruir el contenedor y volver a crearlo no pierde los archivos ya subidos. |
| RF-DOC-06 | Toda subida y toda eliminación queda auditada. | Cada una genera su registro de auditoría (RF-AUD-03). |

---

# 6. Pieza 4 — Notificaciones

**Semanas:** 11 y 12

La pieza tiene dos partes: las notificaciones en sí y la cola de correos que las entrega.

## Notificaciones

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-NOT-01 | Crear una solicitud de permiso notifica a los Administradores. | Todos los Administradores reciben la notificación; nadie más la recibe. |
| RF-NOT-02 | Resolver una solicitud de permiso notifica al solicitante. | La notificación indica si fue aprobada o rechazada, y en el segundo caso el motivo. |
| RF-NOT-03 | Al menos un evento propio del módulo de negocio dispara una notificación. | El evento está descrito en la documentación y se puede provocar en la demo. |
| RF-NOT-04 | Consultar las notificaciones propias, distinguiendo leídas de no leídas. | Un usuario no ve las notificaciones de otro. |
| RF-NOT-05 | Marcar una notificación como leída. | Intentar marcar la notificación de otro usuario se rechaza. |
| RF-NOT-06 | Toda notificación se entrega por dos canales: dentro del sistema y por correo electrónico. | El destinatario la ve en su bandeja interna y recibe además el correo correspondiente. |
| RF-NOT-07 | El destinatario puede desactivar el canal de correo sin perder el canal interno. | Con el correo desactivado la notificación sigue apareciendo dentro del sistema y no se encola ningún correo. |

## Cola de correos

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-NOT-08 | Los correos no se envían dentro de la operación que los origina: se encolan. | La operación de negocio termina correctamente aunque el servidor de correo no responda. |
| RF-NOT-09 | Un proceso independiente toma los correos pendientes y los envía. | El envío ocurre fuera del flujo que creó el correo, y se puede ejecutar sin que ese flujo vuelva a correr. |
| RF-NOT-10 | Un correo que falla se reintenta un número limitado de veces y luego queda marcado como fallido. | No se reintenta indefinidamente ni desaparece sin dejar rastro. |
| RF-NOT-11 | Cada correo registra su estado: pendiente, enviado o fallido, con número de intentos, fechas y último error. | Un Administrador puede consultar la cola y saber qué pasó con cualquier correo. |
| RF-NOT-12 | Un correo enviado no se vuelve a enviar. | Ejecutar el procesador de la cola dos veces seguidas no duplica ningún envío. |
| RF-NOT-13 | Las credenciales del servidor de correo se leen de variables de entorno. | Cumple RD-10: no aparecen en el repositorio ni en el historial de commits. |

---

# 7. Pieza 5 — Reportes con agregación

**Semana:** 12

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-REP-01 | Un reporte sobre el Core: solicitudes de permiso por estado y por período, o documentos subidos por usuario. | Devuelve valores agrupados, no el listado de registros. |
| RF-REP-02 | Un reporte sobre el módulo de negocio, según el dominio elegido. | Responde una pregunta real del dominio, no un conteo de filas. |
| RF-REP-03 | Ambos reportes usan agregación real: agrupaciones, totales o promedios. | El resultado tiene menos filas que los registros que resume. |
| RF-REP-04 | Los reportes respetan el rol de quien consulta. | Un usuario Estándar no obtiene datos de otros usuarios a través de un reporte. |
| RF-REP-05 | Ambos reportes se pueden filtrar por rango de fechas. | Dos rangos distintos producen resultados distintos sobre los mismos datos. |

---

# 8. Pieza 6 — Auditoría

**Semana:** 14

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-AUD-01 | Cada registro guarda usuario que actuó, acción, entidad afectada, identificador, fecha y hora, valor anterior y valor nuevo. | Un registro al que le falte cualquiera de esos datos no cumple. |
| RF-AUD-02 | Se audita toda resolución de solicitud de permiso. | Queda registrado quién aprobó o rechazó qué y cuándo. |
| RF-AUD-03 | Se audita toda subida y eliminación de documento. | Ambas acciones generan registro. |
| RF-AUD-04 | Se audita todo cambio de rol de un usuario. | El registro muestra el rol anterior y el nuevo. |
| RF-AUD-05 | Se audita todo restablecimiento de contraseña. | El registro identifica si lo inició el propio usuario o un Administrador. Nunca guarda la contraseña ni el código. |
| RF-AUD-06 | El registro de auditoría no se modifica ni se elimina desde la aplicación. | No existe ninguna operación de edición ni de borrado sobre él. |
| RF-AUD-07 | Consulta filtrable por usuario, por entidad y por rango de fechas. | Está reservada al Administrador; un usuario Estándar recibe rechazo. |

---

# 9. Módulo de negocio

El dominio lo elige el estudiante del catálogo de proyectos, o puede proponer uno cumpliendo estos mismos requisitos.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-NEG-01 | Al menos cuatro entidades relacionadas entre sí. | Las relaciones existen en el modelo de datos, no solo en el diagrama. |
| RF-NEG-02 | Al menos cinco funcionalidades separables. | Cada una se puede probar por separado de las demás. |
| RF-NEG-03 | Máquina de estados propia, entre 3 y 5 estados. | Los estados están declarados en un solo lugar del código. |
| RF-NEG-04 | Al menos una transición prohibida de forma explícita. | Intentarla se rechaza y el estado no cambia. |
| RF-NEG-05 | Al menos un estado terminal del que no se puede salir. | Ninguna transición parte de ese estado. |
| RF-NEG-06 | Sus operaciones están protegidas por Control de Acceso. | Ninguna operación del negocio se ejecuta sin sesión válida y sin verificación de rol. |
| RF-NEG-07 | Dispara al menos una notificación propia del dominio. | Cumple RF-NOT-03. |
| RF-NEG-08 | Alimenta al menos un reporte con agregación. | Cumple RF-REP-02. |
| RF-NEG-09 | Su máquina de estados es independiente de la de Gestión de permisos. | Cambiar los estados de Gestión de permisos no obliga a cambiar los del negocio. |
| RF-NEG-10 | Adjuntar documentos a una entidad del negocio es opcional. | Si se implementa, cumple todos los requisitos de la Pieza 3. |

> A partir de la semana 8 existen dos máquinas de estados que se prueban por separado: Gestión de permisos y la máquina de estados del negocio.

---

# 10. Calendario de construcción

| Pieza | Semanas | Dónde se evalúa |
|---|---:|---|
| 1. Control de acceso | 2 a 4 | Asignación 1 (S3) · Práctica 1 (S4) |
| Máquina de estados del negocio (estructura) | 4 | Práctica 1 (S4) |
| 2. Gestión de permisos | 6 a 8 | Asignación 2 (S6) · Práctica 2 (S7) |
| Máquina de estados del negocio (pruebas) | 8 | Práctica 3 (S8) |
| 3. Manejador de documentos | 9 | Asignación 3 (S9) |
| 4. Notificaciones y cola de correos | 11 y 12 | Práctica 4 (S11) · Práctica 5 (S12) |
| 5. Reportes | 12 | Práctica 5 (S12) |
| 6. Auditoría | 14 | Asignación 4 (S14) |
| Todo integrado | 15 | Proyecto final (S15) |

---

## Regla de referencia para el proyecto

Los identificadores de requisitos son estables y deben utilizarse al referirse a requisitos en:

- Commits.
- Pull Requests.
- Documentación.
- Delegaciones a agentes como OpenCode.

El criterio de aceptación es el que determina si un requisito está realmente cumplido.
