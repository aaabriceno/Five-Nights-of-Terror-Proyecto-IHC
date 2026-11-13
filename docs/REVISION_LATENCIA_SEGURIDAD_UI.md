# Revisión de red, latencia, seguridad e interfaz de la tablet (2026-11-12)

Revisión hecha sobre el código en `main` (`9b67617`) con la mirada de alguien que optimiza latencia en juegos en red. Cada hallazgo dice qué pasa, por qué importa en este juego y qué se hizo. Lo que quedó sin hacer está al final, con la razón.

## 1. Arquitectura de red

```
 Tablet (Flutter, Wi-Fi)  ──ws──┐
                                │        ┌──ws (127.0.0.1)──  Juego (Unity, autoridad)
 Página del J2 (navegador) ──ws─┤ relay  │
                                │ :8000  ┘
                                └─ http :8081 (sirve operador.html)
```

- **Unity decide todo**; el relay solo reparte: lo de Unity va a la tablet y al jugador 2 (menos lo que es solo del J2), y lo de ellos va a Unity. No hay estado de juego en el relay.
- **Un solo salto por mensaje** en cada dirección: tablet → relay → Unity. En red local eso son 1-3 ms de red; el resto del tiempo lo pone cada extremo.
- **Transporte:** WebSocket sobre TCP. Mensajes JSON de 100-400 bytes; el más grande (`task_list`, `mapa_puntos`) pesa menos de 10 kB.

### Dónde se gasta el tiempo (presupuesto de latencia)

| Tramo | Tiempo típico | Por qué |
|---|---|---|
| Wi-Fi tablet → PC | 2-10 ms (hasta 100-300 ms con ahorro de energía o 2,4 GHz saturado) | Es el tramo que no controla el código |
| Relay | < 0,5 ms | `broadcast` sin esperar a nadie, `TCP_QUICKACK`, un `json.loads` por mensaje |
| Unity recibe → procesa | 0-16 ms a 60 FPS | Los mensajes se reparten una vez por cuadro (`DispatchMessageQueue` en `Update`) |
| Unity envía | 0-40 ms **antes**, ~0 ahora | El socket de Mono nacía con Nagle (ver 2.1) |
| Navegador / tablet pintan | 1 cuadro (16 ms) | Interpolación en la página del J2 |

Medido antes de esta revisión (misma laptop, loopback): ping del J2 p50 6,2 ms, p95 11,3 ms; posiciones 30/s. Los números de esta revisión están en `pruebas/red/RESULTADOS_JUGADOR2.md`.

## 2. Latencia: hallazgos y cambios

### 2.1 Nagle en el socket de Unity (arreglado)
- **Qué pasaba:** `ClientWebSocket` de Mono abre su socket con el algoritmo de Nagle (`TCP_NODELAY = 0`). Un mensaje chico espera el ACK del anterior, y el sistema demora ese ACK hasta 40 ms. En Linux el relay lo esquivaba pidiendo `TCP_QUICKACK` en cada mensaje; **en Windows o macOS ese truco no existe** y cada mensaje de Unity podía tardar 40 ms de más. `ServicePointManager.UseNagleAlgorithm` no sirve (la implementación de Mono no lo mira; se había probado).
- **Qué se hizo:** `Unity/Assets/scripts/AjusteDeSocket.cs` busca por reflexión el `Socket` escondido dentro del `ClientWebSocket` y le pone `NoDelay = true` apenas abre la conexión. Es puro (sin UnityEngine) y se prueba con el Mono de Unity contra el relay real (`pruebas/PruebaAjusteDeSocket.cs`: «antes del ajuste, NoDelay = False» → `True`). En el juego exportado, `strace` muestra `setsockopt(TCP_NODELAY, [1])` y `Player.log` deja `[Red] TCP_NODELAY activado`. Si una versión futura de Mono cambia los campos, no se encuentra el socket y el juego sigue igual que antes (lo dice el log).

### 2.2 Compresión por mensaje en el relay (arreglado)
- **Qué pasaba:** la biblioteca `websockets` acepta `permessage-deflate` cuando el cliente lo pide, y el navegador y la tablet (Dart) lo piden. El relay comprimía con zlib cada uno de los 30 mensajes por segundo de `posiciones` para ahorrar unos bytes en una red local: CPU y unos cientos de microsegundos por mensaje, y memoria de contexto por cliente.
- **Qué se hizo:** `compression=None` en `websockets.serve`. Prueba: caso 14 de `pruebas/probar_relay.py` (la respuesta del relay ya no trae `Sec-WebSocket-Extensions: permessage-deflate`).

### 2.3 Orden de ejecución en Unity (arreglado)
- **Qué pasaba:** `UnityWebSocketClient.Update` repartía los mensajes en el orden por defecto de Unity, que podía ser **después** del `Update` del controlador del juego: un mensaje llegado entre cuadros se usaba recién en el cuadro siguiente (hasta 16 ms más a 60 FPS, 27 ms a 37 FPS) para la cámara del jugador 1, las tareas y las acciones del jugador 2.
- **Qué se hizo:** `[DefaultExecutionOrder(-100)]` en `UnityWebSocketClient`: los mensajes se reparten antes de que el controlador los necesite.

