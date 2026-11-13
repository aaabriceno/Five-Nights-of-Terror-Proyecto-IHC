"""Fotografía el ambiente del juego (niebla, ojos, luces, posprocesado, estática)
en una pantalla virtual (Xvfb), sin tapar el escritorio.

Abre el relay y el juego exportado con distintas opciones, se conecta como la
tablet para que empiece la noche 1 y guarda una captura por escena:

  oficina_sin_ambiente.png  como se veía antes (-sin-ambiente)
  oficina.png               la oficina con el ambiente, nivel alto
  freddy.png / vixy.png / puppet.png   cada animatrónico plantado a 3,5 m (-prueba-posar)
  estatica.png              con la estática encendida (-prueba-estatica) y el aviso del jugador 2
  basico.png                nivel básico (sin bloom ni sombras)

Uso: UnityRelay/.venv/bin/python pruebas/capturar_ambiente.py <carpeta> [escena ...]
El render es por software (llvmpipe): sirve para ver la composición, no los FPS.
"""
import asyncio
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
JUEGO = RAIZ / "Unity/Builds/Linux/FiveNightsOfTerror.x86_64"
URL = "ws://127.0.0.1:8000"
PANTALLA = ":98"
PLAYER_LOG = Path.home() / ".config/unity3d/DefaultCompany/Poryecto_IHC/Player.log"


async def esperar_relay():
    for _ in range(40):
        try:
            async with websockets.connect(URL):
                return True
        except OSError:
            await asyncio.sleep(0.25)
    return False


async def escena(nombre, carpeta, opciones, accion_j2=False, espera=11.0):
    print(f"== {nombre}: {' '.join(opciones) or '(sin opciones)'}")
    relay = subprocess.Popen([sys.executable, "-u", str(RAIZ / "UnityRelay/relay.py")],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    juego = None
    try:
        if not await esperar_relay():
            print("  el relay no arrancó")
            return False
        juego = subprocess.Popen(
            [str(JUEGO), "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720",
             "-mediapipe-cpu", *opciones],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env={**os.environ, "DISPLAY": PANTALLA})
        async with websockets.connect(URL) as tablet:
            # El relay guarda este connect y se lo pasa a Unity cuando se conecte.
            await tablet.send(json.dumps({"type": "connect", "device": "tablet", "modo": "nuevo",
                                          "player_id": "captura"}))
            inicio = time.monotonic()
            while time.monotonic() - inicio < espera:
                await asyncio.sleep(0.5)
            if accion_j2:
                async with websockets.connect(URL) as operador:
                    await operador.send(json.dumps({"type": "operador_connect"}))
                    await asyncio.sleep(0.5)
                    await operador.send(json.dumps({"type": "accion_operador", "accion": "retrasar",
                                                    "objetivo": "freddy", "id": 1}))
                    await asyncio.sleep(0.15)
                    subprocess.run(["import", "-display", PANTALLA, "-window", "root", str(carpeta / f"{nombre}.png")], check=False)
            else:
                subprocess.run(["import", "-display", PANTALLA, "-window", "root", str(carpeta / f"{nombre}.png")], check=False)
            await tablet.send(json.dumps({"type": "disconnect"}))
    finally:
        if juego is not None:
            juego.terminate()
            try:
                juego.wait(10)
            except subprocess.TimeoutExpired:
                juego.kill()
        relay.terminate()
        relay.wait()
    if PLAYER_LOG.exists():
        for linea in PLAYER_LOG.read_text(errors="replace").splitlines():
            if "[Ambiente]" in linea or "[Video] pantalla" in linea:
                print("  " + linea.strip())
    print(f"  -> {carpeta / (nombre + '.png')}")
    return True


ESCENAS = {
    "oficina_sin_ambiente": (["-sin-ambiente"], False),
    "oficina": (["-graficos-alto"], False),
    "freddy": (["-graficos-alto", "-prueba-posar", "Freddy"], False),
    "vixy": (["-graficos-alto", "-prueba-posar", "Vixy"], False),
    "puppet": (["-graficos-alto", "-prueba-posar", "Puppet"], False),
    "estatica": (["-graficos-alto", "-prueba-posar", "Freddy", "-prueba-estatica"], True),
    "basico": (["-graficos-basico", "-prueba-posar", "Freddy"], False),
}


async def principal(carpeta, nombres):
    carpeta.mkdir(parents=True, exist_ok=True)
    if not JUEGO.exists():
        print(f"No está el juego exportado en {JUEGO}")
        return 2
    if not shutil.which("Xvfb") or not shutil.which("import"):
        print("Hacen falta Xvfb e import (ImageMagick).")
        return 2
    pantalla = subprocess.Popen(["Xvfb", PANTALLA, "-screen", "0", "1280x720x24"],
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        await asyncio.sleep(1.0)
        for nombre in nombres:
            opciones, accion_j2 = ESCENAS[nombre]
            await escena(nombre, carpeta, opciones, accion_j2=accion_j2)
    finally:
        pantalla.terminate()
        pantalla.wait()
    return 0


if __name__ == "__main__":
    destino = Path(sys.argv[1]) if len(sys.argv) > 1 else RAIZ / "pruebas/capturas_ambiente"
    elegidas = sys.argv[2:] or list(ESCENAS)
    desconocidas = [n for n in elegidas if n not in ESCENAS]
    if desconocidas:
        print("Escenas desconocidas:", ", ".join(desconocidas), "· válidas:", ", ".join(ESCENAS))
        sys.exit(2)
    sys.exit(asyncio.run(principal(destino, elegidas)))
