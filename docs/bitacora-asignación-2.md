# Bitácora de trabajo — Asignación 2

## 1. Información general

- Proyecto: Sistema de Reservas de Espacios
- Asignatura: Programación III
- Actividad: Gestión de permisos y primera suite de pruebas
- Herramienta de apoyo: OpenCode
- Objetivo: Documentar las tareas delegadas al agente,
  los errores detectados, las correcciones realizadas
  y las verificaciones ejecutadas.

## Incidente D1 — Error en la migración de permisos

### Tarea delegada
Se solicitó a OpenCode revisar la implementación de RF-GP-05, especialmente la persistencia de los permisos otorgados y su funcionamiento con la base de datos real.

### Error detectado
Durante la aplicación de la migración `AddPermisosOtorgados` en SQL Server LocalDB, se produjo el error 1785. El problema se debía a múltiples rutas de eliminación en cascada entre las tablas `Usuarios`, `SolicitudesPermiso` y `PermisosOtorgados`.

### Cómo se detectó
Aunque la suite inicial de pruebas había pasado, la verificación adicional con SQL Server LocalDB reveló que la migración no podía aplicarse. Esto demostró que las pruebas existentes no eran suficientes para detectar el problema de persistencia.

### Corrección aplicada
Se modificó la relación entre `PermisoOtorgado` y `SolicitudPermiso` en `AppDbContext.OnModelCreating`, sustituyendo el comportamiento `Cascade` por `DeleteBehavior.Restrict`. De esta manera, SQL Server dejó de encontrar múltiples rutas de eliminación en cascada y se evitó que una solicitud con un permiso aplicado pudiera eliminarse en cascada junto con dicho permiso.

### Verificación realizada
OpenCode reportó que la cadena completa de migraciones se aplicó correctamente en SQL Server LocalDB. También ejecutó un arnés de verificación con repositorios EF Core reales y contextos nuevos por fase, obteniendo 18 de 18 comprobaciones satisfactorias. Estas verificaciones incluyeron el otorgamiento de permisos, la autorización de usuarios, la persistencia de los estados y el rechazo de operaciones inválidas. La suite del repositorio obtuvo 259 de 259 pruebas satisfactorias en esta etapa.

### Requisitos relacionados
RF-GP-05 y RD-09.

### Estado
Resuelto según las verificaciones reportadas por OpenCode.

## Incidente D2 — Falta de atomicidad al aprobar solicitudes

### Tarea delegada
Se solicitó a OpenCode revisar el proceso de aprobación de solicitudes para comprobar que el cambio de estado y el otorgamiento del permiso se guardaran correctamente.

### Problema detectado
La implementación anterior realizaba dos operaciones de guardado independientes: primero actualizaba la solicitud y después registraba el permiso otorgado. Si la segunda operación fallaba, la solicitud podía quedar marcada como aplicada sin que el permiso se hubiera guardado.

### Cómo se detectó
La revisión del servicio `ResolucionSolicitudService.AprobarAsync` identificó que las dos operaciones se ejecutaban por separado. Esto representaba un riesgo para RF-GP-05, porque la aprobación debía producir el otorgamiento efectivo del permiso.

### Corrección aplicada
Se incorporó el método `GuardarResolucionAsync` en `ISolicitudPermisoRepository`, cuya implementación con EF Core actualiza la solicitud y agrega el permiso otorgado antes de ejecutar un único `SaveChanges`. De esta forma, ambas modificaciones se guardan dentro de la misma transacción de EF Core y no debería persistirse una sin la otra.

También se mejoraron los repositorios falsos de las pruebas para que reprodujeran comportamientos relevantes de la base de datos, como la restricción de permisos duplicados y la lectura de copias de las solicitudes.

### Verificación realizada
OpenCode reportó que la suite completa pasó de 259 a 260 pruebas satisfactorias. Además, el arnés con SQL Server LocalDB obtuvo 21 de 21 comprobaciones satisfactorias.

Se agregó la prueba `Aprobar_WhenGrantCannotBeStored_PersistsNoChangesAtAll`, que verifica que, si no se puede guardar el permiso por un duplicado, la solicitud permanezca en estado Pendiente, sin aprobador registrado y sin generar un permiso adicional.

### Requisitos relacionados
RF-GP-05, RD-09 y los requisitos de pruebas asociados a la consistencia de las operaciones.

### Estado
Resuelto según las verificaciones reportadas por OpenCode.

## Incidente D3 — Inconsistencia de fecha y hora en la aprobación

### Tarea delegada
Se solicitó a OpenCode auditar el cumplimiento de RD-11 en el proceso de aprobación de solicitudes y revisar el uso de `DateTime.UtcNow` y `DateTime.Now` en el proyecto.

