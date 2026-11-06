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
| `posiciones` | Unity → J2 | 20 veces por segundo mientras hay partida | `t`, `animatronicos[] {nombre,x,z}`, `jugador {x,z}` |
| `estado_jugador` | Unity → J2 | Cada tick (0,5 s) y **al instante** si cambia la mirada | `mirando_pantalla`, `tareas_pendientes`, `caja_pct`, `caja_en_peligro` |
| `estado_operador` | Unity → J2 | Cada tick y al cambiar | `cargas`, `max_cargas`, `trampas[]`, `progreso`, `necesario`, `habilidad_puppet`, `ventana_rescate_s`, `multa_factor` |
| `ping_operador` | J2 → Unity | Cada 2 s | `t` |
| `pong_operador` | Unity → J2 | Al recibir un ping | `t` (el mismo) |
| `game_over` | Unity → J2 (y tablet) | Al terminar la partida (`loss`, `win` o `final_victory`) | `result`, `night`, `final_stats` (duración, tareas). La página muestra el fin con «Esperar otra partida» y «Salir», y vuelve sola a esperar cuando llega el siguiente `mapa_puntos` |
| `accion_operador` | J2 → Unity | Nuevas acciones | `accion: "trampa"` + `punto`, o `accion: "rescatar_puppet"`; todas con `id` |
| `mini_tarea_inicio` / `mini_tarea_fin` | J2 → Unity | Empieza y termina la mini-tarea de recarga | — |

- `efecto` gana `tipo` (`ralentizado`, `trampa_colocada`, `trampa_disparada`, `puppet_rescatado`) y `id` (el de la acción que lo causó, 0 si fue por una trampa que se disparó).
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

Un aviso grande arriba en la pantalla de Unity, por 3 s: "Vixy se ralentizó", "Tu compañero puso una trampa", "Trampa: Vixy atrapado", "Tu compañero te salvó de Puppet". La tablet recibe `efecto` pero **no lo muestra todavía** (Flutter ignora los tipos que no conoce); mostrarlo ahí queda pendiente hasta tocar Flutter.

## Qué hay que acordar con Marcelo

1. Nombres de los mensajes y del rol (`operador_connect`).
2. Que `estado_mapa` / `mapa_puntos` vayan solo al operador (hoy filtrado en el relay).
3. Si la tablet debe mostrar también el aviso `efecto`.
4. Qué hacer si el operador se cae a media noche (hoy: reconecta solo y Unity le reenvía el mapa y los estados; sin ping en 6 s los animatrónicos vuelven a su ritmo normal).
5. Las posiciones a 20 Hz (`posiciones`) y el ping de latido (`ping_operador`).
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
