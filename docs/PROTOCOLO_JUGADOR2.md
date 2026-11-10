# Protocolo del jugador 2 (operador)

> **Estado: propuesta para revisar con Marcelo antes de unir a `main`.**
> Rama: `jugador2-operador`. Nada de esto cambia los mensajes que ya usan la tablet y Unity.

## Idea

Cooperativo asimétrico (idea A). El **jugador 1** es el vigilante (PC con cámara + tablet, como hoy). El **jugador 2** es el **operador**: otra computadora con un navegador, que ve un **minimapa 2D** con los animatrónicos y puede **retrasarlos**. Unity sigue siendo la autoridad; el navegador solo pide y muestra.

```
 Navegador (J2) ──operador_connect / accion_operador──▶ Relay ──▶ Unity
 Navegador (J2) ◀── mapa_puntos / estado_mapa / efecto / accion_rechazada ── Relay ◀── Unity
 Tablet (J1)    ◀── efecto (y todo lo de siempre) ── Relay ◀── Unity
```

## Cambios en el relay (`UnityRelay/relay.py`)

- Rol nuevo **operador**, que se anuncia con `operador_connect`.
- Antes, un cliente que no fuera Unity ni tablet no tenía rol; si se hubiera anunciado con `connect` lo habrían tratado como tablet y su `connect` (que el relay guarda) habría iniciado una partida.
- `operador_connect` **no** se guarda como `connect`; solo se reenvía a Unity.
- Lo que manda Unity llega a tablets y operadores, **salvo** `estado_mapa`, `mapa_puntos` y `accion_rechazada`, que van solo a operadores (la tablet no los usa).
- Lo que manda el operador va solo a Unity.

## Mensajes

| Tipo | De → a | Cuándo | Campos |
|---|---|---|---|
| `operador_connect` | J2 → Unity | Al conectarse | — |
| `mapa_puntos` | Unity → J2 | Al inicio de cada noche y cuando llega `operador_connect` | `puntos[] {nombre,x,z}`, `jugador {x,z}` |
| `estado_mapa` | Unity → J2 | Cada tick (0,5 s) y al aceptar una acción | `partida_activa`, `noche`, `hora`, `animatronicos[] {nombre,x,z,retraso_s,espera_s}` |
| `accion_operador` | J2 → Unity | El jugador 2 pulsa **Retrasar** | `accion: "retrasar"`, `objetivo: "freddy"\|"vixy"\|"puppet"` |
| `efecto` | Unity → J1 y J2 | Unity aceptó la acción | `animatronic`, `tipo: "ralentizado"`, `segundos`, `message` |
| `accion_rechazada` | Unity → J2 | Unity no la aceptó | `objetivo`, `motivo: "sin_partida"\|"enfriamiento"\|"desconocido"`, `espera_s` |

### Mensajes de la segunda versión (2026-11-03)

Para que el jugador 2 vea lo que hace el jugador 1 y para las acciones nuevas. Todos son propuesta, igual que los anteriores.

| Tipo | De → a | Cuándo | Campos |
|---|---|---|---|
| `posiciones` | Unity → J2 | 30 veces por segundo mientras hay partida (cada 33 ms; antes 20) | `t`, `animatronicos[] {nombre,x,z}`, `jugador {x,z}` |
| `estado_jugador` | Unity → J2 | Cada tick (0,5 s) y **al instante** si cambia la mirada | `mirando_pantalla`, `tareas_pendientes`, `caja_pct`, `caja_en_peligro` |
| `estado_operador` | Unity → J2 | Cada tick y al cambiar | `cargas`, `max_cargas`, `trampas[]`, `progreso`, `necesario`, `habilidad_puppet`, `ventana_rescate_s`, `multa_factor` |
| `ping_operador` | J2 → Unity | Cada 2 s | `t` |
| `pong_operador` | Unity → J2 | Al recibir un ping | `t` (el mismo) |
| `game_over` | Unity → J2 (y tablet) | Al terminar la partida (`loss`, `win` o `final_victory`) | `result`, `night`, `final_stats` (duración, tareas). La página muestra el fin con «Esperar otra partida» y «Salir», y vuelve sola a esperar cuando llega el siguiente `mapa_puntos` |
| `jugador1_salio` | Relay → J2 | La tablet mandó `disconnect` (`motivo: "salio"`) o se cayó (`"conexion_perdida"`) | `motivo`. La página muestra «el jugador 1 se fue de la partida» |
| `juego_cerrado` | Relay → J2 | Se cerró el Unity | La página muestra «el juego se cerró» |
| `operador_salir` | J2 → Relay | J2 pulsa «Salir» o «Desconectar» | Salir a propósito no cuenta como caída |
| `operador_rechazado` | Relay → J2 | J2 intenta volver pasados 15 s de su caída | `motivo: "tiempo_agotado"`; el relay cierra la conexión |
| `accion_operador` | J2 → Unity | Nuevas acciones | `accion: "trampa"` + `punto`, o `accion: "rescatar_puppet"`; todas con `id` |
| `mini_tarea_inicio` / `mini_tarea_fin` | J2 → Unity | Empieza y termina la mini-tarea de recarga | — |