### 2.4 `Debug.Log` por cada mensaje recibido (arreglado)
- Cada orden de cámara (hasta 7 por segundo mientras se mantiene el botón) escribía una línea con su pila de llamadas en `Player.log`, en el hilo principal. Ahora solo en el editor (`#if UNITY_EDITOR`).

### 2.5 Lo que ya estaba bien (comprobado, sin cambios)
- La tablet (Dart) pone `TCP_NODELAY` en sus sockets HTTP/WebSocket; el navegador también; Python/asyncio también en el relay. El único socket con Nagle era el de Unity.
- El relay no espera a ningún cliente (`broadcast`), el keepalive detecta una tablet sin Wi-Fi en ≤ 12 s, las posiciones salen a 30/s con intervalo fijo y la página interpola.
- 30 posiciones por segundo × ~300 bytes = 9 kB/s: la red no es el cuello de botella; los cuadros por segundo de Unity sí (por eso el perfil de video de 1080p y tope 60).

### 2.6 Recomendaciones para la red de la demo (no son código)
- **PC por cable** si se puede; si no, **Wi-Fi de 5 GHz** para la PC, la tablet y la computadora del jugador 2, en la misma red (no «red de invitados», que aísla clientes).
- En la tablet, **desactivar el ahorro de energía del Wi-Fi** y mantener la pantalla encendida (ya lo hace la app): el ahorro de energía mete ráfagas de 100-300 ms.
- Evitar un router de 2,4 GHz saturado (aulas con muchos dispositivos). Un punto de acceso del celular de 5 GHz suele ir mejor que el Wi-Fi institucional.
- Medir antes de la demo con `pruebas/medir_latencia_operador.py --host <IP de la PC>` desde la computadora del jugador 2: meta p95 < 50 ms.

## 3. Seguridad: errores de programación que abrían vulnerabilidades

El contexto es una demo en red local, sin datos personales. Lo que importa es que nadie de la misma red pueda **arruinar la partida** (cerrar el juego, reiniciar la noche, hacer trampa) ni ejecutar código en el navegador del jugador 2.

| # | Hallazgo | Gravedad | Estado |
|---|---|---|---|
| S1 | Cualquiera de la red podía mandar `unity_connect` y **hacerse pasar por el juego**: `attack`, `game_over`, `task_list` o `estado_mapa` falsos a la tablet y al J2 | Alta | Arreglado: `unity_connect` solo se acepta desde la misma PC (loopback), que es donde corre el juego (`iniciar.sh`). `UNITY_DESDE_CUALQUIER_IP=1` lo permite para montajes raros. Caso 15 |
| S2 | Cualquiera de la red podía mandar `connect` con `modo: "salir"` y **cerrar el juego** (`Application.Quit`), o un `connect` normal y **reiniciar la noche** del jugador 1 | Alta | Arreglado en parte: mientras una tablet juega, un `connect`/`reconnect` con otro `player_id` se rechaza (`conexion_rechazada`, `ya_hay_jugador1`) y no llega a Unity. La misma tablet (mismo `player_id`) siempre puede volver, y su conexión vieja se cierra al instante. Caso 17. **Queda abierto entre partidas** (sin tablet conectada): ver 3.1 |
| S3 | El relay reenviaba a Unity **todo** lo que mandara una tablet o un operador. Un operador podía mandar `task_completed` (resolver las tareas del J1), `dar_cuerda_inicio` (recargar la caja de Puppet) o `tutorial_abrir`; una tablet podía colocar trampas | Media (trampa entre jugadores) | Arreglado: lista de tipos permitidos por rol en el relay; lo demás se descarta. Caso 16 |
| S4 | La página del J2 armaba la lista de amenazas con `innerHTML` y el nombre del animatrónico que llega por la red: un `<img onerror=…>` se **ejecutaba** en el navegador del J2 (XSS). Solo explotable con S1 | Media | Arreglado con `createElement`/`textContent`. Prueba en Firefox: con el código viejo el `onerror` corrió (`window.__xss`); con el nuevo se ve como texto |
| S5 | El relay aceptaba mensajes de hasta 1 MB (el máximo de la biblioteca): memoria sin límite desde cualquier cliente | Baja | Arreglado: tope de 64 kB (`max_size`); un mensaje mayor cierra la conexión con 1009. Caso 14 |
| S6 | La tablet se caía con un mensaje mal formado: `jsonDecode(raw as String) as Map` dentro del `listen`, y `message['type'] as String` o `Task.fromJson` sin comprobar tipos. Un marco binario o un `task_list` con una tarea rota tiraban una excepción fuera del `onError` y dejaban la sesión a medias | Media (robustez) | Arreglado: `WebSocketService.procesarCrudo` solo deja pasar objetos JSON; `GameProvider.handleMessage` aplica cada mensaje dentro de un `try` y no avisa a las pantallas si falla. 7 pruebas nuevas |
| S7 | `player_id` llega por la red y se usa como clave de `PlayerPrefs` (`ihc_next_night_<id>`) sin límite de largo ni de caracteres | Baja | Sin cambios: solo puede escribirlo una tablet aceptada; se anota |
| S8 | `http.server` en `0.0.0.0:8081` sirve la carpeta `operador/` (solo lectura, sin recorrido de directorios gracias a `--directory`) y lista archivos | Informativa | Sin cambios |

