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

## Pendiente

- Repetir desde la computadora del jugador 2 (otra máquina, misma red): `--host <IP de la laptop> --iniciar-partida` con el juego abierto.
- Anotar aquí la fecha, la red y los equipos.
