# Integración Servidor (Python) ↔ Unity — preguntas para cerrar con el compañero

Objetivo de este documento: llegar a la charla con el compañero con preguntas
concretas, no abstractas, para salir de ahí con el contrato completo y recién
ahí empezar a escribir código de integración (cliente WebSocket en Unity,
ajustes en `PatrullaPorPuntos.cs`, etc). Nada de esto se implementa antes de
tener respuesta.

Contexto: repo único, Unity vive en `unity/` (hermana de `backend/` y
`flutter/`). Unity y `backend/` no comparten código — la única superficie de
contacto es el protocolo JSON por WebSocket. Cada quien puede seguir tocando
su lado (modelos 3D, animaciones, lógica Python interna) sin romper al otro,
mientras nadie cambie ese contrato sin avisar.

## Ya decidido (no preguntar, solo confirmar que sigue en pie)

- **Animatronico 1** (el de la izquierda, con mesh de MediaPipe Unity Plugin
  propio del compañero): su lógica de detección visual (`orientacion_cabeza.cs`,
  `HeadOrientationTracker`) **no se toca**. Palabra del compañero.
- **Animatronico 2 y 3**: el compañero dio permiso explícito de enlazarlos a
  la lógica del servidor (ODM + nivel de IA), hoy no hacen nada porque les
  falta el componente `PatrullaPorPuntos` en la escena.
- Servidor ya emite `animatronic_position` (`{type, animatronic, node,
  timestamp}`) cada vez que cualquier animatronico avanza un nodo — implementado
  en `backend/logicaJuego/juego.py`, sin consumir todavía del lado Unity.

## 1. Anim1: ¿su mirada also debe llegar al servidor?

Hoy hay dos sistemas de "el jugador mira la pantalla" corriendo en paralelo,
sin relación entre sí:
- Python (`backend/gaze/`): 3 zonas (PANTALLA/TABLET/NADA), decide el ataque
  real en `juego.py` (`Animatronico.observar()`).
- Unity (`orientacion_cabeza.cs`, propio de anim1): binario `mirandoPC`,
  decide si anim1 se mueve o se congela, visualmente, en Unity.

**Preguntas para el compañero:**
- El servidor ya decide game-over por mirar pantalla usando SU gaze tracking
  (Python). Si anim1 en Unity decide moverse/congelarse con OTRO gaze
  tracking (el suyo), ¿pueden quedar visualmente inconsistentes? (ej.
  servidor cree que estás mirando pantalla y no ataca, pero anim1 en Unity,
  con su propia cámara, decide que no lo miras y avanza igual).
- ¿Aceptás que anim1 siga usando su mesh/detección visual tal cual (no se
  toca el código), pero que el **resultado** (`mirandoPC`) empiece a
  recibirse también desde el servidor en vez de calcularse localmente? Así
  hay una sola fuente de verdad para el juego, y el mesh de MediaPipe sigue
  siendo 100% tuyo, solo cambia de dónde saca el booleano final.
- Si la respuesta es "no, que Unity siga siendo dueño de su propio
  mirandoPC": entonces el servidor necesita RECIBIR ese estado desde Unity
  (no al revés) para que `juego.py` no dependa de una segunda cámara Python
  corriendo en paralelo. ¿Cuál de las dos direcciones preferís?

**Decidido (2026-09-24):** el binario de Unity (`mirandoPC`) alcanza. Verificado
en `juego.py` → `Animatronico.observar()`: TABLET y NADA caen en la misma
rama (`else`, ambos suman `segundosSinObservar`) — el 3er estado de Python
(TABLET) existe en `backend/gaze/deteccion.py` pero **no se usa distinto de
NADA** en la lógica de ataque real. Diseño de juego confirmado por Anthony:
mirar la tablet cuenta igual que no mirar nada — el animatronico no
"perdona" por estar resolviendo una tarea, fiel al FNAF original. Por lo
tanto no hace falta que Unity distinga 3 zonas, el binario ya es equivalente
en la práctica.

**Corrección importante (2026-09-24, tras leer `backend/gaze/landmarks.py` y
`estimacion.py` completos):** la pregunta "¿podemos usar la malla de mi
compañero en mi server?" ya está resuelta — **ya la usamos**. `backend/gaze/`
corre MediaPipe Face Mesh con `refine_landmarks=True`, que es la MISMA malla
de 478 puntos (468 + 10 de iris) que usa el plugin de Unity del compañero.
No hay dos mallas distintas, hay un solo insumo (la malla MediaPipe) y dos
formas distintas de leerla:
- **Unity (`orientacion_cabeza.cs`):** usa 3 puntos (frente/nariz/mentón),
  1 sola proporción vertical, umbral fijo `1.3f` sin calibrar.
- **Python (`backend/gaze/estimacion.py`):** usa 5 puntos (frente/nariz/mentón
  + 2 pómulos) + iris, calcula pitch Y yaw (2 ejes, no solo vertical),
  combina con posición del iris en un vector 2D, calibrado por usuario
  (`backend/calibracion.json`).

