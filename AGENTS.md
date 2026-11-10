# AGENTS.md

## 1. Contexto del proyecto

Este proyecto es un sistema de reservas de espacios y equipos
desarrollado para la asignatura Programación III del ITLA.

El sistema permitirá gestionar reservas de recursos como:

- Salones
- Canchas
- Equipos

Las reservas deberán realizarse por horario y el sistema deberá
evitar conflictos de disponibilidad.

## 2. Tecnología

- Lenguaje: C#
- Framework: ASP.NET Core
- Control de versiones: Git
- Repositorio: GitHub

## 3. Documentación oficial

Los requisitos oficiales del proyecto están documentados en:

docs/requerimientos-core.md

Este archivo contiene los requisitos del Core y sus identificadores.

Los identificadores deben utilizarse como referencia durante el
desarrollo:

- RD-XX: requisitos de diseño transversales.
- RF-CA-XX: requisitos de Control de Acceso.
- RF-GP-XX: requisitos de Gestión de Permisos.
- RF-DOC-XX: requisitos del Manejador de Documentos.
- RF-NOT-XX: requisitos de Notificaciones.
- RF-REP-XX: requisitos de Reportes.
- RF-AUD-XX: requisitos de Auditoría.
- RF-NEG-XX: requisitos del módulo de negocio.

Cuando una tarea mencione uno o varios identificadores, consulta
docs/requerimientos-core.md y utiliza el requisito y criterio de
aceptación correspondiente.

No inventes requisitos ni cambies el significado de los requisitos
oficiales.

## 4. Etapa actual del proyecto

Actualmente el proyecto se encuentra en la práctica 4 (semana 9).

La pieza que se trabaja durante esta etapa es el manejador de
documentos (RF-DOC-01 a RF-DOC-05):

- Pieza 1 - Control de Acceso: implementada y verificada.
- Pieza 2 - Gestión de Permisos: implementada y verificada.
- Módulo de negocio: contrato y reglas de transición implementados y
  verificados en la práctica 3.
- Pieza 3 - Manejador de documentos: implementada y verificada
  (RF-DOC-01 a RF-DOC-04).
- P4-02 - Contenedor, inicialización automática y persistencia:
  implementada y verificada. RF-DOC-05 queda completo al comprobar que
  el contenido sobrevive a `docker compose down` sin `-v`.
- P4-03 - API externa (evaluación climática de una reserva con
  WeatherAPI): implementada y verificada.

El Control de Acceso sigue siendo la base de las demás funcionalidades
del Core y las operaciones del sistema deberán quedar protegidas por
él.

## 5. Alcance actual

### Práctica 4 (alcance actual)

Durante la práctica 4 se trabajará con:

- RD-06
- RD-07
- RD-08
- RD-09
- RD-12

Y con:

- RF-DOC-01
- RF-DOC-02
- RF-DOC-03
- RF-DOC-04
- RF-DOC-05

RF-DOC-05 se verifica en esta entrega con volúmenes nombrados de
Docker: documentos, correos `.eml` y base de datos sobreviven a
`docker compose down` sin `-v`.

La P4-03 (API externa) se implementa en esta misma práctica 4:
evaluación climática de una reserva mediante el pronóstico horario de
WeatherAPI, con autorización, regla del 60 %, cobertura completa,
manejo controlado de fallos y pruebas sin la API real.

Quedan fuera de alcance:

- RF-DOC-06 (auditoría de subidas y eliminaciones).
- Frontend, reportes y otras piezas del Core.

### Antecedentes: práctica 3

La práctica 3 trabajó principalmente:

- RD-04
- RD-07
- RD-12

Y con:

- RF-NEG-02
- RF-NEG-03
- RF-NEG-04
- RF-NEG-05
- RF-NEG-06
- RF-NEG-09

RF-NEG-02 y RF-NEG-06 no se consideran terminados mientras falten
operaciones del negocio y su exposición protegida.

No implementar funcionalidades fuera del alcance solicitado.

## 6. Control de Acceso

El Control de Acceso debe contemplar como mínimo:

- Registro de usuarios.
- Correo único.
- Contraseñas almacenadas mediante hash.
- Inicio de sesión.
- Credencial de sesión.
- Roles Administrador y Estándar.
- Autorización basada en roles.
- Consulta del usuario autenticado.
- Cambio de rol por Administrador.
- Recuperación de contraseña.
- Código de recuperación de un solo uso.
- Vencimiento del código.
- Cambio de contraseña.
- Invalidación de sesiones anteriores al cambiar la contraseña.
- Restablecimiento forzado de contraseña por Administrador.

