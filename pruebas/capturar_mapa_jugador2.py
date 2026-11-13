"""Foto del mapa del jugador 2 con el juego de verdad: abre el relay y el juego
exportado en una pantalla virtual (Xvfb), arranca la noche 1 como si fuera la
tablet, abre la página del jugador 2 en Firefox (sin ventana) y guarda una
captura. Comprueba también que Unity mandó el plano del edificio.

Uso: UnityRelay/.venv/bin/python pruebas/capturar_mapa_jugador2.py <archivo.png>
"""
import asyncio
import json
import os
import subprocess
import sys
import time
from pathlib import Path

import websockets

sys.path.insert(0, str(Path(__file__).resolve().parent))
import probar_pagina_operador as pagina  # noqa: E402  (Navegador y el servidor web de la página)

RAIZ = Path(__file__).resolve().parent.parent
JUEGO = RAIZ / "Unity/Builds/Linux/FiveNightsOfTerror.x86_64"
URL = "ws://127.0.0.1:8000"
PANTALLA = ":96"


async def principal(destino):
    pantalla = subprocess.Popen(["Xvfb", PANTALLA, "-screen", "0", "1280x720x24"],
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    relay = subprocess.Popen([sys.executable, "-u", str(RAIZ / "UnityRelay/relay.py")],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    juego = navegador = None
    try:
        await asyncio.sleep(1.5)
        juego = subprocess.Popen([str(JUEGO), "-screen-fullscreen", "0", "-screen-width", "640", "-screen-height", "360",
                                  "-mediapipe-cpu", "-sin-ambiente"],
                                 stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env={**os.environ, "DISPLAY": PANTALLA})
        async with websockets.connect(URL) as tablet, websockets.connect(URL) as espia:
            await tablet.send(json.dumps({"type": "connect", "device": "tablet", "modo": "nuevo", "player_id": "captura"}))
            await espia.send(json.dumps({"type": "operador_connect"}))
            plano = None
            limite = time.monotonic() + 40
            while plano is None and time.monotonic() < limite:
                try:
                    m = json.loads(await asyncio.wait_for(espia.recv(), 2))
                except asyncio.TimeoutError:
                    await espia.send(json.dumps({"type": "operador_connect"}))
                    continue
                if m.get("type") == "mapa_puntos":
                    plano = m.get("plano") or []
            tipos = {}
            for p in plano or []:
                tipos[p["tipo"]] = tipos.get(p["tipo"], 0) + 1
            print("piezas del plano por tipo:", tipos)
            print("rótulos:", sorted({p["etiqueta"] for p in plano or [] if p["etiqueta"]}))
            ok = bool(plano) and tipos.get("muro", 0) >= 20 and tipos.get("mesa", 0) >= 4
            print("  ok    Unity manda el plano con muros y mesas" if ok else "  FALLA Unity no mandó el plano esperado")
            await espia.send(json.dumps({"type": "operador_salir"}))

            web = pagina.levantar_web()
            navegador = await asyncio.get_running_loop().run_in_executor(None, pagina.Navegador)
            navegador.ejecutar("window.resizeTo(1400, 900);")
            navegador.abrir(f"http://127.0.0.1:{pagina.PUERTO_WEB}/operador.html?host=127.0.0.1&puerto=8000&auto=1")
            listo = navegador.esperar(lambda: navegador.ejecutar(
                "var m=document.getElementById('mapa');return m && m.dataset.estado==='listo' && +m.dataset.plano>0;"), 20)
            print("  ok    la página recibió y dibuja el plano" if listo else "  FALLA la página no dibujó el plano")
            ruta = navegador.texto("ruta-freddy")
            print("ruta de Freddy en el panel:", ruta)
            time.sleep(2)
            navegador.captura(destino)
            print("captura:", destino)
            web.shutdown()
            return 0 if ok and listo else 1
    finally:
        if navegador is not None:
            navegador.cerrar()
        if juego is not None:
            juego.terminate()
            juego.wait(10)
        relay.terminate()
        relay.wait()
        pantalla.terminate()


if __name__ == "__main__":
    sys.exit(asyncio.run(principal(sys.argv[1] if len(sys.argv) > 1 else "mapa_jugador2.png")))
