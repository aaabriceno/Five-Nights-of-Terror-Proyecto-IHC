"""Latencia del relay con un cliente que usa el algoritmo de Nagle (como el de Unity).

El cliente de Unity (ClientWebSocket de Mono) abre su socket con TCP_NODELAY = 0
(se comprobó con strace). Con Nagle, un mensaje pequeño que sale mientras otro
sigue sin confirmar (ACK) se queda esperando, y el receptor demora el ACK hasta
40 ms: de ahí salía el p95 de ~41 ms del ping del jugador 2 (con el juego real,
bajó a ~14 ms). La defensa está en el relay (UnityRelay/relay.py): pide a Linux
que confirme (ACK) de inmediato. En esta prueba el problema aparece solo en
algunos pings (al azar), por eso son 500 y se pide que ninguno pase de 25 ms.
Con RELAY=<ruta> se prueba otro relay (p. ej. uno viejo).

Esta prueba lo reproduce sin Unity: el operador manda pings y un "Unity" con
Nagle contesta a cada uno con unas posiciones y el pong, uno tras otro (como en
un cuadro del juego); se mide la ida y vuelta del ping a través del relay. Solo
tiene sentido en Linux.

Uso: UnityRelay/.venv/bin/python pruebas/probar_latencia_del_relay.py
"""
import asyncio
import json
import os
import socket
import statistics
import subprocess
import sys
import time
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
URL = "ws://127.0.0.1:8000"
INTENTOS = 500
META_MEDIANA_MS = 10.0
META_MAXIMO_MS = 25.0
fallos = 0


def comprobar(descripcion, condicion):
    global fallos
    print(("  ok    " if condicion else "  FALLA ") + descripcion)
    if not condicion:
        fallos += 1


async def esperar_relay():
    for _ in range(40):
        try:
            async with websockets.connect(URL):
                return True
        except OSError:
            await asyncio.sleep(0.25)
    return False


def percentil(valores, p):
    ordenados = sorted(valores)
    return ordenados[min(len(ordenados) - 1, int(len(ordenados) * p / 100))]


async def unity_con_nagle(unity):
    """Como Unity en cada cuadro: por cada ping del operador manda unas posiciones y el pong, uno tras otro."""
    async for crudo in unity:
        mensaje = json.loads(crudo)
        if mensaje.get("type") == "ping_operador":
            # Un cuadro de Unity: varios mensajes pequeños seguidos y, entre ellos, el pong.
            await unity.send(json.dumps({"type": "posiciones", "relleno": "x" * 200}))
            await unity.send(json.dumps({"type": "pong_operador", "t": mensaje["t"]}))
            await unity.send(json.dumps({"type": "estado_jugador", "relleno": "x" * 80}))
            await unity.send(json.dumps({"type": "estado_mapa", "relleno": "x" * 280}))


async def medir():
    unity = await websockets.connect(URL, ping_interval=None)
    operador = await websockets.connect(URL, ping_interval=None)
    # Como el cliente de Unity: sin TCP_NODELAY, o sea, con el algoritmo de Nagle.
    unity.transport.get_extra_info("socket").setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 0)
    await unity.send(json.dumps({"type": "unity_connect"}))
    tarea = asyncio.ensure_future(unity_con_nagle(unity))
    await operador.send(json.dumps({"type": "operador_connect"}))
    esperas_ms = []
    for i in range(INTENTOS):
        salida = time.perf_counter()
        await operador.send(json.dumps({"type": "ping_operador", "t": salida}))
        while True:
            mensaje = json.loads(await asyncio.wait_for(operador.recv(), 2.0))
            if mensaje.get("type") == "pong_operador":
                esperas_ms.append((time.perf_counter() - salida) * 1000)
                break
        await asyncio.sleep(1 / 30)                    # unos 30 pings por segundo, como el ritmo de las posiciones del juego
    tarea.cancel()
    await unity.close()
    await operador.close()
    return esperas_ms


async def principal():
    if sys.platform != "linux":
        print("Esta prueba solo tiene sentido en Linux (TCP_QUICKACK).")
        return 0
    relay = subprocess.Popen([sys.executable, "-u", os.environ.get("RELAY", str(RAIZ / "UnityRelay/relay.py"))],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        if not await esperar_relay():
            print("El relay no arrancó")
            return 1
        esperas = await medir()
        mediana, p95 = statistics.median(esperas), percentil(esperas, 95)
        print(f"  ping con un Unity con Nagle ({INTENTOS} intentos): mediana {mediana:.1f} ms, p95 {p95:.1f} ms, máx {max(esperas):.1f} ms")
        lentos = [e for e in esperas if e >= META_MAXIMO_MS]
        comprobar(f"con un Unity con Nagle, la mediana del ping es < {META_MEDIANA_MS:.0f} ms", mediana < META_MEDIANA_MS)
        comprobar(f"ningún ping tarda {META_MAXIMO_MS:.0f} ms o más (el ACK demorado de Linux añade ~40 ms); tardaron así: {len(lentos)}",
                  not lentos)
    finally:
        relay.terminate()
        relay.wait()
    print()
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


sys.exit(asyncio.run(principal()))
