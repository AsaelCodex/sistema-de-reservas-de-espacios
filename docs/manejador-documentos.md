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
| `TamanoBytes` | Tamaño real cuando el flujo permite conocerlo. |
| `FechaSubidaUtc` | Fecha UTC fijada por el servidor. |
| `UsuarioId` | Usuario de la sesión que subió el archivo. |
| `ContenidoId` | Identificador interno generado por el servidor; no se expone en la API. |
| `Eliminado` / `EliminadoEnUtc` | Estado de eliminación lógica y su fecha UTC. |

## 3. Endpoints

Todas las operaciones requieren `Authorization: Bearer <token>` y declaran
`[RequiereRol(NivelOperacion.Autenticado)]`.

| Método y ruta | Descripción | Éxito | Errores principales |
|---|---|---|---|
| `POST /api/documentos` | Sube un archivo como `multipart/form-data`, campo `archivo`. | `201` con metadatos | `400` archivo ausente/vacío/datos inválidos/nombre con ruta; `401` sesión inválida; `413` supera el límite; `500` fallo de persistencia compensado; `503` almacenamiento no disponible |
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
| `400` | Archivo ausente, vacío, nombre/tipo inválido o datos incorrectos. |
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
    "TamanioMaximoBytes": 10485760
  }
}
```

```powershell
$env:Documentos__DirectorioAlmacenamiento = "C:\datos\documentos"
$env:Documentos__TamanioMaximoBytes = "5242880"
```

- La ruta relativa se resuelve contra el directorio de contenido de la API.
- Al iniciar, la API rechaza una ruta que esté dentro de `wwwroot`.
- El directorio se crea al subir el primer documento.
- El nombre físico es un identificador hexadecimal de 32 caracteres generado
  por el servidor. No se usa el nombre original para construir rutas.
- La escritura usa un archivo temporal y un movimiento final; un fallo no deja
  contenido parcial.
- `FormOptions.MultipartBodyLengthLimit` y
  `KestrelServerOptions.Limits.MaxRequestBodySize` usan el mismo límite.
- El contenido se sirve únicamente mediante `GET /api/documentos/{id}/contenido`;
  no se publica como archivo estático.

## 5. Consistencia entre archivo y metadatos

1. Se validan actor, nombre, tipo, tamaño y límite antes de escribir.
2. Se escribe el contenido y se obtiene el `ContenidoId`.
3. Se persisten los metadatos.
4. Si la persistencia falla, se elimina el contenido recién escrito y se
   devuelve `500` con un mensaje genérico. No queda fila ni archivo huérfano.
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
| RD-07 | `Subir_WithoutFile_IsRejectedWithoutWriting`; `Subir_WithEmptyFile_IsRejectedWithoutWriting`; `Subir_WhenFileExceedsLimit_IsRejectedWithoutWriting`; `Subir_WithFileNameThatEscapesDirectory_IsRejectedWithoutWriting` |
| RD-08 | `Subir_WhenStorageFails_ReturnsControlledErrorWithoutMetadata`; `Subir_WhenMetadataPersistenceFails_RemovesStoredContent`; `Descargar_WhenPhysicalContentIsMissing_ReturnsControlledError`; `AlmacenArchivosDocumentosTests.Abrir_WithInvalidContentId_ReturnsNull` |

### API

`DocumentosControllerTests` cubre:

- Las cuatro operaciones sin sesión, con token desconocido y con sesión vencida.
- Declaración de nivel `Autenticado`.
- Comportamiento real del filtro: con nivel `Autenticado` no reemplaza el
  resultado y el controlador aplica el `401`.
- Subida con metadatos completos y propietario de la sesión; `ContenidoId` no
  se expone.
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

## 9. Limitaciones y pendientes

- **RF-DOC-05:** la ruta ya es configurable y no está en `wwwroot`, pero el
  volumen Docker persistente se implementará en otra entrega.
- **RF-DOC-06:** la auditoría de subida y eliminación no se implementa aquí.
- No hay frontend, reportes ni antivirus/validación de contenido por firma.
- Las pruebas de API invocan el controlador en memoria, no un servidor HTTP real
  con `WebApplicationFactory`, igual que el resto de la suite.
- La eliminación lógica no borra archivos físicos; una política de retención o
  purga física queda fuera de esta práctica.
