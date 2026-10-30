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

Coordenadas `x`, `z` en metros de la escena de Unity (vista desde arriba). El minimapa se dibuja con las rutas (waypoints) de los animatrónicos; no hace falta una imagen del mapa.

## Reglas (propuesta de balance, a ajustar con el profesor)

`Unity/Assets/scripts/FrenoDeOperador.cs`, probada fuera del editor con `pruebas/probar_freno_de_operador.sh`:

- **Retrasar** baja a la **mitad** la velocidad del animatrónico durante **8 s**.
- **Espera** de **20 s** (desde el uso) antes de volver a retrasar al mismo animatrónico. Cada animatrónico tiene su propia espera.
- Retrasar afecta: la espera entre avances (Freddy/Vixy), la amenaza por tareas pendientes (Vixy), la salida de Puppet de la caja y la cuenta del ataque desde el penúltimo punto.
- Retrasar **no** salta la mirada de Freddy ni las tareas de Vixy: solo gana tiempo.
- Sin partida en curso, la acción se rechaza (`sin_partida`).
- Cada noche nueva reinicia retrasos y esperas.

## Lo que ve el jugador 1

Un aviso grande arriba en la pantalla de Unity, por 3 s: "Vixy se ralentizó". La tablet recibe `efecto` pero **no lo muestra todavía** (Flutter ignora los tipos que no conoce).

## Qué hay que acordar con Marcelo

1. Nombres de los mensajes y del rol (`operador_connect`).
2. Que `estado_mapa` / `mapa_puntos` vayan solo al operador (hoy filtrado en el relay).
3. Si la tablet debe mostrar también el aviso `efecto`.
4. Qué hacer si el operador se cae a media noche (hoy: reconecta solo y Unity le reenvía el mapa y el estado).

## Cómo probarlo

```bash
# Pruebas automáticas
UnityRelay/.venv/bin/python pruebas/probar_relay.py      # relay, incluye el rol operador
./pruebas/probar_freno_de_operador.sh                    # reglas del jugador 2

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
