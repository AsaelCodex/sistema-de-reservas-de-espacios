# Manejador de documentos — Práctica 4 (P4-01)

**Requisitos:** RF-DOC-01 a RF-DOC-04 · RD-06, RD-07, RD-08, RD-09 y RD-12  
**Preparación para:** RF-DOC-05 (ruta configurable; el volumen Docker se
implementará en otra entrega)  
**Fuera de alcance:** RF-DOC-06 (auditoría), frontend, reportes y demás piezas
del Core.

## 1. Alcance implementado

| Requisito | Implementación |
|---|---|
| RF-DOC-01 | Subida con nombre original, tipo, tamaño, fecha UTC, usuario de la sesión e identificador interno del contenido. |
| RF-DOC-02 | Listado propio para Estándar y listado global para Administrador; nunca incluye eliminados. |
| RF-DOC-03 | Descarga solo para el propietario o un Administrador; rechazo controlado si el contenido físico no está disponible. |
| RF-DOC-04 | Eliminación lógica mediante `Documento.Eliminar`; la fila y el contenido físico se conservan. |
| RF-DOC-05 | Ruta de almacenamiento configurable y validada fuera de `wwwroot`. **No** se crea el volumen Docker en esta entrega. |
| RD-06 | Sesión validada en todas las operaciones; propietario tomado de `SesionService`, nunca del cliente. |
| RD-07 | Validación de archivo ausente/vacío, nombre, tipo, tamaño y límite máximo. |
| RD-08 | Mensajes controlados; no se exponen rutas internas, excepciones ni el identificador de contenido. |
| RD-09 | Metadatos persistidos en SQL Server mediante EF Core y migración `AddDocumentos`. |
| RD-12 | Servicios y almacenamiento probados sin levantar la aplicación. |

## 2. Arquitectura

| Capa | Componentes |
|---|---|
| Core | Entidad `Documento`, `ActorDocumento`, puertos `IDocumentoRepository` e `IAlmacenDocumentos`. Sin ASP.NET, EF Core ni dependencias del módulo de reservas. |
| Business | `SubirDocumentoService`, `ListadoDocumentosService`, `DescargarDocumentoService`, `EliminarDocumentoService` y sus resultados. |
| Infrastructure | `DocumentoRepository` (EF Core/SQL Server), `AlmacenArchivosDocumentos` (sistema de archivos) y migración `AddDocumentos`. |
| API | `DocumentosController`, composición en `Program.cs`, límites de formulario/Kestrel y configuración de la ruta. |

### Modelo `Documento`

| Campo | Descripción |
|---|---|
| `Id` | Identificador público del documento. |
| `NombreOriginal` | Nombre enviado por el cliente, validado y usado solo como metadato. |
| `Tipo` | Tipo de contenido declarado por el cliente, validado. |
| `TamanoBytes` | Tamaño realmente leído del flujo; nunca un valor declarado por el cliente. |
| `FechaSubidaUtc` | Fecha UTC fijada por el servidor. |
| `UsuarioId` | Usuario de la sesión que subió el archivo. |
| `ContenidoId` | Identificador interno generado por el servidor; no se expone en la API. |
| `Eliminado` / `EliminadoEnUtc` | Estado de eliminación lógica y su fecha UTC. |

## 3. Endpoints

Todas las operaciones requieren `Authorization: Bearer <token>` y declaran
`[RequiereRol(NivelOperacion.Autenticado)]`.

| Método y ruta | Descripción | Éxito | Errores principales |
|---|---|---|---|
| `POST /api/documentos` | Sube un archivo como `multipart/form-data`, campo `archivo`. | `201` con metadatos y `Location: /api/documentos/{id}/contenido` | `400` archivo ausente/vacío/datos inválidos/nombre con ruta/multipart malformado; `401` sesión inválida; `413` supera el límite; `500` fallo de persistencia compensado; `503` almacenamiento no disponible |
| `GET /api/documentos` | Lista documentos activos. Estándar: solo propios. Administrador: todos. | `200` con arreglo | `401` sesión inválida |
| `GET /api/documentos/{id}/contenido` | Descarga el contenido por endpoint protegido. | `200` con el archivo | `401`; `403` otro Estándar; `404` inexistente, eliminado o contenido físico no disponible |
| `DELETE /api/documentos/{id}` | Elimina lógicamente el documento. | `204` | `401`; `403` otro Estándar; `404` inexistente o ya eliminado; `500` fallo de persistencia |

