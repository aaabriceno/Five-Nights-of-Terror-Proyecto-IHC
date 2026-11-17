"""Prueba con el juego REAL exportado: el tutorial de 7 diapositivas y su práctica.

Hace de tablet: abre el tutorial, va a la práctica de los controles y manda las
mismas órdenes que en una partida (cámara, linterna, compuerta, cuerda); Unity
marca cada paso y lo publica en `tutorial_prueba_estado`. Después pasa a la
práctica de la tarea y manda los cables resueltos. La mirada no se puede probar
sin una cara delante de la cámara.

El juego se abre en una pantalla virtual (Xvfb) y se guardan capturas de las dos
diapositivas de práctica en la carpeta que se indique (por defecto, ninguna).
Hay que exportar antes el juego (./exportar_linux.sh) y no tener otro relay en
el puerto 8000.

Uso: UnityRelay/.venv/bin/python pruebas/probar_tutorial_con_juego.py [carpeta_de_capturas]
"""
import asyncio
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
URL = "ws://127.0.0.1:8000"
JUEGO = RAIZ / "Unity/Builds/Linux/FiveNightsOfTerror.x86_64"
PANTALLA = ":97"
CAPTURAS = Path(sys.argv[1]) if len(sys.argv) > 1 else None
fallos = 0


def comprobar(descripcion, condicion):
    global fallos
    print(("  ok    " if condicion else "  FALLA ") + descripcion, flush=True)
    if not condicion:
        fallos += 1


async def esperar_tipo(socket, tipo, espera=3.0):
    """El primer mensaje de ese tipo que llegue en `espera` segundos, o None."""
    limite = asyncio.get_running_loop().time() + espera
    while (queda := limite - asyncio.get_running_loop().time()) > 0:
        try:
            m = json.loads(await asyncio.wait_for(socket.recv(), queda))
        except asyncio.TimeoutError:
            return None
        if m.get("type") == tipo:
            return m
    return None


def capturar(nombre):
    if CAPTURAS is None or not shutil.which("import"):
        return
    CAPTURAS.mkdir(parents=True, exist_ok=True)
    subprocess.run(["import", "-display", PANTALLA, "-window", "root", str(CAPTURAS / nombre)],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)


async def principal():
    procesos = []
    entorno = dict(os.environ)
    try:
        if shutil.which("Xvfb"):
            procesos.append(subprocess.Popen(["Xvfb", PANTALLA, "-screen", "0", "1920x1080x24"],
                                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
            entorno["DISPLAY"] = PANTALLA
            await asyncio.sleep(1.5)
        procesos.append(subprocess.Popen([sys.executable, "-u", str(RAIZ / "UnityRelay/relay.py")],
                                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
        await asyncio.sleep(1.0)
        procesos.append(subprocess.Popen([str(JUEGO), "-screen-fullscreen", "0", "-screen-width", "1920",
                                          "-screen-height", "1080"],
                                         env=entorno, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
        await asyncio.sleep(12.0)       # el juego tarda en abrir y conectarse al relay

        async with websockets.connect(URL) as tablet:
            await tablet.send(json.dumps({"type": "connect", "device": "tablet", "player_id": "prueba_tut", "modo": "tutorial"}))
            await tablet.send(json.dumps({"type": "tutorial_abrir"}))
            estado = await esperar_tipo(tablet, "tutorial_estado", 5)
            comprobar(f"el tutorial tiene 7 diapositivas y empieza en la primera (llegó {estado})",
                      estado is not None and estado.get("total") == 7 and estado.get("indice") == 0)

            # --- práctica 1: los controles (diapositiva 6, índice 5) ---
            await tablet.send(json.dumps({"type": "tutorial_slide", "indice": 5}))
            prueba = await esperar_tipo(tablet, "tutorial_prueba_estado")
            comprobar("al llegar a la práctica de controles no hay nada probado",
                      prueba is not None and not any(prueba.get(k) for k in ("camara", "linterna", "compuerta", "cuerda")))
            await tablet.send(json.dumps({"type": "tutorial_prueba_tarea", "exito": True}))
            await asyncio.sleep(0.3)
            capturar("practica_controles_vacia.png")

            pasos = [
                ("camara", {"type": "camara", "direccion": -1}, "girar la cámara (mantener ◀)"),
                ("linterna", {"type": "linterna", "encendida": True}, "prender la linterna"),
                ("compuerta", {"type": "compuerta", "lado": "izquierda", "cerrar": True}, "cerrar una compuerta"),
                ("cuerda", {"type": "dar_cuerda_inicio"}, "dar cuerda a la caja"),
            ]
            for clave, orden, texto in pasos:
                await tablet.send(json.dumps(orden))
                prueba = await esperar_tipo(tablet, "tutorial_prueba_estado")
                comprobar(f"{texto}: Unity lo marca enseguida ({clave} = {prueba.get(clave) if prueba else None})",
                          prueba is not None and prueba.get(clave) is True)
            await tablet.send(json.dumps({"type": "dar_cuerda_fin"}))
            comprobar("los cables enviados en la práctica de controles no cuentan (son de la otra práctica)",
                      prueba is not None and prueba.get("tarea_completada") is False)
            await asyncio.sleep(0.5)
            capturar("practica_controles_completa.png")

            # --- práctica 2: la tarea y la mirada (diapositiva 7, índice 6) ---
            await tablet.send(json.dumps({"type": "tutorial_slide", "indice": 6}))
            prueba = await esperar_tipo(tablet, "tutorial_prueba_estado")
            comprobar("en la práctica de la tarea siguen marcados los cuatro controles",
                      prueba is not None and all(prueba.get(k) for k in ("camara", "linterna", "compuerta", "cuerda")))
            await tablet.send(json.dumps({"type": "tutorial_prueba_tarea", "exito": True}))
            prueba = await esperar_tipo(tablet, "tutorial_prueba_estado")
            comprobar("los cables resueltos se marcan (tarea_completada)", prueba is not None and prueba.get("tarea_completada") is True)
            await asyncio.sleep(0.5)
            capturar("practica_tarea.png")

            noche = await esperar_tipo(tablet, "night_status", 1.0)
            comprobar("nada de esto empezó una partida (no llega night_status)", noche is None)

            # --- volver a abrir el tutorial empieza la práctica de cero ---
            await tablet.send(json.dumps({"type": "tutorial_cerrar"}))
            await tablet.send(json.dumps({"type": "tutorial_abrir"}))
            await tablet.send(json.dumps({"type": "tutorial_slide", "indice": 5}))
            prueba = await esperar_tipo(tablet, "tutorial_prueba_estado")
            comprobar("al abrir el tutorial otra vez, la práctica empieza sin nada probado",
                      prueba is not None and not any(prueba.get(k) for k in ("camara", "linterna", "compuerta", "cuerda", "tarea_completada")))
            await tablet.send(json.dumps({"type": "tutorial_cerrar"}))
            await tablet.send(json.dumps({"type": "disconnect"}))
    finally:
        for proceso in reversed(procesos):
            proceso.terminate()
            try:
                proceso.wait(timeout=10)
            except subprocess.TimeoutExpired:
                proceso.kill()
    print()
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


sys.exit(asyncio.run(principal()))