### 3.1 Lo que sigue abierto: un código de emparejamiento
Sin ninguna autenticación, **entre partidas** (cuando no hay tablet conectada) cualquiera de la red puede mandar un `connect` y arrancar o cerrar el juego. La forma correcta de cerrarlo es un **código de emparejamiento**: Unity muestra 4 cifras en la pantalla de espera (junto a la IP), la tablet las pide una sola vez en Opciones y las manda en el `connect`; el relay (o Unity) rechaza lo que no las traiga. Son unos 40 líneas en Unity, 20 en el relay y una casilla en Flutter; no se hizo en esta revisión porque cambia el protocolo de la tablet y conviene acordarlo con el equipo.

## 4. Interfaz de la tablet: legibilidad y jugabilidad

Se miró el menú, Opciones, la pantalla de conexión y el menú de tareas en la tablet real (Lenovo 11", 2000×1200) y se cambió lo que entorpecía jugar. Los textos nuevos siguen el tuteo.

| Pantalla | Problema | Cambio |
|---|---|---|
| Menú | El **modo de prueba viene encendido por defecto** y no se ve: tocas «Nuevo juego» y la PC no hace nada, sin pista de por qué. Las opciones del JPEG no respondían al toque | Indicador abajo a la derecha: «Modo de prueba · sin PC» (ámbar) o «PC: 192.168.1.100:8000»; tocarlo abre Opciones y al volver se actualiza. Destello suave al tocar una opción |
| Opciones | Hablaba de «servidor» y «modo simulado»; el campo IP usaba teclado numérico **sin punto** en Android (no se podía escribir una IP); aceptaba `abc` o `192.168.1`; letras de 16 px | Textos para quien juega («IP de la PC», «Jugar sin la PC (modo de prueba)»), dice dónde ver la IP (la pantalla de la PC), teclado numérico con decimal, valida IPv4 y puerto antes de guardar, letras de 22-26 px, botón de 68 px, confirmación «la tablet buscará el juego en …» |
| Conexión | Si el relay rechaza la tablet (otra tablet ya juega), la app decía «Conectado» (cualquier mensaje contaba como respuesta) y entraba a una partida vacía | `conexion_rechazada` → «La PC ya tiene otra tablet» con qué hacer; sin reintentos automáticos (el relay diría lo mismo) |
| Menú de tareas | Nombre a 16 px y tiempo a 12 px: no se leen de reojo mientras se vigila la PC (el resto de la app usa 24-32) | Nombre 24 px, tiempo 18 px, icono 30 px en caja de 56 |

Pruebas: 173 en Flutter (antes 150), con casos nuevos para cada cambio. Versión `0.15.0+24`.

### 4.1 Propuestas que no se hicieron (para decidir con el equipo)
- **Tipografía del juego** en los títulos de la app (la misma del logo «Five Nights of Terror») para que la tablet se sienta parte del juego; hoy usa la fuente del sistema.
- **Encontrar la PC sola** (descubrimiento por UDP o un QR en la pantalla de espera de Unity) en lugar de tipear la IP. Con el código de emparejamiento (3.1) se resuelven los dos problemas de una vez.
- **Tutorial:** explicar los botones de cámara y, para el J2, la terminal.
- **Menú:** dibujar las opciones como widgets (hoy son texto en el JPEG con zonas invisibles: si cambia la imagen hay que recalibrar a mano, y no se puede agrandar el texto ni leerlo con lector de pantalla).

## 5. Rendimiento (sin cambios de código)
- Flutter: la pantalla de juego se reconstruye con cada `estado_puppet`/`night_status` (2 por segundo): despreciable. El menú de tareas repinta cada 2 s para el color de urgencia.
- Unity: el costo está en la escena y en MediaPipe (ver perfil de video). Nada de red pesa.
- Relay: un proceso, un `json.loads` por mensaje, sin compresión: menos de 1 % de un núcleo con 30 mensajes por segundo.

## 6. Cómo comprobar todo

```bash
UnityRelay/.venv/bin/python pruebas/probar_relay.py            # 17 casos (14-17 son de esta revisión)
bash pruebas/probar_reglas_de_operador.sh                        # Mono: 7 pruebas, incluida AjusteDeSocket contra el relay
node UnityRelay/operador/probar_logica.js
UnityRelay/.venv/bin/python pruebas/probar_pagina_operador.py    # Firefox, incluye el XSS
cd Flutter && flutter test                                       # 173
UnityRelay/.venv/bin/python pruebas/probar_operador_con_juego.py # juego real en pantalla virtual
UnityRelay/.venv/bin/python pruebas/medir_latencia_operador.py --lanzar --n 200
```
