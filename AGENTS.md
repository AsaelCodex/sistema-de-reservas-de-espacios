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

Actualmente el proyecto se encuentra en la práctica 3 (semanas 4 y 8).

La pieza que se trabaja durante esta etapa es el contrato del módulo
de negocio y sus reglas de transición:

- Pieza 1 - Control de Acceso: implementada y verificada.
- Pieza 2 - Gestión de Permisos: implementada y verificada.
- Módulo de negocio: contrato y reglas de transición en curso.

El Control de Acceso sigue siendo la base de las demás funcionalidades
del Core y las operaciones del sistema deberán quedar protegidas por
él.

## 5. Alcance actual

Durante la práctica 3 se trabajará principalmente con:

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