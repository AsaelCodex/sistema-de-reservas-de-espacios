# API externa — evaluación climática (P4-03)

Documentación de la integración con WeatherAPI para evaluar la
probabilidad de lluvia durante el horario de una reserva.

## 1. Alcance

`GET /api/reservas/{id}/evaluacion-climatica` devuelve una evaluación
propia del sistema a partir del pronóstico **horario** del proveedor.

- No modifica la reserva ni sus transiciones; el estado se conserva.
- No sustituye un aviso meteorológico oficial: es una orientación para
  actividades al aire libre.
- La operación no usa el clima actual ni un horizonte fijo de 72 horas.
- El propietario, la fecha y el horario se leen de la reserva persistida;
  nunca de datos enviados por el cliente.

## 2. Arquitectura

| Capa | Componente | Responsabilidad |
|---|---|---|
| Core | `UbicacionClima`, `HoraPronosticoClima`, `PronosticoClima`, `SolicitudPronosticoClima` | Contratos del pronóstico, sin HTTP |
| Core | `IProveedorClima`, `ErrorProveedorClima`, `ResultadoPronosticoClima` | Puerto y resultado controlado del proveedor |
| Business | `EvaluacionClimaticaService` | Autorización, fechas, cobertura y regla del umbral |
| Business | `OpcionesEvaluacionClimatica`, `EvaluacionClimatica`, `ResultadoEvaluacionClimatica` | Configuración validada y resultado propio |
| Infrastructure | `ClienteWeatherApi` | Cliente HTTP, parsing estricto y clasificación de errores |
| API | `ReservasController.GetEvaluacionClimatica` | Sesión, actor, proyección y códigos HTTP |

El Core no conoce HTTP: el cliente y su formato JSON viven en
Infrastructure.

## 3. Endpoint y autorización

```http
GET /api/reservas/{id}/evaluacion-climatica
Authorization: Bearer <token>
```

- La sesión se valida con `SesionService` antes de invocar al negocio.
- Solo el **propietario** de la reserva o un **Administrador** pueden
  consultarla.
- La autorización se comprueba antes de consultar al proveedor.
- Otro usuario Estándar recibe `403` y el proveedor no se invoca.
- Una reserva inexistente recibe `404` sin consultar al proveedor.

## 4. Regla de evaluación

1. Se toman las horas completas que intersectan el intervalo
   `[HoraInicio, HoraFin)`.
   - `10:00–12:00` evalúa `10:00` y `11:00`.
   - `10:30–12:30` evalúa `10:00`, `11:00` y `12:00`.
2. Se lee `chance_of_rain` de cada hora.
3. La probabilidad evaluada es `max(chance_of_rain)`.
4. Se compara con `Clima:UmbralProbabilidadLluvia` (60 por defecto):
   - Si `máxima >= umbral`, `advertenciaLluvia` es `true` y el mensaje
     sugiere considerar un espacio cubierto.
   - Si `máxima < umbral`, `advertenciaLluvia` es `false`; el mensaje
     aclara que **no** se garantiza ausencia de lluvia.
5. La cobertura debe ser completa: si falta una sola hora esperada, la
   operación responde `422` y no calcula con datos parciales.

El umbral debe estar entre 0 y 100 y se valida al arrancar y en el
servicio.

## 5. Fechas y horizonte

- Se rechaza una fecha anterior a la fecha local del recinto (`422`).
- Se rechaza un horario que ya inició, incluso si empieza exactamente
  ahora (`422`).
- El horizonte no se fija en horas: se deduce de las fechas y horas que
  devuelve el proveedor.
- Si la fecha reservada no aparece en el pronóstico devuelto, la
  operación responde `422` (`FueraDeHorizonte`).
- Se solicitan `Clima:DiasPronostico` días (3 por defecto) y se valida
  contra la cobertura real.

La zona horaria del recinto es `America/Santo_Domingo` (UTC-4, sin
horario de verano). Las fechas se comparan en esa zona, no en UTC.

## 6. Configuración