### Problema detectado
El método `AprobarAsync` obtenía la fecha y hora en dos llamadas independientes: una para registrar la resolución de la solicitud y otra para registrar el otorgamiento del permiso. Aunque ambas operaciones pertenecían a la misma aprobación, podían terminar registrando valores diferentes.

### Cómo se detectó
Durante la auditoría de RD-11, OpenCode identificó las dos llamadas independientes a `DateTime.UtcNow` en el flujo de aprobación. También revisó las 58 apariciones de `DateTime.UtcNow` y `DateTime.Now` reportadas en el proyecto para buscar otros casos similares.

### Corrección aplicada
Se modificó `AprobarAsync` para capturar la hora UTC una sola vez en la variable `resueltaEn` y utilizar ese mismo valor en `SolicitudPermiso.ResueltaEn` y `PermisoOtorgado.OtorgadoEn`.

### Verificación realizada
OpenCode reportó 261 de 261 pruebas satisfactorias, incluida la nueva prueba `Aprobar_RecordsSameTimestampForRequestAndGrant`, que comprueba la igualdad exacta de ambas fechas. También reportó 22 de 22 comprobaciones satisfactorias en el arnés de SQL Server LocalDB, verificando la igualdad de las fechas después de leerlas desde un contexto nuevo.

La matriz de requisitos se actualizó para marcar RD-11 como verificado, respaldado por la auditoría, las pruebas y la comprobación en la base de datos real.

### Requisito relacionado
RD-11.

### Estado
Resuelto según las verificaciones reportadas por OpenCode.

## Incidente D4 — Falta de verificación de permisos ante operaciones rechazadas

### Tarea delegada
Se solicitó a OpenCode fortalecer las pruebas de RF-GP-05 para comprobar que las operaciones rechazadas no generen permisos otorgados.

### Problema detectado
Las pruebas verificaban que determinadas operaciones fueran rechazadas o que la solicitud conservara su estado, pero no comprobaban en todos los casos que no se hubiera creado un permiso adicional. Esto se debía a que el repositorio falso de permisos no estaba disponible directamente para las aserciones de las pruebas.

### Cómo se detectó
Durante la revisión de las pruebas de `ResolucionSolicitudService`, se identificó que el permiso otorgado no podía observarse directamente en algunos escenarios. Por ello, una prueba podía confirmar el rechazo de una operación sin verificar completamente sus efectos secundarios.

### Corrección aplicada
Se modificaron las pruebas para compartir una instancia observable de `FakePermisoOtorgadoRepository`. Se añadieron aserciones para comprobar que una solicitud rechazada no genere permisos, que una segunda aprobación no cree un permiso duplicado y que una solicitud inexistente no produzca concesiones.

### Verificación realizada
OpenCode reportó que la suite completa pasó con 261 de 261 pruebas satisfactorias. No se añadieron métodos de prueba nuevos; se fortalecieron las afirmaciones de pruebas existentes. También informó que el arnés de SQL Server LocalDB ya había obtenido 22 de 22 comprobaciones satisfactorias, incluidas las verificaciones equivalentes en la base de datos real.

### Requisito relacionado
RF-GP-05, complementando las comprobaciones de transiciones prohibidas de RF-GP-06.

### Estado
Resuelto según las verificaciones reportadas por OpenCode.

## Incidente D5 — Falta de pruebas de persistencia versionadas

### Tarea delegada
Se solicitó a OpenCode proponer e implementar una solución para incorporar las verificaciones de persistencia de RF-GP-05 a la suite oficial de pruebas, de manera que fueran reproducibles después de clonar el repositorio.

### Problema detectado
Las verificaciones anteriores de persistencia se ejecutaban mediante un arnés temporal ubicado fuera del repositorio, por lo que no formaban parte de la suite versionada. Además, varios repositorios falsos utilizados en las pruebas unitarias compartían objetos en memoria, lo que podía ocultar errores relacionados con el guardado real de los datos.

### Cómo se detectó
Durante la auditoría de RF-GP-05 se comprobó que la suite unitaria, por sí sola, no demostraba que los cambios persistieran en SQL Server después de utilizar un contexto nuevo de EF Core. También se identificó que el arnés temporal no estaba disponible automáticamente para otros desarrolladores después de clonar el proyecto.

### Corrección aplicada
Se incorporaron pruebas de persistencia dentro de `SistemaDeReservas.Tests/Persistencia`, utilizando SQL Server LocalDB, repositorios reales de EF Core y una base de datos temporal independiente por prueba.