La respuesta de subida y de listado tiene esta forma (no incluye
`ContenidoId`):

```json
{
  "id": "6f1c...",
  "nombreOriginal": "acta.pdf",
  "tipo": "application/pdf",
  "tamanoBytes": 1234,
  "fechaSubidaUtc": "2026-11-08T12:30:00Z",
  "usuarioId": "0a2b...",
  "eliminado": false
}
```

### Códigos HTTP

| Código | Significado |
|---|---|
| `201` | Documento subido y metadatos persistidos. |
| `200` | Listado o descarga correctos. |
| `204` | Eliminación lógica completada. |
| `400` | Archivo ausente, vacío, nombre/tipo inválido, datos incorrectos o multipart malformado. |
| `401` | Sesión ausente, desconocida, vencida o cerrada. |
| `403` | Otro usuario Estándar intenta descargar o eliminar un documento ajeno. |
| `404` | Documento inexistente, eliminado o contenido físico no disponible. |
| `413` | El archivo supera `Documentos:TamanioMaximoBytes`. |
| `500` | Fallo controlado de persistencia; si el archivo ya se escribió, se compensa. |
| `503` | El almacenamiento físico no pudo escribir el archivo. |

## 4. Almacenamiento y límites

Configuración en `appsettings.json` y sobrescritura por variables de entorno:

```json
{
  "Documentos": {
    "DirectorioAlmacenamiento": "documentos-almacenados",
    "TamanioMaximoBytes": 10485760,
    "MargenSolicitudBytes": 1048576
  }
}
```

```powershell
$env:Documentos__DirectorioAlmacenamiento = "C:\datos\documentos"
$env:Documentos__TamanioMaximoBytes = "5242880"
$env:Documentos__MargenSolicitudBytes = "1048576"
```

- La ruta relativa se resuelve contra el directorio de contenido de la API.
- Al iniciar, la API rechaza una ruta que esté dentro de `wwwroot`.
- En el contenedor, `Documentos__DirectorioAlmacenamiento` vale
  `/app/documentos`, respaldado por el volumen nombrado `documentos`. Los
  archivos no forman parte de la imagen.
- El directorio se crea al subir el primer documento.
- El nombre físico es un identificador hexadecimal de 32 caracteres generado
  por el servidor. No se usa el nombre original para construir rutas.
- La escritura usa un archivo temporal y un movimiento final; un fallo no deja
  contenido parcial.
- **Límites separados (P4-DOC-01):** `TamanioMaximoBytes` es el límite del
  archivo y lo aplica el servicio durante la lectura. El cuerpo multipart
  incluye delimitadores, cabeceras y nombre de archivo, por lo que el límite de
  la solicitud es `TamanioMaximoBytes + MargenSolicitudBytes` (1 MiB por
  defecto). `FormOptions.MultipartBodyLengthLimit` y
  `KestrelServerOptions.Limits.MaxRequestBodySize` usan ese límite mayor para
  no rechazar archivos válidos cercanos al máximo.
- **Conteo real (P4-DOC-02):** el almacenamiento lee por bloques, cuenta los
  bytes y corta al superar `TamanioMaximoBytes`. Un flujo no posicionable se
  mide durante la lectura; el tamaño guardado es el real. Un contenido realmente
  vacío se rechaza y no deja temporal ni metadatos.
- El contenido se sirve únicamente mediante `GET /api/documentos/{id}/contenido`;
  no se publica como archivo estático.

## 5. Consistencia entre archivo y metadatos

1. Se validan actor, nombre y tipo antes de escribir; si el flujo es
   posicionable se hace una comprobación temprana de `Length - Position`.
2. Se escribe el contenido contando los bytes realmente leídos y aplicando el
   límite durante la copia. El resultado informa `Guardado` con el tamaño real,
   `Vacio` o `LimiteExcedido`; en los dos últimos casos se elimina el temporal.
