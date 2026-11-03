"""Prueba de la página del jugador 2 (UnityRelay/operador) sin el juego.

Levanta un servidor web con la página, un "Unity falso" que le manda los
mensajes del protocolo, y abre la página en Firefox sin ventana (geckodriver).
Comprueba lo que se ve: conexión, datos del jugador 1, latencia, mapa y que
no falle si llegan mensajes en desorden.

Uso: UnityRelay/.venv/bin/python pruebas/probar_pagina_operador.py [--captura ruta.png]
Necesita firefox y geckodriver (snap). No usa el puerto 8000.
"""
import argparse
import asyncio
import base64
import http.server
import json
import socketserver
import subprocess
import tempfile
import threading
import time
import urllib.request
from functools import partial
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
CARPETA_PAGINA = RAIZ / "UnityRelay" / "operador"
PUERTO_WEB = 8095
PUERTO_UNITY = 8015

def puerto_libre():
    import socket
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


PUERTO_GECKO = puerto_libre()   # el geckodriver del snap no siempre se deja matar: no reutilizar puertos
GECKODRIVER = "/snap/bin/geckodriver"
fallos = 0


def comprobar(descripcion, condicion):
    global fallos
    print(("  ok    " if condicion else "  FALLA ") + descripcion)
    if not condicion:
        fallos += 1


# ---------------------------------------------------------------- servidor web
class SinRegistro(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *args):
        pass


def levantar_web():
    socketserver.TCPServer.allow_reuse_address = True
    servidor = socketserver.TCPServer(("127.0.0.1", PUERTO_WEB), partial(SinRegistro, directory=str(CARPETA_PAGINA)))
    threading.Thread(target=servidor.serve_forever, daemon=True).start()
    return servidor


# ------------------------------------------------------------------ Unity falso
RECIBIDOS_POR_UNITY = []     # tipos de mensaje que la página le mandó al Unity falso
PUNTOS = [{"nombre": f"Punto{i}", "x": float(i * 3), "z": float((i % 2) * 4)} for i in range(5)]


async def unity_falso(socket):
    """Se comporta como Unity con un operador conectado."""
    envios = []

    async def enviar(mensaje):
        await socket.send(json.dumps(mensaje))

    async def sin_cargas_despues(enviar):
        await asyncio.sleep(4.0)
        await enviar({"type": "estado_operador", "cargas": 0, "max_cargas": 3, "trampas": ["Punto1"],
                      "progreso": 1, "necesario": 3, "habilidad_puppet": False,
                      "ventana_rescate_s": 0, "multa_factor": 1})

    async def posiciones():
        i = 0
        # Antes del mapa: la página no debe fallar con esto.
        while True:
            i += 1
            await enviar({"type": "posiciones", "t": int(time.time() * 1000),
                          "animatronicos": [{"nombre": "freddy", "x": 1.0 + i * 0.05, "z": 1.0},
                                            {"nombre": "vixy", "x": 6.0, "z": 3.0},
                                            {"nombre": "puppet", "x": 9.0, "z": 0.5}],
                          "jugador": {"nombre": "jugador", "x": 12.0, "z": 2.0}})
            await asyncio.sleep(0.05)

    try:
        async for crudo in socket:
            mensaje = json.loads(crudo)
            RECIBIDOS_POR_UNITY.append(mensaje.get("type"))
            if mensaje.get("type") == "operador_connect":
                envios.append(asyncio.ensure_future(posiciones()))
                await asyncio.sleep(0.3)           # el mapa llega DESPUÉS de las primeras posiciones
                await enviar({"type": "mapa_puntos", "puntos": PUNTOS,
                              "jugador": {"nombre": "jugador", "x": 12.0, "z": 2.0}})
                await enviar({"type": "estado_mapa", "partida_activa": True, "noche": 2, "hora": "3:40 AM",
                              "animatronicos": [
                                  {"nombre": "freddy", "x": 1.0, "z": 1.0, "retraso_s": 0, "espera_s": 0},
                                  {"nombre": "vixy", "x": 6.0, "z": 3.0, "retraso_s": 0, "espera_s": 0},
                                  {"nombre": "puppet", "x": 9.0, "z": 0.5, "retraso_s": 0, "espera_s": 0}],
                              "timestamp": int(time.time() * 1000)})
                await enviar({"type": "estado_jugador", "mirando_pantalla": True, "tareas_pendientes": 2,
                              "caja_pct": 37, "caja_en_peligro": False})
                await enviar({"type": "estado_operador", "cargas": 1, "max_cargas": 3, "trampas": [],
                              "progreso": 0, "necesario": 3, "habilidad_puppet": False,
                              "ventana_rescate_s": 0, "multa_factor": 1})
                envios.append(asyncio.ensure_future(sin_cargas_despues(enviar)))
            elif mensaje.get("type") == "ping_operador":
                await enviar({"type": "pong_operador", "t": mensaje["t"]})
    finally:
        for tarea in envios:
            tarea.cancel()