La recuperación de contraseña debe considerar que el envío de
correo pertenece a una pieza posterior del Core. En esta etapa
el correo puede quedar registrado en la cola correspondiente
según los requisitos oficiales.

## 7. Separación de responsabilidades

Respetar los siguientes principios:

- La lógica de negocio no debe estar en Controllers.
- Los Controllers o endpoints deben encargarse de recibir y
  responder solicitudes, no de contener toda la lógica de negocio.
- Cada componente debe tener una responsabilidad clara.
- Las dependencias entre componentes deben estar justificadas.
- El Core no debe depender del módulo de reservas.
- Las transiciones de estados deben estar centralizadas.
- La autorización debe verificarse del lado del servidor.

## 8. Seguridad

Nunca:

- Guardar contraseñas en texto plano.
- Guardar credenciales o claves directamente en el código.
- Cometer secretos al repositorio.
- Exponer contraseñas en respuestas o logs.
- Exponer stack traces, rutas internas o consultas SQL al usuario.

Las credenciales y claves deben obtenerse mediante variables de
entorno o el mecanismo de configuración seguro correspondiente.

## 9. Validación

Toda entrada proveniente del exterior debe validarse antes de
utilizarse.

Los datos inválidos deben producir respuestas controladas y
comprensibles.

No ocultar errores de autorización simplemente en la interfaz.

Si una operación requiere Administrador, debe rechazarse también
cuando un usuario Estándar construya la petición manualmente.

## 10. Pruebas

Cada requisito implementado debe poder verificarse mediante
pruebas.

Cuando sea posible, las pruebas deben referenciar el requisito
que están verificando.

Ejemplo:

RF-CA-01:
- Registrar usuario con correo nuevo.
- Registrar nuevamente el mismo correo.
- Verificar que el segundo registro sea rechazado.

RF-CA-02:
- Registrar una contraseña.
- Verificar que el almacenamiento contenga un hash y no la
  contraseña original.

No considerar un requisito terminado solamente porque la aplicación
compile o ejecute correctamente.

## 11. Git

Utilizar ramas por funcionalidad.

Ejemplos:

feature/rf-ca-01-registro
feature/rf-ca-03-login
feature/rf-ca-05-autorizacion

Los commits deben:

- Hacer una sola cosa.
- Tener un asunto descriptivo.
- Estar escritos en modo imperativo.
- Tener como máximo 50 caracteres en el asunto.
- Evitar mensajes genéricos como "cambios", "arreglos" o "final".

No mezclar funcionalidades no relacionadas en un mismo commit.

## 12. Pull Requests

Cada Pull Request debe tener un alcance claro.

La descripción debe explicar:

- Qué cambia.
- Por qué.
- Cómo probarlo.
- Qué NO incluye.

No mezclar funcionalidades diferentes en un mismo Pull Request
cuando puedan separarse.

## 13. Comportamiento esperado del agente

Antes de realizar cambios importantes:

1. Analizar la estructura existente.
2. Consultar los requisitos correspondientes.
3. Identificar los archivos que serán modificados.
4. Explicar brevemente el enfoque.
5. Implementar únicamente el alcance solicitado.
6. Crear o actualizar las pruebas correspondientes.
7. Verificar que no se hayan introducido cambios fuera del alcance.

No reestructurar todo el proyecto sin necesidad.

No agregar funcionalidades "por si acaso".

Si existe una ambigüedad en los requisitos, señalarla antes de
tomar una decisión importante.

## 14. Regla principal

El requisito oficial es la fuente de verdad.

Cuando una instrucción del usuario haga referencia a un ID como
RF-CA-03, buscar ese ID en:

docs/requerimientos-core.md

y trabajar según su descripción y criterio de aceptación.

## 15. Manejador de documentos (práctica 4)

La pieza está implementada en Core, Business, Infrastructure y API:

- Core: `Documento`, `ActorDocumento`, `IDocumentoRepository`,
  `IAlmacenDocumentos` y `ResultadoGuardadoContenido`.
- Business: `SubirDocumentoService`, `ListadoDocumentosService`,
  `DescargarDocumentoService` y `EliminarDocumentoService`.
- Infrastructure: `DocumentoRepository`, `AlmacenArchivosDocumentos` y la
  migración `AddDocumentos`.
- API: `DocumentosController` y configuración en `Program.cs`.

### Endpoints