Con la misma malla de insumo, el método Python procesa más señales y está
calibrado — es el más completo de los dos, no al revés. **Camino elegido:
Python (`backend/gaze/`) sigue siendo el detector real y la fuente de verdad
para anim1.** Unity no necesita mandar nada nuevo al servidor — es al
revés: Unity recibe el resultado (`mirandoPC`/zona) del servidor por
WebSocket, y `HeadOrientationTracker` dentro de anim1 deja de calcular con
`CalcularProporcionFacial()` local y pasa a setear `mirandoPC` con lo que
llega del mensaje. El mesh visual de Unity (la cara que se ve en pantalla)
sigue intacto — es presentación, no cambia.

**Lado Python: hecho (2026-09-24).** Agregado en `backend/logicaJuego/juego.py`:
`Juego._emitirEstadoDeGazeSiCambio()`, llamado cada tick del loop principal
justo después de leer `zonaAtencion = self.hiloGaze.obtenerZona()`. Emite
evento `gaze_status` SOLO cuando la zona cambia (no cada 0.5s, evita spam):
```json
{"type": "gaze_status", "zona": "PANTALLA", "mirando_pantalla": true, "timestamp": ...}
```
`mirando_pantalla` (booleano) viaja además de `zona` (string) para que Unity
lo consuma directo sin comparar strings del lado C#. `servidor.py` no
requirió cambios — cualquier tipo de evento no listado cae al envío genérico
(`ServidorJuego.alEventoDeJuego` → `_enviarAlCliente`).

**Lado Unity: pendiente, a implementar por el compañero (o junto).** Cambios
necesarios en el proyecto Unity:
1. Instalar `NativeWebSocket` (Package Manager → Add from git URL:
   `https://github.com/endel/NativeWebSocket.git#upm`).
2. Script cliente nuevo (ej. `ClienteServidor.cs`) que conecte a
   `ws://[IP_SERVIDOR]:8000` (mismo puerto que usa Flutter), parsee mensajes
   JSON entrantes, y guarde el último `mirando_pantalla` recibido en algo
   accesible (variable pública/estática).
3. En `HeadOrientationTracker.cs`, reemplazar el cuerpo de `Update()`:
   ya no llama `CalcularProporcionFacial()` — en su lugar, `mirandoPC` se
   asigna directo desde el valor que llegó del servidor.
4. El mesh/visualización de anim1 no cambia — sigue siendo 100% del
   compañero, solo cambia de dónde sale el booleano final.

**Trade-off a conversar:** con este cambio, Unity deja de poder detectar
mirada de forma standalone (sin servidor corriendo) — antes su cámara local
alcanzaba, después depende de que `backend/servidor.py` esté corriendo y
conectado. Mismo modelo que ya usa Flutter (depende del server real, no hay
fallback standalone). Confirmar que el compañero está de acuerdo con esa
dependencia antes de sacar su código de detección local.

## 2. Anim2 y Anim3: mapeo nodo → posición 3D

El servidor decide a qué nodo del grafo se mueve cada animatronico
(`"escenario"`, `"nodo1"`...`"nodo6"`, `"areaJuegos"`, `"areaFiestas"`,
`"jugador"`) y ya lo emite por WebSocket (`animatronic_position`).

**Preguntas para el compañero:**
- ¿Armás vos una tabla nodo→Transform en Unity (10 puntos fijos en la
  escena, uno por nodo) para que el cliente WebSocket sepa a qué posición 3D
  mover a cada animatronico cuando llega un mensaje? (Recomendado: las
  posiciones 3D son tu dominio, no tiene sentido que el servidor las calcule.)
- Anim2 y anim3, ¿usan el mismo esquema de `NavMeshAgent` que ya tiene
  `PatrullaPorPuntos.cs`, pero recibiendo destino por WebSocket en vez de
  decidir con lógica propia? ¿O preferís que solo "teletransporten"/interpolen
  al punto que dice el servidor, sin pathfinding propio de Unity?
- Los nombres `"Freddy"`, `"Vixy"`, `"Puppet"` en el servidor, ¿coinciden con
  cómo vas a nombrar/identificar los GameObjects de anim2 y anim3 en la
  escena? Hay que acordar el nombre exacto (mismo string, sensible a mayúsculas).

## 3. Fusión ODM + nivel de IA + mirar pantalla (para anim2/anim3)

Ya existe el patrón en el servidor — no es una mecánica nueva a inventar,
`juego.py` ya combina ambas señales para Freddy/Vixy:
```python
atacoAlJugador = animatronico.observar(zonaAtencion, ...)          # mirar pantalla
if not atacoAlJugador:
    atacoAlJugador = animatronico.intentarAtacarPorTareasPendientes(...)  # ODM/tareas sin resolver
```
`observar()` ya depende de en qué nodo está el animatronico (`nodosDeObservacion
= ["nodo3", "nodo6"]`) — o sea, movimiento (ODM/nivel IA) y mirada ya están
acoplados en el diseño actual.

