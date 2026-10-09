"""Pruebas del relay: levanta UnityRelay/relay.py en el puerto 8000 y le habla
con clientes WebSocket que hacen de tablet y de Unity.

Uso: UnityRelay/.venv/bin/python pruebas/probar_relay.py
"""
import asyncio
import json
import subprocess
import sys
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
URL = "ws://127.0.0.1:8000"
CONNECT_TUTORIAL = json.dumps({"type": "connect", "device": "tablet", "modo": "tutorial"})
fallos = 0


def comprobar(descripcion, condicion):
    global fallos
    print(("  ok    " if condicion else "  FALLA ") + descripcion)
    if not condicion:
        fallos += 1


async def recibir_o_nada(socket, espera=1.0):
    try:
        return await asyncio.wait_for(socket.recv(), espera)
    except asyncio.TimeoutError:
        return None


async def esperar_relay():
    for _ in range(40):
        try:
            async with websockets.connect(URL):
                return True
        except OSError:
            await asyncio.sleep(0.25)
    return False


async def caso_tablet_ya_se_fue():
    print("Caso 1: la tablet se desconecta y después se abre Unity")
    async with websockets.connect(URL) as tablet:
        await tablet.send(CONNECT_TUTORIAL)
        await asyncio.sleep(0.3)
    await asyncio.sleep(0.3)
    async with websockets.connect(URL) as unity:
        await unity.send(json.dumps({"type": "unity_connect"}))
        mensaje = await recibir_o_nada(unity)
        comprobar("Unity no recibe el connect de una tablet que ya no está", mensaje is None)


async def caso_tablet_se_despide():
    print("Caso 2: la tablet manda disconnect y después se abre Unity")
    async with websockets.connect(URL) as tablet:
        await tablet.send(CONNECT_TUTORIAL)
        await tablet.send(json.dumps({"type": "disconnect"}))
        await asyncio.sleep(0.3)
        async with websockets.connect(URL) as unity:
            await unity.send(json.dumps({"type": "unity_connect"}))
            mensaje = await recibir_o_nada(unity)
            comprobar("Unity no recibe el connect tras el disconnect", mensaje is None)


async def caso_tablet_sigue_conectada():
    print("Caso 3: la tablet sigue conectada cuando se abre Unity")
    async with websockets.connect(URL) as tablet:
        await tablet.send(CONNECT_TUTORIAL)
        await asyncio.sleep(0.3)
        async with websockets.connect(URL) as unity:
            await unity.send(json.dumps({"type": "unity_connect"}))
            mensaje = await recibir_o_nada(unity)
            comprobar("Unity recibe el connect de la tablet presente", mensaje == CONNECT_TUTORIAL)


async def principal():
    relay = subprocess.Popen(
        [sys.executable, "-u", str(RAIZ / "UnityRelay" / "relay.py")],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        if not await esperar_relay():
            print("El relay no arrancó (¿puerto 8000 ocupado?).")
            return 2
        await caso_tablet_ya_se_fue()
        await caso_tablet_se_despide()
        await caso_tablet_sigue_conectada()
    finally:
        relay.terminate()
        relay.wait()
    print()
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


sys.exit(asyncio.run(principal()))
