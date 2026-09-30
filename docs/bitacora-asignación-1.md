# Bitácora - Asignación 1

## Tarea 1 - Trabajo con el agente

### Qué le pedí

Le solicité al agente la creación del .gitignore del proyecto indicandole el ambiente en el que estoy trabajando
que es Rider.

### Qué me devolvió

Me devolvió un .gitignore casi completo para mi ambiente y lenguaje.

### Error encontrado

Le faltó poner para que git ignore el ReSharper Local Cache, Rider utiliza internamente
el motor de ReSharper para el análisis de código. Este motor genera carpetas gigantescas
de caché local que no deberian subirse al repositorio.

### Cómo detecté el error

Revisando el .gitignore y tambien investigando acerca de Rider.

### Cómo lo corregí

Simplemente lo agregué en el .gitignore para que asi no se subiera al repositorio.

### Evidencia

Commit: 54b5dfa