**Preguntas para el compañero:**
- ¿Confirmamos que anim2/anim3 usan exactamente esta misma lógica ya
  implementada (sin inventar mecánica nueva), y lo único que falta es
  conectarlos al servidor?
- Los `nodosDeObservacion` (`nodo3`, `nodo6`) definen desde dónde un
  animatronico "puede verte". ¿Están bien esos 2 nodos para los 3, o cada
  animatronico debería tener su propio punto de observación?

## 4. Linterna de tablet (mecánica nueva, aparte)

Decisión tomada: **no se mete en este sprint de integración.** Es mecánica
adicional (botón en Flutter → luz en escena Unity), no reemplazo de mirar
pantalla — ambas pueden coexistir después. Queda pendiente, sin bloquear la
integración servidor↔Unity actual. (Ver también nota ya guardada sobre
sonido/linterna con diseños en competencia, sin decidir.)

## 5. Protocolo — mensajes que cruzan la frontera

Ya implementados en `backend/servidor.py` / `juego.py`, funcionando con
Flutter, pendientes de consumir en Unity:
- `attack`, `game_over`, `estado_puppet` — ya definidos, formato estable.
- `animatronic_position` (nuevo) — `{type, animatronic, node, timestamp}`.

**Preguntas para el compañero:**
- ¿Falta algún evento que Unity necesite y el servidor no emite todavía? (ej.
  "llegó a destino" explícito, o le alcanza con la posición de nodo y él
  interpola visualmente el trayecto solo)
- ¿Instalamos `NativeWebSocket` (paquete recomendado para Unity) o preferís
  otra librería de cliente WebSocket?

**Importante (agregado 2026-09-24, dos cambios del lado servidor):**

1. **Token obligatorio.** El servidor ahora exige un token compartido en el
   mensaje `connect` (ver `backend/servidor.py`, `TOKEN_SERVIDOR` — se
   genera solo la primera vez que se arranca el server y se imprime en
   consola, persiste en `backend/token_servidor.txt`, gitignored). Todo
   cliente que mande `connect` sin el token correcto recibe
   `connect_rejected` y no se acepta la conexión para iniciar partida.
   `ClienteServidor.cs` (Unity) hoy NO manda ningún `connect` — solo abre
   el socket y escucha `gaze_status` (ver sección 1). Eso significa que
   `gaze_status` solo le llega a Unity si YA hay una partida activa iniciada
   por Flutter (que sí manda `connect` con token) — Unity no puede arrancar
   una partida por su cuenta con el código actual, ni falta que lo haga.

2. **Resuelto (2026-09-25): roles por conexión, no un solo cliente.**
   Confirmado en la práctica que Unity y Flutter conectados a la vez se
   desconectaban entre sí (`"Cliente nuevo reemplaza a uno anterior"`,
   tablet entraba en loop de reconexión cada 3s). Fix: `servidor.py` ahora
   distingue `self.clienteTablet` (uno, dueño de la partida) de
   `self.clientesAliados` (`set`, cualquier cantidad, solo reciben
   broadcast). El rol se decide por el campo `device` del mensaje
   `connect`: `"tablet"` se vuelve dueña (desplaza a cualquier tablet
   anterior, no a los aliados); cualquier otro valor (Unity manda
   `"unity"`) queda como aliado, sin desplazar a nadie. `_enviarAlCliente`
   hace broadcast a la tablet + todos los aliados.
   `unity/Assets/scripts/ClienteServidor.cs` actualizado: manda
   `{"type": "connect", "device": "unity", "token": tokenServidor}` al
   abrir el socket (antes no mandaba ningún `connect` — por eso "funcionaba
   sin chocar" mientras Unity y Flutter nunca coincidían activos a la vez).
   **Falta completar en el Inspector**: el campo nuevo `Token Servidor` en
   el componente `ClienteServidor` de la escena, con el mismo token que usa
   la tablet (ver `backend/token_servidor.txt`).

## 6. Cosas ya diagnosticadas, no bloqueantes

- La cámara en Unity se debe reseleccionar manualmente cada Play (el sample
  de MediaPipeUnity toma la primera cámara del sistema, sin persistir
  elección). Ajuste de código menor, no urgente.
- Carpeta del proyecto Unity renombrada de `Poryecto_IHC/Poryecto_IHC/` a
  `unity/` (hermana de `backend/` y `flutter/`) — hecho 2026-09-24, copia
  verificada archivo por archivo, sin pérdida.

## Cómo usar este documento

Ir pregunta por pregunta con el compañero. Cuando haya respuesta, anotarla
debajo de la pregunta como "**Decidido:** ...", sin borrar la pregunta
original (sirve de registro). Recién con las secciones 1, 2 y 5 cerradas se
empieza a escribir el cliente WebSocket en Unity y a tocar `PatrullaPorPuntos.cs`.