- `efecto` gana `tipo` (`ralentizado`, `trampa_colocada`, `trampa_disparada`, `puppet_rescatado`, y desde 2026-11-10 `operador_conectado` y `operador_perdido`) y `id` (el de la acción que lo causó, 0 si fue por una trampa que se disparó). Con `puppet_rescatado` llega además `multa_factor` (cuántas veces más rápido se descarga ahora la caja: 2, luego 4...); en los demás efectos vale 0.
- `camara` (tablet → Unity, 2026-11-10): `direccion` -1 (izquierda), 1 (derecha) o 0 (soltó). Son los dos botones grandes de la tablet, que se **mantienen apretados**: mientras el dedo siga apoyado la tablet repite la orden cada 0,15 s y Unity gira la cámara del jugador 1 (70°/s, hasta ±75°); al soltar vuelve sola al frente (90°/s). Si pasan 0,5 s sin órdenes Unity da el botón por soltado, así que un «soltar» perdido por la red no deja la cámara girando. Se centra al empezar y al terminar la partida.
- `posiciones` gana `camara_yaw` (grados; 0 = al frente, negativo = izquierda): la página del jugador 2 lo dibuja como un cono de visión desde J1 y lo escribe en el panel («Cámara: Izquierda 45°»).
- `mapa_puntos` gana `rutas[] {animatronico, puntos[]}` (2026-11-10): el camino fijo de cada animatrónico, en orden, desde donde nace hasta el punto penúltimo (sin el jugador 1). Es la guía del jugador 2: la página la dibuja con líneas de colores y la escribe en cada tarjeta de amenaza. El jugador 1 no la recibe. Los desvíos ocasionales no van en la guía.
- **La caja de Puppet (2026-11-10):** dura **30 s cada noche** (ya no baja de noche en noche; antes iba de ~51 s a ~34 s según la noche). Lo único que la acorta son los rescates del jugador 2: cada uno la deja en la mitad (30 s → 15 s → 7,5 s), con **un mínimo de 7,5 s** (`SegundosMinimosDeLaCaja`): más rescates siguen valiendo pero ya no la acortan. Vuelve a 30 s al empezar otra noche o partida. Darle cuerda la llena en 15 s y la multa no lo cambia. Unity lo manda en `segundos_caja` (en `estado_puppet`, `estado_operador` y `efecto puppet_rescatado`), además de `multa_factor`; la tablet y la página dicen los segundos ("La caja dura 15 s"). Con un Unity viejo (sin `segundos_caja`) la tablet cae al texto "×2 más rápida".
- `estado_puppet` (a la tablet, cada 0,5 s) gana `multa_factor` (1 = sin multa). Es opcional: una tablet o un Unity viejos lo ignoran y la caja se ve a su ritmo.
- `accion_rechazada` gana `id` y los `motivo` `sin_cargas`, `punto_ocupado`, `punto_invalido`, `sin_habilidad`, `sin_ventana`, `mini_tarea_muy_rapida`, `mini_tarea_sin_inicio`.
- El relay envía `posiciones`, `estado_jugador`, `estado_operador` y `pong_operador` **solo** a operadores.
- El `ping_operador` además le dice a Unity que el operador sigue conectado: el relay no avisa cuando un navegador se cae, así que Unity lo da por caído si pasan 6 s sin ping.

Coordenadas `x`, `z` en metros de la escena de Unity (vista desde arriba). El minimapa se dibuja con las rutas (waypoints) de los animatrónicos; no hace falta una imagen del mapa.

## Reglas (propuesta de balance, a ajustar con el profesor)

`Unity/Assets/scripts/FrenoDeOperador.cs`, probada fuera del editor con `pruebas/probar_freno_de_operador.sh`:

- **Retrasar** baja a la **mitad** la velocidad del animatrónico durante **8 s**.
- **Espera** de **20 s** (desde el uso) antes de volver a retrasar al mismo animatrónico. Cada animatrónico tiene su propia espera.
- Retrasar afecta: la espera entre avances (Freddy/Vixy), la amenaza por tareas pendientes (Vixy), la salida de Puppet de la caja y la cuenta del ataque desde el penúltimo punto.
- Retrasar **no** salta la mirada de Freddy ni las tareas de Vixy: solo gana tiempo.
- Sin partida en curso, la acción se rechaza (`sin_partida`).
- Cada noche nueva reinicia retrasos y esperas.

### Mecánica nueva del jugador 2

`Unity/Assets/scripts/RecursosDeOperador.cs`, `RescateDePuppet.cs`, probadas con `pruebas/probar_reglas_de_operador.sh`. Valores iniciales para ajustar con el profesor:

- **Trampa:** empieza con 1 carga (máximo 3). La coloca en un punto de las rutas; el primer animatrónico que pasa a 1,5 m queda **quieto 6 s** y la trampa se gasta. Puppet dentro de su caja no la activa.
- **Mini-tarea de recarga:** repetir una secuencia de 4 símbolos. Da **1 carga** y **1 de progreso**. Unity rechaza una mini-tarea terminada en menos de 3 s (no se puede fingir desde la página).
- **Rescate de Puppet:** con 3 mini-tareas completadas se desbloquea. Cuando Puppet sale de su caja se abre una **ventana de 6 s** en la que Puppet se queda quieto. Si J2 rescata dentro de la ventana, Puppet vuelve a su caja, la caja se llena, J1 recibe el aviso y la **caja se descarga al doble de rápido** desde ahí (cada rescate lo multiplica otra vez). Usar el rescate gasta la habilidad.
- **Más agresividad con dos jugadores:** mientras haya un operador conectado, Freddy avanza con **0,4** veces su intervalo (unos 9 s en vez de 22 s en la noche 1) y Vixy con **0,7**. Puppet no cambia.

## Latencia (medida)

`pruebas/medir_latencia_operador.py`; resultados en `pruebas/red/RESULTADOS_JUGADOR2.md`. En la misma laptop, p95 del ping 42 ms (meta < 50 ms). Falta repetir desde la otra computadora.

## Lo que ve el jugador 1

**En el PC (Unity):** un aviso grande arriba por 3 s: "Vixy se ralentizó", "Tu compañero puso una trampa", "Trampa: Vixy atrapado", "Tu compañero te salvó de Puppet (caja ×2 más rápida)", "Tu compañero se conectó: los animatrónicos van más rápido".

**En la tablet (Flutter, desde 2026-11-10):** bajo el reloj de la noche, siempre a la vista:

- **El aviso** de lo último que hizo el compañero, 5 s, con título y detalle ("Tu compañero te salvó de Puppet" / "Pero la caja se descarga ×2 más rápido"), y una vibración distinta: un toque breve (trampa, retraso), dos toques (rescate), uno largo (se cayó). El texto lo arma la tablet a partir de `tipo`, `animatronic`, `segundos` y `multa_factor`; el `message` de Unity solo se usa si llega un `tipo` que la tablet no conoce.
- **Dos datos que no caducan:** "Compañero conectado / desconectado" (con compañero, Freddy y Vixy avanzan más rápido) y "Caja de Puppet ×2 más rápida" mientras la multa siga.

**En la página del jugador 2:** el mismo `efecto` se le cuenta en segunda persona ("Retrasaste a Freddy por 5 s", "Rescataste a Puppet · la caja del jugador 1 se descarga ×2 más rápido") y el panel del jugador 1 muestra la multa de su caja. Su propia conexión o caída no se le anuncia.

## Qué hay que acordar con Marcelo

1. Nombres de los mensajes y del rol (`operador_connect`).
2. Que `estado_mapa` / `mapa_puntos` vayan solo al operador (hoy filtrado en el relay).
3. ~~Si la tablet debe mostrar también el aviso `efecto`~~ (hecho el 2026-11-10; falta probarlo en la tablet física).
4. Qué hacer si el operador se cae a media noche (hoy: reconecta solo y Unity le reenvía el mapa y los estados; sin ping en 6 s los animatrónicos vuelven a su ritmo normal).
5. Las posiciones a 30 Hz (`posiciones`) y el ping de latido (`ping_operador`).
6. La ventana de 6 s para rescatar a Puppet y la multa de doble descarga.
7. Los valores de agresividad (Freddy ×0,4, Vixy ×0,7) y de las trampas.

