"""Mide la latencia del jugador 2 contra el juego real.

Hace de operador: manda `n` pings (`ping_operador`) y mide cuánto tarda cada
`pong_operador` (ida y vuelta: operador → relay → Unity → relay → operador).
Si hay partida en curso, mide también cada cuánto llegan las `posiciones`
(deben ser unas 20 por segundo, cada ~50 ms).

Meta: percentil 95 del ping menor a 50 ms en red local. Sale con código 1 si
no se cumple, 2 si no llegó ningún pong.

Con el juego ya abierto y el relay corriendo:
  UnityRelay/.venv/bin/python pruebas/medir_latencia_operador.py --host 192.168.1.7
Todo en esta máquina (levanta relay y juego, e inicia una noche como tablet):
  UnityRelay/.venv/bin/python pruebas/medir_latencia_operador.py --lanzar
"""
import argparse
import asyncio
import json
import statistics
import subprocess
import sys
import time
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
META_P95_MS = 50.0


def percentil(valores, p):
    ordenados = sorted(valores)
    posicion = max(0, min(len(ordenados) - 1, -(-len(ordenados) * p // 100) - 1))
    return ordenados[int(posicion)]


def resumen(nombre, valores_ms):
    if not valores_ms:
        print(f"{nombre:<28} sin datos")
        return
    print(f"{nombre:<28} n={len(valores_ms):<4} media={statistics.mean(valores_ms):7.2f}  "
          f"p50={percentil(valores_ms, 50):7.2f}  p95={percentil(valores_ms, 95):7.2f}  max={max(valores_ms):7.2f}  (ms)")


async def medir(direccion, cantidad, iniciar_partida):
    rtt_ms = []
    llegadas_posiciones = []
    enviados = {}

    async with websockets.connect(direccion) as operador:
        tablet = None
        if iniciar_partida:
            tablet = await websockets.connect(direccion)
            await tablet.send(json.dumps({"type": "connect", "device": "tablet", "player_id": "medicion", "modo": "nuevo"}))
        await operador.send(json.dumps({"type": "operador_connect"}))

        async def leer():
            async for crudo in operador:
                llegada = time.perf_counter()
                try:
                    mensaje = json.loads(crudo)
                except json.JSONDecodeError:
                    continue
                if mensaje.get("type") == "pong_operador":
                    inicio = enviados.pop(mensaje.get("t"), None)
                    if inicio is not None:
                        rtt_ms.append((llegada - inicio) * 1000)
                elif mensaje.get("type") == "posiciones":
                    llegadas_posiciones.append(llegada)

        lector = asyncio.ensure_future(leer())
        await asyncio.sleep(1.0)
        for i in range(cantidad):
            identificador = int(time.perf_counter() * 1e6)
            enviados[identificador] = time.perf_counter()
            await operador.send(json.dumps({"type": "ping_operador", "t": identificador}))
            await asyncio.sleep(0.05)
        await asyncio.sleep(1.0)
        lector.cancel()
        if tablet:
            await tablet.send(json.dumps({"type": "disconnect"}))
            await tablet.close()

    intervalos = [(b - a) * 1000 for a, b in zip(llegadas_posiciones, llegadas_posiciones[1:])]
    return rtt_ms, intervalos


async def principal(argumentos):
    relay = juego = None
    try:
        if argumentos.lanzar:
            relay = subprocess.Popen([sys.executable, "-u", str(RAIZ / "UnityRelay/relay.py")],
                                     stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            await asyncio.sleep(1.0)
            juego = subprocess.Popen([str(argumentos.juego)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            print("Esperando 12 s a que el juego abra y se conecte...")
            await asyncio.sleep(12.0)
        direccion = f"ws://{argumentos.host}:{argumentos.puerto}"
        print(f"Midiendo contra {direccion} ({argumentos.n} pings)...")
        rtt, intervalos = await medir(direccion, argumentos.n, argumentos.iniciar_partida or argumentos.lanzar)
    except OSError as error:
        print(f"No se pudo conectar: {error}")
        return 2
    finally:
        if juego:
            juego.terminate()
            juego.wait()
        if relay:
            relay.terminate()
            relay.wait()

    print()
    resumen("ping (ida y vuelta)", rtt)
    resumen("intervalo entre posiciones", intervalos)
    if not rtt:
        print("\nNo llegó ningún pong: ¿está el juego abierto y conectado al relay?")
        return 2
    p95 = percentil(rtt, 95)
    if not intervalos:
        print("No llegaron posiciones: hace falta una partida en curso (usa --iniciar-partida).")
    cumple = p95 < META_P95_MS
    print(f"\nMeta: p95 del ping < {META_P95_MS:.0f} ms → {'CUMPLE' if cumple else 'NO CUMPLE'} (p95 = {p95:.1f} ms)")
    return 0 if cumple else 1


if __name__ == "__main__":
    analizador = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    analizador.add_argument("--host", default="127.0.0.1")
    analizador.add_argument("--puerto", type=int, default=8000)
    analizador.add_argument("--n", type=int, default=200)
    analizador.add_argument("--iniciar-partida", action="store_true", help="hace de tablet e inicia una noche")
    analizador.add_argument("--lanzar", action="store_true", help="levanta relay y juego en esta máquina")
    analizador.add_argument("--juego", type=Path, default=RAIZ / "Unity/Builds/Linux/FiveNightsOfTerror.x86_64")
    raise SystemExit(asyncio.run(principal(analizador.parse_args())))