# -------------------------------------------------------------------- WebDriver
class Navegador:
    def __init__(self):
        self.perfil = tempfile.mkdtemp(prefix="perfil_pagina_", dir=str(Path.home()))
        self.gecko = subprocess.Popen([GECKODRIVER, "--port", str(PUERTO_GECKO)],
                                      stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        time.sleep(1.5)
        respuesta = self._pedir("POST", "/session", {"capabilities": {"alwaysMatch": {
            "browserName": "firefox",
            "moz:firefoxOptions": {"args": ["-headless", "-profile", self.perfil]}}}})
        self.sesion = respuesta["value"]["sessionId"]

    def _pedir(self, metodo, ruta, cuerpo=None):
        datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
        peticion = urllib.request.Request(f"http://127.0.0.1:{PUERTO_GECKO}{ruta}", data=datos, method=metodo,
                                          headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(peticion, timeout=30) as r:
            return json.loads(r.read())

    def abrir(self, url):
        self._pedir("POST", f"/session/{self.sesion}/url", {"url": url})

    def ejecutar(self, script, argumentos=None):
        return self._pedir("POST", f"/session/{self.sesion}/execute/sync",
                           {"script": script, "args": argumentos or []})["value"]

    def texto(self, id_):
        return self.ejecutar("var e=document.getElementById(arguments[0]);return e?e.textContent.trim():null;", [id_])

    def esperar(self, condicion, segundos=10.0):
        limite = time.time() + segundos
        while time.time() < limite:
            try:
                if condicion():
                    return True
            except Exception:
                pass
            time.sleep(0.2)
        return False

    def captura(self, ruta):
        png = self._pedir("GET", f"/session/{self.sesion}/screenshot")["value"]
        Path(ruta).write_bytes(base64.b64decode(png))

    def cerrar(self):
        try:
            self._pedir("DELETE", f"/session/{self.sesion}")
        except Exception:
            pass
        try:
            self.gecko.terminate()      # el geckodriver del snap a veces no deja matarlo
            self.gecko.wait(timeout=5)
        except Exception:
            pass
        subprocess.run(["rm", "-rf", self.perfil])


async def principal(captura):
    web = levantar_web()
    unity = await websockets.serve(unity_falso, "127.0.0.1", PUERTO_UNITY)
    navegador = None
    try:
        navegador = await asyncio.get_running_loop().run_in_executor(None, Navegador)
        url = f"http://127.0.0.1:{PUERTO_WEB}/operador.html?host=127.0.0.1&puerto={PUERTO_UNITY}&auto=1"
        await asyncio.get_running_loop().run_in_executor(None, navegador.abrir, url)

        def esperar(condicion, segundos=10.0):
            return asyncio.get_running_loop().run_in_executor(None, navegador.esperar, condicion, segundos)

        comprobar("la página se conecta (#estado-conexion dice Conectado)",
                  await esperar(lambda: "Conectado" in (navegador.texto("estado-conexion") or "")))
        comprobar("#j1-mirada dice que el jugador 1 mira la pantalla",
                  await esperar(lambda: (navegador.texto("j1-mirada") or "").lower().startswith("mira la pantalla")))
        comprobar("#j1-tareas muestra 2", await esperar(lambda: (navegador.texto("j1-tareas") or "") == "2"))
        comprobar("#j1-caja muestra 37", await esperar(lambda: "37" in (navegador.texto("j1-caja") or "")))
        comprobar("#noche muestra 2", await esperar(lambda: (navegador.texto("noche") or "") == "2"))
        comprobar("#latencia muestra un número en ms",
                  await esperar(lambda: any(c.isdigit() for c in (navegador.texto("latencia") or "")) and
                                "ms" in (navegador.texto("latencia") or ""), 8.0))
        errores = navegador.ejecutar("return (window.__errores || []).length;")
        comprobar(f"sin errores de JavaScript, aunque posiciones llegó antes que el mapa (errores: {errores})", errores == 0)
        comprobar("el mapa deja de mostrar la espera cuando llega mapa_puntos",
                  await esperar(lambda: navegador.ejecutar(
                      "var m=document.getElementById('mapa');return !!m && m.dataset.estado==='listo';")))

        # --- trampas y mini-tarea (Tarea 8) ---
        comprobar("#cargas muestra 1 / 3",
                  await esperar(lambda: (navegador.texto("cargas") or "") == "1 / 3", 4.0))
        comprobar("con carga, #boton-trampa está habilitado",
                  navegador.ejecutar("var b=document.getElementById('boton-trampa');return !!b && !b.disabled;"))
        navegador.ejecutar("var b=document.getElementById('boton-trampa');if(b)b.click();")
        comprobar("al pulsar Colocar trampa, el mapa pasa a modo trampa",
                  navegador.ejecutar("return document.getElementById('mapa').dataset.modo==='trampa';"))
        navegador.ejecutar("var b=document.getElementById('boton-minitarea');if(b)b.click();")
        comprobar("la mini-tarea se muestra al pulsar Recargar",
                  await esperar(lambda: navegador.ejecutar(
                      "var m=document.getElementById('minitarea');return !!m && !m.hidden;")))
        comprobar("Unity recibe mini_tarea_inicio", "mini_tarea_inicio" in RECIBIDOS_POR_UNITY)
        comprobar("sin cargas, #boton-trampa queda deshabilitado",
                  await esperar(lambda: navegador.ejecutar(
                      "var b=document.getElementById('boton-trampa');return !!b && b.disabled;"), 8.0))
        comprobar("#cargas muestra 0 / 3", (navegador.texto("cargas") or "") == "0 / 3")

        css = urllib.request.urlopen(f"http://127.0.0.1:{PUERTO_WEB}/operador.css").read().decode()
        comprobar("el CSS respeta prefers-reduced-motion", "prefers-reduced-motion" in css)

        if captura:
            await asyncio.sleep(1.0)
            navegador.captura(captura)
            print(f"  captura guardada en {captura}")
    finally:
        if navegador:
            navegador.cerrar()
        unity.close()
        await unity.wait_closed()
        web.shutdown()
    print()
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


if __name__ == "__main__":
    analizador = argparse.ArgumentParser()
    analizador.add_argument("--captura", help="guarda una captura de la página en esta ruta")
    argumentos = analizador.parse_args()
    raise SystemExit(asyncio.run(principal(argumentos.captura)))