## Cómo probarlo

```bash
# Pruebas automáticas
UnityRelay/.venv/bin/python pruebas/probar_relay.py      # relay, incluye el rol operador
./pruebas/probar_freno_de_operador.sh                    # regla de Retrasar
./pruebas/probar_reglas_de_operador.sh                   # trampas, recargas, rescate de Puppet, agresividad
node UnityRelay/operador/probar_logica.js                # suavizado, medidor y mini-tarea de la página
UnityRelay/.venv/bin/python pruebas/probar_pagina_operador.py   # la página en Firefox, con un Unity falso
UnityRelay/.venv/bin/python pruebas/probar_operador_con_juego.py   # con el juego exportado
UnityRelay/.venv/bin/python pruebas/medir_latencia_operador.py --lanzar   # latencia contra el juego real

# Con el juego: relay + juego + página del operador
UnityRelay/.venv/bin/python UnityRelay/relay.py &
python3 -m http.server 8081 --directory UnityRelay/operador &
# abrir http://<IP-de-la-laptop>:8081/operador.html en la otra computadora
# (con el juego abierto y una partida iniciada desde la tablet)
```

## Correrlo (juego real)

1. Exportar el juego con el código de esta rama: `./exportar_linux.sh` (reemplaza `Unity/Builds/Linux`).
2. Abrir `iniciar.sh` (o el acceso directo del escritorio). Levanta el relay, la **página del jugador 2** en el puerto 8081 y el juego, y escribe en la terminal la dirección para la otra computadora, por ejemplo `http://192.168.1.7:8081/operador.html`.
3. En la otra computadora (misma red que la laptop), abrir esa dirección y pulsar **Conectar**. Debe decir "Conectado" y mostrar el minimapa.
4. Iniciar una noche desde la tablet. Los animatrónicos aparecen en el minimapa y los botones **Retrasar** se activan.
5. El jugador 2 pulsa **Retrasar**: el jugador 1 ve el aviso arriba en la pantalla de Unity.

Si la otra computadora no abre la página: misma red, firewall (puertos 8000 y 8081) y la IP correcta (ver `pruebas/red/LEEME.md`).

## Si alguien se va
- **Jugador 1 se va** (menú de la tablet, tablet caída o juego cerrado): la partida no puede seguir; J2 ve una pantalla que lo explica con «Esperar otra partida» y «Salir».
- **Jugador 2 se cae**: el juego sigue sin cambios (Unity ya baja la agresividad cuando no hay latidos, 6 s). La página reintenta 3 veces, una cada 5 s, dentro de 15 s (`VENTANA_PARA_VOLVER_MS`, `MS_ENTRE_INTENTOS`, `MAX_INTENTOS` en `operador.js`) y muestra «SIN SEÑAL · RECONECTANDO · INTENTO n DE 3 · QUEDAN x s».
- El **relay** lo hace cumplir: pasados 15 s de la caída (`VENTANA_OPERADOR_S`, por defecto 15) rechaza `operador_connect` con `operador_rechazado` hasta que la tablet mande un `connect` (partida nueva) o Unity mande `game_over`. Salir con `operador_salir` no abre la ventana.
- La página ofrece «Reconectar» tras rendirse; si el relay lo rechaza, lo explica y no insiste.


## Cómo se mueven los animatrónicos (2026-11-10)

- **Freddy y Vixy siguen una ruta fija**, punto por punto y en orden: nunca se saltan uno ni retroceden. Freddy: `P01 → P03 → P04 → P05 → P06 → P07`; Vixy: `P02 → P11 → P12 → V01 → V02 → V03 → V04`; Puppet (sin cambios): `P13 → P14 → P15 → P16 → V05 → V06 → V07 → V08`.
- **Desvíos ocasionales** (25 % por tramo que lo tiene, `ProbabilidadDeDesvio`): Freddy se asoma a `P08` o `P09` entre `P04` y `P05` (cuesta unos 20 s); Vixy puede ir a `P10` justo al salir. Después vuelve a la ruta. Es la sorpresa: no sale en la guía del jugador 2.
- **Tiempo:** Freddy avanza cada 80–120 % del intervalo de la noche (antes era exacto); Vixy sigue dependiendo de las tareas pendientes.
- `P10` deja de ser parte de la ruta de Vixy porque el escenario no tiene puerta al pasillo izquierdo: ir y volver son 35 m. Si Marcelo abre una puerta en el muro oeste del escenario, se puede volver a poner.
- La trampa se ve en el mapa del jugador 1: un disco rojo con una luz que late; desaparece cuando un animatrónico la pisa o termina la partida.

