# Latencia del jugador 2 contra el juego real

Se mide con `pruebas/medir_latencia_operador.py`: ida y vuelta de un `ping_operador` hasta que vuelve el `pong_operador` (operador → relay → Unity → relay → operador) y el intervalo entre mensajes `posiciones`.

Meta: p95 del ping < 50 ms en red local.

## 2026-11-03 · todo en la misma laptop (relay + juego + operador)

Comando: `UnityRelay/.venv/bin/python pruebas/medir_latencia_operador.py --lanzar --n 200`

| Medida | n | media | p50 | p95 | max |
|---|---|---|---|---|---|
| Ping (ida y vuelta) | 200 | 28,6 ms | 27,3 ms | 42,3 ms | 86,8 ms |
| Intervalo entre `posiciones` | 240 | 50,4 ms | 46,4 ms | 106,0 ms | 117,8 ms |

Resultado: **cumple la meta** (p95 = 42,3 ms < 50 ms).

Lectura:
- La red local no es lo que pesa (el ping del relay solo está en ~2-5 ms, ver el informe de pruebas de red). Casi todo el tiempo es Unity: procesa los mensajes dentro de `Update` (`DispatchMessageQueue`), o sea, como mucho una vez por cuadro. Con la escena 3D en esta laptop el cuadro dura unos 30 ms, y de ahí sale el ping de ~27 ms.
- Las posiciones llegan en promedio cada 50 ms (20 por segundo), pero con tirones hasta de ~118 ms cuando un cuadro tarda más. La página suaviza ese movimiento interpolando (`logica.js`).
- Para bajar más la latencia hay que subir los cuadros por segundo de Unity (menos carga de escena o calidad gráfica menor), no tocar la red.

## 2026-11-10 · revisión de latencia y conectividad

Mismo comando (`--lanzar --n 200`), misma laptop (Ryzen + AMD Radeon 610M integrada + RTX 5060), con el mapa V2 de Marcelo.

| Configuración | Ping p50 | Ping p95 | Ping máx | Intervalo de posiciones p95 |
|---|---|---|---|---|
| Antes: relay anterior, juego en la integrada, pantalla 2560×1600 | 40,8 ms | 44,0 ms | 72,5 ms | 101,6 ms |
| Solo el juego en la NVIDIA | 7,7 ms | 41,4 ms | 42,8 ms | 60,0 ms |
| Solo el relay arreglado (integrada, 2560×1600) | 22,9 ms | 43,5 ms | 47,8 ms | 90,2 ms |
| **Después: relay arreglado + juego en la NVIDIA** | **6,2 ms** | **11,3 ms** | **12,7 ms** | **56,2 ms** |

Qué se encontró (y se arregló):