```json
{
  "Clima": {
    "Ubicacion": {
      "Nombre": "ITLA, La Caleta, Santo Domingo Este",
      "Latitud": 18.4512,
      "Longitud": -69.6626,
      "ZonaHoraria": "America/Santo_Domingo"
    },
    "UmbralProbabilidadLluvia": 60,
    "TimeoutSegundos": 10,
    "DiasPronostico": 3
  },
  "WeatherApi": {
    "ApiKey": ""
  }
}
```

| Variable de entorno | Uso | Valor por defecto |
|---|---|---|
| `Clima__Ubicacion__Nombre` | Nombre mostrado de la ubicación | ITLA, La Caleta, Santo Domingo Este |
| `Clima__Ubicacion__Latitud` | Latitud del recinto | `18.4512` |
| `Clima__Ubicacion__Longitud` | Longitud del recinto | `-69.6626` |
| `Clima__Ubicacion__ZonaHoraria` | Zona IANA del recinto | `America/Santo_Domingo` |
| `Clima__UmbralProbabilidadLluvia` | Umbral 0–100 | `60` |
| `Clima__TimeoutSegundos` | Tiempo de espera del cliente (1–60) | `10` |
| `Clima__DiasPronostico` | Días solicitados (1–14) | `3` |
| `WeatherApi__ApiKey` | Clave del proveedor | vacía |

- La ubicación es **única** para todos los espacios de esta entrega y no
  se deduce del nombre del espacio.
- La ubicación de demostración es el ITLA en La Caleta, Santo Domingo
  Este (`18.4512, -69.6626`).
- La falta de `WeatherApi__ApiKey` no impide arrancar: solo esta
  operación responde `503`.
- La clave real se define en `.env` (ignorado por Git y excluido del
  contexto de construcción) o por entorno. `.env.example` solo contiene
  un marcador ficticio.
- En el contenedor, `docker-compose.yml` propaga
  `WeatherApi__ApiKey: "${WeatherApi__ApiKey:-}"`.

## 7. Proveedor y operación remota

- Proveedor: WeatherAPI.com.
- Operación: `GET https://api.weatherapi.com/v1/forecast.json`.
- Parámetros: `key`, `q` (latitud,longitud), `days`, `aqi=no`,
  `alerts=no`.
- Campo usado: `forecast.forecastday[].hour[].chance_of_rain`.
- Referencia temporal: `hour[].time` (`yyyy-MM-dd HH:mm`, hora local) y
  `hour[].time_epoch`; el cliente exige que ambas coincidan en la zona
  del recinto.
- El host y el esquema son fijos; el cliente no sigue redirecciones.
- El tiempo de espera es propio y distinto de la cancelación del
  usuario; la cancelación del usuario se propaga.
- No se registra la URL con la clave ni se expone el cuerpo del
  proveedor.

## 8. Errores y códigos HTTP

| Código | Cuándo |
|---|---|
| `200` | Evaluación calculada con cobertura completa |
| `400` | Datos de la reserva inválidos para evaluar |
| `401` | Sesión ausente, desconocida, vencida o cerrada |
| `403` | Actor sin permiso (otro Estándar) |
| `404` | Reserva inexistente |
| `422` | Fecha pasada, horario iniciado, fuera de horizonte o cobertura incompleta |
| `502` | Error remoto o respuesta del proveedor que no se puede interpretar |
| `503` | Clave ausente o inválida, cuota agotada o proveedor no disponible |
| `504` | Tiempo de espera agotado |

Los mensajes son controlados y no incluyen trazas, rutas internas,
claves ni URLs del proveedor.

## 9. Ejemplo real

Verificación realizada el **2026-11-10** con el proyecto Compose
aislado `reservas-p403-verificacion` y una clave real definida en
`.env` (no incluida aquí). La reserva se creó para el día siguiente,
`2026-11-11`, de `10:00` a `12:00`, en `America/Santo_Domingo`.

Solicitud (la clave se define por entorno, nunca en el comando):

```powershell
$token = "<token-de-sesión>"
curl.exe -s "$base/api/reservas/<id>/evaluacion-climatica" `
  -H "Authorization: Bearer $token"