3. Solo tras un guardado correcto se persisten los metadatos con el tamaño real.
4. Si la persistencia falla, se elimina el contenido recién escrito y se
   devuelve `500` con un mensaje genérico. No queda fila ni archivo huérfano.
   `IAlmacenDocumentos.EliminarAsync` propaga los fallos reales de borrado y
   trata como válido el contenido ya inexistente. Si la compensación también
   falla, el fallo se registra internamente y la respuesta sigue siendo
   genérica: no se exponen rutas, excepciones ni secretos.
5. La eliminación lógica **no** invoca `IAlmacenDocumentos.EliminarAsync`; ese
   método existe solo para compensar una subida fallida.

## 6. Autorización y sesión

- `AutorizacionRolFilter` solo aplica la comprobación de rol cuando la operación
  declara `NivelOperacion.Administrador`. Con `Autenticado` el filtro no valida
  la sesión por sí solo.
- Por eso `DocumentosController` consulta `SesionService.ConsultarUsuarioAsync`
  en **todas** las acciones y devuelve `401` antes de invocar al negocio.
- El actor se construye con `consulta.Usuario.Id` y `consulta.Usuario.Rol`.
- El Estándar solo ve, descarga y elimina documentos propios. El Administrador
  puede gestionar los de otros usuarios.
- Un documento eliminado se trata como inexistente en listado y descarga.

## 7. Pruebas

### Servicios y almacenamiento (sin aplicación)

| Requisito | Pruebas |
|---|---|
| RF-DOC-01 | `SubirDocumentoServiceTests.Subir_WithCompleteData_StoresMetadataAndContent`; `AlmacenArchivosDocumentosTests.Guardar_GeneratesServerNameAndWritesContent` |
| RF-DOC-02 | `ListadoDocumentosServiceTests.Listar_AsOwner_ReturnsOnlyOwnActiveDocuments`; `...AsAdministrator_ReturnsActiveDocumentsOfAllUsers` |
| RF-DOC-03 | `DescargarDocumentoServiceTests` (propietario, Administrador, otro Estándar, eliminado, contenido ausente) |
| RF-DOC-04 | `EliminarDocumentoServiceTests.Eliminar_AsOwner_LogicallyDeletesAndKeepsRowAndContent` |
| RD-06 | `Subir_WithInvalidActor_IsRejectedWithoutWriting`; `Descargar_AsAnotherStandardUser_IsRejected`; `Eliminar_AsAnotherStandardUser_IsRejectedWithoutChanges` |
| RD-07 | `Subir_WithoutFile_IsRejectedWithoutWriting`; `Subir_WithEmptyFile_IsRejectedWithoutWriting`; `Subir_WhenFileExceedsLimit_IsRejectedWithoutWriting`; `Subir_WithFileNameThatEscapesDirectory_IsRejectedWithoutWriting`; `Subir_WithNonSeekableStream_StoresActualSize`; `Subir_WithNonSeekableStreamOverLimit_LeavesNoMetadataNorContent`; `Subir_WithNonSeekableEmptyStream_LeavesNoMetadataNorContent`; `AlmacenArchivosDocumentosTests.Guardar_WithNonSeekableStream_CountsActualSize`; `...OverLimit_DoesNotLeaveContent`; `...WithEmptyStream_ReturnsEmptyWithoutFinalContent` |
| RD-08 | `Subir_WhenStorageFails_ReturnsControlledErrorWithoutMetadata`; `Subir_WhenMetadataPersistenceFails_RemovesStoredContent`; `Subir_WhenCompensationFails_LogsInternallyAndKeepsResponseControlled`; `Descargar_WhenPhysicalContentIsMissing_ReturnsControlledError`; `AlmacenArchivosDocumentosTests.Abrir_WithInvalidContentId_ReturnsNull`; `AlmacenArchivosDocumentosTests.Eliminar_WhenContentCannotBeDeleted_PropagatesError`; `AlmacenArchivosDocumentosTests.Eliminar_WithMissingContent_IsNoOp` |

### API HTTP real (WebApplicationFactory + TestServer)

`DocumentosHttpTests` ejercita el pipeline completo de ASP.NET Core con
repositorios y sesión falsos y almacenamiento real en un directorio temporal:

- Archivo por debajo del límite: `201`, `Location` apuntando a
  `/api/documentos/{id}/contenido` y descarga correcta del contenido.
- Archivo exactamente en el límite del archivo: `201` (el multipart excede el
  límite del archivo pero no el de la solicitud).