1. **El juego se abría en la tarjeta integrada** (AMD 610M), no en la RTX 5060 (`Player.log`: `Renderer: AMD Radeon 610M`), a 2560×1600: unos 20 cuadros por segundo. Unity atiende la red una vez por cuadro, así que el ping era de ~40 ms. *Arreglo:* `iniciar.sh` abre el juego con el render offload de NVIDIA cuando hay una (`SIN_TARJETA_NVIDIA=1` lo desactiva). Con la ventana a 1280×720 en la integrada también bajaba a ~8 ms.
2. **Nagle + ACK demorado: el p95 de ~41 ms.** El cliente de Unity (ClientWebSocket de Mono) abre el socket con `TCP_NODELAY = 0` (comprobado con `strace`), y Linux demora hasta 40 ms el ACK del relay; un mensaje pequeño que sale justo después de otro esperaba ese ACK. Con `ServicePointManager.UseNagleAlgorithm = false` no cambia nada (no lo usa). *Arreglo:* el relay pide `TCP_QUICKACK` con cada mensaje que recibe (`confirmar_de_inmediato`). `pruebas/probar_latencia_del_relay.py` lo vigila.
3. **Un cliente atascado frenaba a todos y terminaba tumbando a Unity.** El relay esperaba (`await client.send`) a que cada cliente aceptara el mensaje: una tablet o un navegador que dejan de leer (Wi-Fi caído, app congelada) dejaban esperando al relay, que dejaba de leer a Unity, y al no contestar esta los ping la daba por perdida y **cerraba la conexión de Unity** (se vio: llegaban 20 de 120 mensajes). *Arreglo:* el relay manda con `broadcast`, sin esperar a nadie; a un cliente lento lo saca el keepalive. Caso 13 de `probar_relay.py`.
4. **Una tablet sin Wi-Fi tardaba hasta 50 s en darse por perdida** (ping cada 20 s + 20 s de espera + 10 s de cierre). Ahora 5 + 5 + 2 = 12 s como máximo (`PING_INTERVALO_S`, `PING_TIMEOUT_S`, `CIERRE_TIMEOUT_S`). Con eso, Unity y el jugador 2 se enteran antes.
5. **Reconectar la tablet reiniciaba la noche** (se comprobó con el juego real: Freddy volvía a P01 al reconectar). La app mandaba otro `connect`. Ahora manda `reconnect` y Unity retoma la noche (ver `docs/PROTOCOLO_JUGADOR2.md`).
6. **Si la tablet se caía con la cuerda de la caja apretada, la cuerda se quedaba apretada** (y la cámara girada): nadie mandaba el «soltar». Ahora el relay avisa a Unity (`tablet_caida`) y los suelta.

No se tocó la red entre dos máquinas: sigue pendiente repetir la medición desde la computadora del jugador 2 y con la tablet física.

## 2026-11-10 · perfil de video: 1920×1080, 60 FPS, 30 posiciones por segundo

Decisiones del equipo (ver `PerfilDeVideo.cs` y la sección «Perfil de video y de hardware» de `docs/PROTOCOLO_JUGADOR2.md`). Misma laptop (AMD Radeon 610M integrada + RTX 5060), `Player.log` del juego exportado.

| Configuración | Resolución de render | FPS medios | Tarjeta |
|---|---|---|---|
| Antes (pantalla nativa) | 2560×1600 | ~20 | integrada |
| Integrada, perfil nuevo | 1920×1080 | 37 | integrada |
| NVIDIA, perfil nuevo | 1280×720 en ventana (prueba) y 1920×1080 en pantalla completa | 60 (el tope) | RTX 5060 |

- **Posiciones:** 29,9 a 30,0 por segundo (intervalo medio 33,3 ms, máximo 48-63 ms). La prueba `probar_operador_con_juego.py` ahora exige 50 en 2 s (llegaron 60).
- **Pantalla completa a 1920×1080:** el log dice `pantalla 1920x1080 (ExclusiveFullScreen), dibuja a 1920x1080, escritorio 2560x1600`. Unity no cambia el modo del escritorio: dibuja a 1920×1080 en un viewport reducido y la imagen se escala para llenar la pantalla. Al cerrar el juego el escritorio sigue en 2560×1600.
- **MediaPipe con la NVIDIA no puede usar la GPU.** Se probaron el vendor EGL de Mesa (`__EGL_VENDOR_LIBRARY_FILENAMES`) y `EGL_PLATFORM=surfaceless`: con la RTX 5060 dibujando el juego, MediaPipe sigue sin conseguir su contexto (`kGpuService … eglMakeCurrent() returned error`; con surfaceless ni siquiera hay configuración EGL). Por eso el runner reintenta con la CPU (`MediaPipe no pudo usar la GPU (BadStatusException); se reintenta con la CPU`) y la cara se detecta (`[Cara] detectada`). Costo: el proceso del juego usó 60-66 % de **un** núcleo en total con la NVIDIA (juego + detección en CPU).
- **Sin cámara no se puede comprobar la detección sola:** `mirando_pantalla` sigue siendo `False` si no hay una cara frente a la cámara; por eso se agregó el `[Cara]` al log.

## Pendiente

- Repetir desde la computadora del jugador 2 (otra máquina, misma red): `--host <IP de la laptop> --iniciar-partida` con el juego abierto.
- Anotar aquí la fecha, la red y los equipos.