Todas las operaciones requieren `Authorization: Bearer <token>` y
declaran `[RequiereRol(NivelOperacion.Autenticado)]`. Como el filtro no
valida la sesión para ese nivel, el controlador consulta
`SesionService` en todas las acciones y responde `401` antes de invocar
al negocio. El propietario se toma solo de la sesión.

| Método y ruta | Descripción | Éxito | Errores principales |
|---|---|---|---|
| `POST /api/documentos` | Sube un archivo (`multipart/form-data`, campo `archivo`). | `201` con `Location: /api/documentos/{id}/contenido` | `400`, `401`, `413`, `500`, `503` |
| `GET /api/documentos` | Lista activos: propios para Estándar, todos para Administrador. | `200` | `401` |
| `GET /api/documentos/{id}/contenido` | Descarga por endpoint protegido. | `200` | `401`, `403`, `404` |
| `DELETE /api/documentos/{id}` | Elimina lógicamente. | `204` | `401`, `403`, `404`, `500` |

- `400`: archivo ausente o vacío, nombre con ruta, tipo/tamaño inválido o
  multipart malformado.
- `403`: otro usuario Estándar intenta descargar o eliminar un documento
  ajeno.
- `404`: documento inexistente, eliminado o contenido físico no
  disponible.
- `413`: supera `Documentos:TamanioMaximoBytes` (10 MiB por defecto).
- `500`: fallo controlado de persistencia; la subida compensa el archivo
  ya escrito. Si la compensación falla, se registra internamente sin
  exponer el detalle.
- `503`: el almacenamiento físico no pudo escribir el archivo.

### Almacenamiento

- `Documentos:DirectorioAlmacenamiento` (por defecto
  `documentos-almacenados`) es configurable por entorno con
  `Documentos__DirectorioAlmacenamiento`.
- La API rechaza al iniciar una ruta dentro de `wwwroot`.
- El nombre físico es un identificador hexadecimal de 32 caracteres
  generado por el servidor; el nombre original solo se guarda como
  metadato.
- La escritura usa un archivo temporal y un movimiento final.
- El límite del archivo (`Documentos:TamanioMaximoBytes`) es distinto del
  límite de la solicitud multipart
  (`TamanioMaximoBytes + Documentos:MargenSolicitudBytes`, 1 MiB por
  defecto). `FormOptions` y Kestrel usan el límite de la solicitud.
- El almacenamiento cuenta los bytes realmente leídos, corta al superar el
  límite, rechaza contenido vacío y guarda el tamaño real.
- La eliminación lógica conserva la fila y el contenido físico.

### Pruebas

- `SistemaDeReservas.Tests/Documentos`: servicios y almacenamiento sin
  levantar la aplicación, incluidos flujos no posicionables y conteo real.
- `SistemaDeReservas.Tests/API/DocumentosControllerTests.cs`: sesión,
  roles, autorización, códigos HTTP y validaciones en memoria.
- `SistemaDeReservas.Tests/API/DocumentosHttpTests.cs`: pipeline HTTP real
  con `WebApplicationFactory`/`TestServer`, límites del multipart, parser
  malformado y `Location` descargable.
- `SistemaDeReservas.Tests/Persistencia/DocumentoPersistenciaTests.cs`:
  metadatos, listados, eliminación lógica e índice único en SQL Server
  LocalDB.

Cada prueba declara el requisito con
`[Trait("Requerimiento", "RF-DOC-XX")]` o `RD-XX`. La documentación de la
pieza está en `docs/manejador-documentos.md`.

## 16. Contenedor e inicialización (práctica 4, P4-02)

La ejecución completa vive en `docker-compose.yml` (servicios `api` y
`sqlserver`) y en `SistemaDeReservas/SistemaDeReservas.API/Dockerfile`
(imagen multi-etapa .NET 10, usuario `app`). Solo se publica
`http://localhost:5137`; no hay HTTPS ni certificados locales.

- `.env` está ignorado por Git y excluido del contexto de construcción.
  `.env.example` contiene valores ficticios funcionales; las credenciales
  reales se definen por entorno y nunca se versionan ni se pasan como
  argumentos de construcción.
- El arranque espera a SQL Server con un `healthcheck` que exige el motor
  accesible y todas las bases `ONLINE`, más reintentos acotados en la
  aplicación (`InicializacionBaseDatos:IntentosMaximos`). No hay pausas
  fijas.
- Las migraciones se aplican con `MigrateAsync` antes de Kestrel y del
  worker de correos. Nunca se usa `EnsureCreated`. Un error no transitorio
  se registra y detiene el arranque; los transitorios de SQL (incluido
  `1801` mientras una base se recupera) se reintentan.