- Archivo por encima del límite: `413` controlado, sin metadatos ni archivos.
- Multipart malformado: `400` controlado, sin rutas ni trazas en el cuerpo.

### API

`DocumentosControllerTests` cubre:

- Las cuatro operaciones sin sesión, con token desconocido y con sesión vencida.
- Declaración de nivel `Autenticado`.
- Comportamiento real del filtro: con nivel `Autenticado` no reemplaza el
  resultado y el controlador aplica el `401`.
- Subida con metadatos completos y propietario de la sesión; `Location` apunta
  al endpoint de contenido y `ContenidoId` no se expone.
- Flujo no posicionable con tamaño declarado incorrecto: se guarda el tamaño
  real.
- Listado propio, listado de Administrador y exclusión de eliminados.
- Descarga por propietario y Administrador; rechazo de otro Estándar.
- Eliminación por propietario y Administrador; rechazo de otro Estándar.
- Documento eliminado que no aparece ni se puede descargar.
- Archivo ausente, vacío, nombre con intento de escape y límite excedido.
- Fallo de almacenamiento (`503`) y fallo de persistencia con compensación
  (`500`).

### Persistencia (SQL Server LocalDB)

`DocumentoPersistenciaTests` verifica con contextos nuevos:

- Metadatos completos legibles después de escribir.
- Listados que excluyen eliminados y filtran por propietario.
- Eliminación lógica real que conserva la fila y el archivo físico.
- Índice único de `ContenidoId`.

## 8. Comprobación manual

1. Aplicar migraciones:

```powershell
dotnet ef database update `
  --project SistemaDeReservas/SistemaDeReservas.Infrastructure `
  --startup-project SistemaDeReservas/SistemaDeReservas.API
```

2. Iniciar la API con `DB_CONNECTION_STRING` y `App:PublicBaseUrl` definidos.
3. Iniciar sesión como Estándar y guardar el token.
4. Subir un archivo:

```powershell
curl.exe -s -X POST "$base/api/documentos" `
  -H "Authorization: $std" -F "archivo=@C:\ruta\acta.pdf"
```

5. Listar y descargar:

```powershell
curl.exe -s "$base/api/documentos" -H "Authorization: $std"
curl.exe -s "$base/api/documentos/<id>/contenido" -H "Authorization: $std" -o acta.pdf
```

6. Intentar descargar con otro Estándar: debe responder `403`.
7. Eliminar y comprobar:

```powershell
curl.exe -s -X DELETE "$base/api/documentos/<id>" -H "Authorization: $std"
```

- El listado ya no muestra el documento.
- La descarga responde `404`.
- La fila sigue en `Documentos` con `Eliminado = 1`.
- El archivo físico sigue en el directorio configurado.

## 9. Persistencia en contenedor (RF-DOC-05)

`docker-compose.yml` monta tres volúmenes nombrados:

| Volumen | Ruta en el contenedor | Contenido |
|---|---|---|
| `sqlserver-datos` | `/var/opt/mssql` | Base de datos |
| `documentos` | `/app/documentos` | Contenido subido |
| `correos` | `/app/correo-salida` | Correos `.eml` |

Verificación realizada con el proyecto Compose aislado
`reservas-verificacion`:

1. Subir un documento y anotar su identificador y hash SHA-256.
2. `docker compose -p reservas-verificacion down` (sin `-v`).
3. `docker compose -p reservas-verificacion up -d`.
4. Iniciar sesión y descargar el mismo documento: `200` y hash idéntico.
5. Comprobar que el administrador inicial no se duplica y que los `.eml`
   siguen en `/app/correo-salida`.

`down -v` elimina los volúmenes y, con ellos, los documentos; es una operación
destructiva. En el host no queda una copia del contenido.

## 10. Limitaciones y pendientes

- **RF-DOC-06:** la auditoría de subida y eliminación no se implementa aquí.
- No hay frontend, reportes ni antivirus/validación de contenido por firma.
- Las pruebas HTTP usan `WebApplicationFactory` con `TestServer`, no Kestrel
  real: ejercitan enrutamiento, parser multipart y límites del formulario, pero
  no la pila de red ni el límite de Kestrel. Las demás pruebas de API invocan el
  controlador en memoria.
- La eliminación lógica no borra archivos físicos; una política de retención o
  purga física queda fuera de esta práctica.
