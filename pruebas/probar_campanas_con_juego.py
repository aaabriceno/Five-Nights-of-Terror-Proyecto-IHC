"""¿Suenan las campanas de las 6 AM al ganar una noche? Con el juego de verdad.

Abre el relay y el juego exportado en una pantalla virtual (Xvfb), con la noche
acortada (-prueba-segundos-noche) y sin Freddy; hace de tablet (mantiene la
cuerda de la caja de Puppet) hasta que llega game_over con result "win", y graba
lo que sale por los parlantes de la PC (monitor de PulseAudio/PipeWire) para
medir el volumen antes y después de ganar. El registro del juego dice qué
efectos sonaron ([Sonido]).

Uso: UnityRelay/.venv/bin/python pruebas/probar_campanas_con_juego.py [carpeta]
Ojo: suena por tus parlantes (es lo que se graba).
"""
import array
import asyncio
import json
import math
import os
import subprocess
import sys
import time
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
JUEGO = RAIZ / "Unity/Builds/Linux/FiveNightsOfTerror.x86_64"
URL = "ws://127.0.0.1:8000"
PANTALLA = ":95"
PLAYER_LOG = Path.home() / ".config/unity3d/DefaultCompany/Poryecto_IHC/Player.log"
MUESTREO = 16000
fallos = 0


def comprobar(descripcion, condicion):
    global fallos
    print(("  ok    " if condicion else "  FALLA ") + descripcion)
    if not condicion:
        fallos += 1


def monitor_por_defecto():
    sumidero = subprocess.run(["pactl", "get-default-sink"], capture_output=True, text=True).stdout.strip()
    return sumidero + ".monitor" if sumidero else None


def volumen_por_segundo(crudo):
    """RMS (0-1) de cada segundo de audio mono de 16 bits."""
    muestras = array.array("h", crudo[: len(crudo) // 2 * 2])
    salida = []
    for i in range(0, len(muestras) - MUESTREO + 1, MUESTREO):
        bloque = muestras[i:i + MUESTREO]
        salida.append(math.sqrt(sum(m * m for m in bloque) / len(bloque)) / 32768)
    return salida


async def principal(carpeta):
    carpeta.mkdir(parents=True, exist_ok=True)
    monitor = monitor_por_defecto()
    grabacion = carpeta / "audio.raw"
    pantalla = subprocess.Popen(["Xvfb", PANTALLA, "-screen", "0", "1280x720x24"],
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    relay = subprocess.Popen([sys.executable, "-u", str(RAIZ / "UnityRelay/relay.py")],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    juego = grabador = None
    try:
        await asyncio.sleep(1.5)
        juego = subprocess.Popen(
            [str(JUEGO), "-screen-fullscreen", "0", "-screen-width", "640", "-screen-height", "360",
             "-mediapipe-cpu", "-prueba-sin-freddy", "-prueba-segundos-noche", "25"],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env={**os.environ, "DISPLAY": PANTALLA})
        if monitor:
            grabador = subprocess.Popen(["parec", "-d", monitor, "--format=s16le", "--rate", str(MUESTREO),
                                         "--channels", "1", str(grabacion)] if False else
                                        ["parec", "-d", monitor, "--format=s16le", f"--rate={MUESTREO}", "--channels=1"],
                                        stdout=open(grabacion, "wb"), stderr=subprocess.DEVNULL)
        inicio = time.monotonic()
        resultado = None
        momento_fin = None
        async with websockets.connect(URL) as tablet:
            await tablet.send(json.dumps({"type": "connect", "device": "tablet", "modo": "nuevo", "player_id": "campanas"}))
            limite = time.monotonic() + 90
            ultimo_cuerda = 0.0
            while time.monotonic() < limite and resultado is None:
                if time.monotonic() - ultimo_cuerda > 1.0:      # la caja de Puppet siempre llena
                    await tablet.send(json.dumps({"type": "dar_cuerda_inicio"}))
                    ultimo_cuerda = time.monotonic()
                try:
                    m = json.loads(await asyncio.wait_for(tablet.recv(), 0.5))
                except asyncio.TimeoutError:
                    continue
                if m.get("type") == "game_over":
                    resultado = m.get("result")
                    momento_fin = time.monotonic() - inicio
            await tablet.send(json.dumps({"type": "dar_cuerda_fin"}))
            comprobar(f"la noche termina ganada (result {resultado!r}) a los {momento_fin or 0:.1f} s", resultado == "win")
            await asyncio.sleep(19)          # las campanas duran 16 s; después debe sonar el menú
    finally:
        if grabador is not None:
            grabador.terminate()
            grabador.wait()
        if juego is not None:
            juego.terminate()
            try:
                juego.wait(10)
            except subprocess.TimeoutExpired:
                juego.kill()
        relay.terminate()
        relay.wait()
        pantalla.terminate()

    registro = PLAYER_LOG.read_text(errors="replace").splitlines() if PLAYER_LOG.exists() else []
    sonidos = [l.strip() for l in registro if "[Sonido]" in l]
    for linea in sonidos:
        print("  " + linea)
    comprobar("el juego reprodujo las campanas al ganar",
              any("Campanas" in l and "no suena" not in l for l in sonidos))
    # La música del menú suena ~13 veces más fuerte que las campanas: si arranca a la
    # vez, las tapa. Tiene que esperar a que terminen.
    comprobar("la música del menú espera a que terminen las campanas (si no, las tapa)",
              any("espera" in l and "campanas" in l.lower() for l in sonidos))
    indice_campanas = next((i for i, l in enumerate(sonidos) if "Campanas" in l and "no suena" not in l), None)
    comprobar("y no arranca junto con ellas",
              indice_campanas is not None and not any("Música del menú: suena" in l for l in sonidos[indice_campanas:indice_campanas + 2]))
    comprobar("y cuando terminan, la música del menú sí suena",
              indice_campanas is not None and any("Música del menú: suena" in l for l in sonidos[indice_campanas:]))

    if monitor and grabacion.exists() and momento_fin:
        volumen = volumen_por_segundo(grabacion.read_bytes())
        antes = volumen[max(0, int(momento_fin) - 6):int(momento_fin) - 1]
        despues = volumen[int(momento_fin) + 1:int(momento_fin) + 7]
        print("  volumen por segundo (antes de ganar):", " ".join(f"{v:.3f}" for v in antes))
        print("  volumen por segundo (después):       ", " ".join(f"{v:.3f}" for v in despues))
        comprobar("por los parlantes sale sonido después de ganar (las campanas)",
                  bool(despues) and max(despues) > 0.01)
    else:
        print("  (no se pudo grabar el audio de la PC)")
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


if __name__ == "__main__":
    sys.exit(asyncio.run(principal(Path(sys.argv[1]) if len(sys.argv) > 1 else RAIZ / "pruebas/campanas")))