- Si `AdministradorInicial:Habilitado` es `true` y no existe **ningún**
  administrador, se crea la cuenta configurada con el hasher y la política
  de contraseñas existentes. Los reinicios no duplican, promueven ni
  modifican cuentas. Un correo ya ocupado es un error fatal. La contraseña
  no se registra.
- Volúmenes nombrados: `sqlserver-datos` (`/var/opt/mssql`), `documentos`
  (`/app/documentos`) y `correos` (`/app/correo-salida`).
  `Documentos__DirectorioAlmacenamiento` apunta dentro del volumen. Los
  `.eml` se consultan con `docker compose exec api cat ...` o por la cola.
- `docker compose down` conserva los volúmenes; `down -v` los elimina. La
  verificación se hace con un proyecto Compose aislado (por ejemplo,
  `-p reservas-verificacion`) para no tocar volúmenes del usuario.

El detalle operativo está en la sección «Contenedor» de `README.md` y en
`docs/manejador-documentos.md`.

## 17. API externa — evaluación climática (práctica 4, P4-03)

La evaluación climática consulta el pronóstico horario de WeatherAPI
para una reserva persistida. No modifica la reserva ni sus
transiciones.

- Core: `UbicacionClima`, `HoraPronosticoClima`, `PronosticoClima`,
  `SolicitudPronosticoClima`, `ErrorProveedorClima`,
  `ResultadoPronosticoClima` e `IProveedorClima`. El Core no conoce HTTP.
- Business: `EvaluacionClimaticaService`, `EvaluacionClimatica`,
  `OpcionesEvaluacionClimatica` y `ResultadoEvaluacionClimatica`.
- Infrastructure: `ClienteWeatherApi` con
  `https://api.weatherapi.com/v1/forecast.json`.
- API: `GET /api/reservas/{id}/evaluacion-climatica` en
  `ReservasController`.

### Autorización y datos

Solo el propietario o un Administrador pueden evaluar la reserva. La
sesión se valida con `SesionService` y la autorización se comprueba
**antes** de consultar al proveedor. El propietario, la fecha y el
horario salen de la reserva persistida; nunca del cliente.

### Regla y horizonte

- Se usan las horas completas que intersectan `[HoraInicio, HoraFin)`.
- La probabilidad evaluada es `max(chance_of_rain)` de esas horas.
- El umbral inicial es 60 % y debe estar entre 0 y 100.
- Si se alcanza el umbral se advierte y se sugiere un espacio cubierto;
  si no, no se promete ausencia de lluvia.
- Se rechazan fechas pasadas, horarios ya iniciados, fechas fuera de las
  devueltas por el proveedor y cobertura incompleta.
- Nunca se usa el clima actual ni un horizonte fijo de 72 horas.
- La ubicación es única y configurable (`Clima:Ubicacion`), en
  `America/Santo_Domingo`. No se deduce del nombre del espacio.
- La ubicación de demostración es ITLA, La Caleta, Santo Domingo Este
  (`18.4512, -69.6626`).

### Configuración y errores

- `Clima:UmbralProbabilidadLluvia` (60), `Clima:TimeoutSegundos` (10) y
  `Clima:DiasPronostico` (3) se validan al arrancar.
- `WeatherApi:ApiKey` se lee de `WeatherApi__ApiKey`. Si falta, la
  aplicación arranca y solo esta operación responde `503`.
- El cliente usa HTTPS y host fijos, no sigue redirecciones, aplica un
  tiempo de espera propio, propaga la cancelación del usuario y no
  registra la URL con la clave.
- Mapeo HTTP: `400` datos inválidos; `401` sin sesión; `403` actor sin
  permiso; `404` reserva inexistente; `422` fecha pasada/iniciada/fuera
  de horizonte/cobertura incompleta; `503` clave ausente o inválida,
  cuota o proveedor no disponible; `504` timeout; `502` error remoto o
  respuesta inválida.
- Las respuestas no exponen claves, URLs, cuerpos del proveedor ni
  detalles internos.

### Pruebas y documentación

- `SistemaDeReservas.Tests/Clima`: servicio con reloj controlable,
  umbral 59/60/61, horas parciales, horizonte, cobertura, duplicados,
  datos inválidos y fallos del proveedor; cliente con transporte falso.
- `ReservasControllerTests`: sesión, roles, propietario, Administrador,
  otro Estándar, reserva inexistente y clave ausente.
- No se llama a la API real en las pruebas automatizadas.
- La documentación de la pieza está en `docs/api-externa.md`.