Las pruebas aplican las migraciones desde una base vacía, verifican los datos mediante contextos nuevos y eliminan las bases temporales al finalizar. También se añadió un mecanismo para omitir las pruebas cuando LocalDB no está disponible, con un modo estricto mediante `REQUIERE_PERSISTENCIA=1` para convertir esa ausencia en un error.

Se actualizaron el README, la matriz de requisitos y la documentación de gestión de permisos para reflejar las nuevas pruebas y sus instrucciones de ejecución.

### Verificación realizada
Según el reporte de OpenCode, se obtuvieron los siguientes resultados:

- Suite completa: 268 de 268 pruebas satisfactorias.
- Pruebas de persistencia: 7 de 7 satisfactorias.
- Modo estricto con LocalDB disponible: 7 de 7 satisfactorias.
- Pruebas no relacionadas con persistencia: 261 de 261 satisfactorias.
- Sin modo estricto y con un origen de datos inválido para la sonda: 7 pruebas omitidas y ningún error de ejecución.
- Con modo estricto y ese mismo origen inválido: las 7 pruebas fallaron como se esperaba, sin omisiones y con código de salida 1.
- Limpieza: la consulta a SQL Server no encontró bases de datos cuyo nombre coincidiera con `Reservas%` después de la ejecución.

### Requisitos relacionados
RF-GP-03, RF-GP-05, RF-GP-06, RD-07, RD-09 y RD-11.

### Estado
Resuelto según las verificaciones reportadas por OpenCode.

## 3. Verificación final

Al finalizar la revisión y las correcciones relacionadas con la gestión de permisos, OpenCode reportó los siguientes resultados de verificación:

- **Suite completa:** 268 de 268 pruebas satisfactorias.
- **Pruebas de persistencia:** 7 de 7 satisfactorias, utilizando SQL Server LocalDB y repositorios reales de EF Core.
- **Pruebas unitarias:** 261 de 261 satisfactorias al ejecutar la suite excluyendo las pruebas de persistencia.
- **Migraciones:** se aplicó la cadena completa de migraciones desde bases de datos nuevas durante las pruebas de persistencia.
- **Persistencia de datos:** se verificaron los cambios mediante contextos nuevos, incluyendo el estado de las solicitudes, los permisos otorgados, los datos de resolución y la consistencia de las fechas.
- **Operaciones inválidas:** se comprobó que las solicitudes rechazadas y los intentos de aprobación repetida no generen permisos adicionales.
- **Modo estricto:** con `REQUIERE_PERSISTENCIA=1`, la ausencia de LocalDB produjo el fallo esperado, evitando que las pruebas de persistencia se omitieran silenciosamente.
- **Limpieza:** se reportó que las bases de datos temporales creadas durante las verificaciones fueron eliminadas correctamente.

También se actualizaron el README y la documentación de requisitos para incluir las nuevas pruebas, sus instrucciones de ejecución y la evidencia correspondiente.

**Estado final:** D1–D5 se consideran resueltos según las correcciones y verificaciones reportadas por OpenCode y revisadas durante el desarrollo. La evidencia de persistencia de RD-09 corresponde a verificaciones realizadas en SQL Server mediante contextos independientes de EF Core.

## 4. Conclusión

Durante el desarrollo de esta asignación se trabajó en la gestión de permisos del Sistema de Reservas de Espacios y en la mejora de su suite de pruebas. La revisión permitió identificar varios aspectos que necesitaban atención, entre ellos un error en las migraciones de la base de datos, la falta de atomicidad al aprobar solicitudes, la inconsistencia potencial en las fechas registradas y algunas pruebas que no verificaban completamente los efectos de las operaciones rechazadas.

A partir de estos hallazgos, se realizaron correcciones en la persistencia, se fortalecieron las pruebas existentes y se incorporaron pruebas de integración con SQL Server LocalDB para comprobar que los cambios quedaran guardados correctamente. También se mejoró la documentación del proyecto para facilitar la ejecución de las pruebas y permitir que otros desarrolladores puedan repetir las verificaciones.

Una de las principales lecciones de este trabajo fue comprender que una suite de pruebas satisfactoria no siempre garantiza que el sistema funcione correctamente en una base de datos real. Por eso, combinar pruebas unitarias con pruebas de persistencia permite detectar problemas que podrían pasar inadvertidos si solo se verifica el comportamiento en memoria.

Finalmente, el uso de OpenCode como herramienta de apoyo facilitó la revisión del código, la identificación de problemas y la propuesta de correcciones. Sin embargo, fue necesario revisar los resultados y comprobar la evidencia antes de considerar resueltos los hallazgos. Este proceso ayudó a mejorar la confiabilidad de la gestión de permisos y a comprender la importancia de mantener pruebas reproducibles y documentación actualizada.