```

Respuesta real (`200`), sin la clave y sin datos internos:

```json
{
  "reservaId": "84d11dff-2e23-444d-b2fa-baddf42b1de7",
  "fecha": "2026-11-11",
  "horaInicio": "10:00:00",
  "horaFin": "12:00:00",
  "zonaHoraria": "America/Santo_Domingo",
  "ubicacion": {
    "nombre": "ITLA, La Caleta, Santo Domingo Este",
    "latitud": 18.4512,
    "longitud": -69.6626
  },
  "probabilidadMaximaLluvia": 19,
  "umbralProbabilidadLluvia": 60,
  "advertenciaLluvia": false,
  "horasEvaluadas": [
    { "hora": "10:00:00", "probabilidadLluvia": 19 },
    { "hora": "11:00:00", "probabilidadLluvia": 14 }
  ],
  "orientacion": "Orientación para actividades al aire libre.",
  "mensaje": "No se alcanza el umbral configurado de advertencia de lluvia. Esto no garantiza ausencia de lluvia; es una orientación para actividades al aire libre."
}
```

Los valores concretos de lluvia cambian con el pronóstico; lo que se
verifica es la forma de la respuesta y la regla aplicada.

Durante la misma verificación:

- Con una **clave ficticia** la API respondió `503` con el mensaje de
  clave rechazada (sin exponer la clave).
- **Sin clave** la API arrancó y respondió `503` con el mensaje de
  evaluación no configurada.
- `GET /api/reservas` y `GET /api/documentos` siguieron respondiendo
  `200` con la sesión de Administrador.

## 10. Ejemplos simulados (pruebas automatizadas)

Los siguientes datos son **simulados** por las pruebas y **no** son una
respuesta real de WeatherAPI. Se usan para verificar la regla sin red.

Entrada horaria simulada para una reserva `10:00–12:00`:

```json
[
  { "hora": "10:00", "probabilidad": 59 },
  { "hora": "11:00", "probabilidad": 61 }
]
```

Resultado esperado: `probabilidadMaximaLluvia = 61`,
`advertenciaLluvia = true` con umbral 60. Con `60` también advierte;
con `59` no advierte.

Otro caso simulado: `10:30–12:30` con horas `10:00`, `11:00`, `12:00`
y `13:00` en el pronóstico; solo se evalúan las tres primeras.

Los archivos de prueba están en:

- `SistemaDeReservas.Tests/Clima/EvaluacionClimaticaServiceTests.cs`
- `SistemaDeReservas.Tests/Clima/ClienteWeatherApiTests.cs`
- `SistemaDeReservas.Tests/Clima/OpcionesEvaluacionClimaticaTests.cs`
- `SistemaDeReservas.Tests/API/ReservasControllerTests.cs`

## 11. Distinción real vs. simulado

| Aspecto | Pruebas automatizadas | Verificación manual |
|---|---|---|
| Proveedor | `HttpMessageHandler` falso | WeatherAPI real |
| Clave | Ficticia o ausente | Real, leída de `.env` |
| Red | No se usa | Sí |
| Reloj | `FakeTimeProvider` fijo | Fecha real del entorno |
| Datos | JSON construido por la prueba | Pronóstico real |
| Propósito | Regresión y reglas | Confirmar integración y arranque |

Las pruebas automatizadas nunca llaman a la API real ni necesitan una
clave. La verificación real se hizo en un proyecto Compose aislado para
no tocar volúmenes del usuario; el proyecto se eliminó al terminar.

## 12. Limitaciones conocidas

- La ubicación es única para todos los espacios; no hay ubicación por
  espacio ni detección de si el espacio es exterior o cubierto.
- El plan gratuito de WeatherAPI ofrece un horizonte corto (3 días); si
  la fecha queda fuera, se responde `422` y no se inventan datos.
- La evaluación es orientativa y no reemplaza un aviso oficial.
- No se implementa caché de pronósticos: cada consulta válida invoca al
  proveedor.
- La auditoría de esta operación (RF-DOC-06/RF-AUD) queda fuera de
  alcance de esta práctica.