## Opciones de prueba del juego

Solo para las pruebas automáticas (`pruebas/probar_vixy_y_puppet_con_juego.py`); sin ellas el juego es el de siempre:

- `-prueba-sin-freddy`: Freddy no aparece (así Vixy y Puppet pueden terminar su recorrido sin que él acabe la partida).
- `-prueba-tareas-cada <s>`: una tarea nueva cada `<s>` segundos en vez de cada 30 (Vixy solo avanza con 6 tareas pendientes en la noche 1, y la noche dura 120 s).

## Perfil de video y de hardware (2026-11-10)

Los números acordados viven juntos en `Unity/Assets/scripts/PerfilDeVideo.cs` y se aplican al abrir el juego (`AjusteDeVideo.cs`):

- **Resolución estándar 1920×1080** en pantalla completa (`ExclusiveFullScreen`). Unity dibuja a 1920×1080 y la pantalla lo escala, aunque el escritorio sea más grande (se vio en `Player.log`: `dibuja a 1920x1080, escritorio 2560x1600`). Si se lanza con `-screen-width`, `-screen-height`, `-screen-fullscreen` o `-popupwindow`, el perfil no toca la pantalla (así abren el juego las pruebas, en ventana chica).
- **Máximo 60 cuadros por segundo**, con el vsync apagado (en una pantalla de 90 Hz el vsync dejaría pasar de 60).
- **30 posiciones por segundo** hacia el jugador 2 (antes 20): intervalo de 33 ms.
- **MediaPipe (detección de la cara) en la GPU, con respaldo a la CPU.** En una PC sin tarjeta (Mesa por software: `llvmpipe`/`softpipe`) o con `-mediapipe-cpu` va directo a la CPU; si la GPU falla (en esta laptop con la NVIDIA no consigue su contexto EGL: `kGpuService … eglMakeCurrent() returned error`), el juego lo detecta, lo anota en el log y reintenta con la CPU. Sin esto Freddy no se congelaba nunca porque la cámara no detectaba la cara.
- `Player.log` deja `[Video]` (resolución, tarjeta y FPS medios cada 15 s) y `[Cara]` (cuándo se detecta y se pierde la cara) para comprobar el rendimiento de cualquier PC sin herramientas externas.

## Conexión de la tablet y del relay (2026-11-10)

- **La tablet se cae y vuelve:** la app manda `reconnect` (en vez de `connect`, que reinicia la noche). El relay lo reenvía a Unity y avisa al jugador 2 con `jugador1_volvio`; Unity retoma la noche en curso y le manda a la tablet `night_status`, `task_list` y `estado_puppet` para ponerla al día. Sin partida en curso (Unity se reinició o la noche ya terminó) lo trata como un `connect`. El relay no toca el `connect` guardado.
- **La tablet se cae sin despedirse:** el relay manda a Unity `tablet_caida` (Unity suelta la cuerda de la caja y centra la cámara) y al jugador 2 `jugador1_salio` con `motivo: "conexion_perdida"`. La página del jugador 2 ya no lo toma como el fin de la partida: muestra «JUGADOR 1 SIN CONEXIÓN · ESPERANDO QUE VUELVA» hasta que llega `jugador1_volvio`. Con `disconnect` (salir a propósito) todo sigue como antes.
- **Keepalive del relay:** ping cada 5 s, 5 s de espera y 2 s para cerrar (antes 20 + 20 + 10): una tablet o un navegador sin red se dan por perdidos en 12 s como máximo. Se cambia con las variables `PING_INTERVALO_S`, `PING_TIMEOUT_S` y `CIERRE_TIMEOUT_S`.
- **El relay no espera a ningún cliente** (`broadcast`): uno atascado no atrasa a los demás ni hace que Unity sea dada por perdida.
- **El relay pide `TCP_QUICKACK`** al recibir cada mensaje (en Linux): el cliente de Unity tiene Nagle encendido y sin esto un mensaje podía esperar hasta 40 ms.
