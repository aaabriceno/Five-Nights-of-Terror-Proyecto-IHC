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
import os
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
RESPUESTAS_DE_TAREA = []     # los `tarea_respuesta` completos
ORDENES = None               # cola de órdenes del test al Unity falso: ("fin", resultado) o ("nueva",)
PUNTOS = [{"nombre": f"Punto{i}", "x": float(i * 3), "z": float((i % 2) * 4)} for i in range(5)]
RUTAS = [{"animatronico": "freddy", "puntos": ["Punto0", "Punto1", "Punto2"]},
         {"animatronico": "vixy", "puntos": ["Punto4", "Punto3"]}]


async def unity_falso(socket):
    """Se comporta como Unity con un operador conectado."""
    envios = []

    async def enviar(mensaje):
        await socket.send(json.dumps(mensaje))

    async def sin_cargas_despues(enviar):
        await asyncio.sleep(4.0)
        await enviar({"type": "estado_operador", "cargas": 0, "max_cargas": 3, "trampas": ["Punto1"],
                      "progreso": 0, "necesario": 1, "habilidad_puppet": False,
                      "ventana_rescate_s": 0, "multa_factor": 1})

    async def ventana_de_rescate(enviar):
        # Puppet sale: primero J2 no tiene la habilidad, después sí.
        await asyncio.sleep(5.5)
        await enviar({"type": "estado_operador", "cargas": 0, "max_cargas": 3, "trampas": [],
                      "progreso": 0, "necesario": 1, "habilidad_puppet": False,
                      "ventana_rescate_s": 4.2, "multa_factor": 1})
        await asyncio.sleep(2.5)
        await enviar({"type": "estado_operador", "cargas": 0, "max_cargas": 3, "trampas": [],
                      "progreso": 1, "necesario": 1, "habilidad_puppet": True,
                      "ventana_rescate_s": 4.2, "multa_factor": 1})

    async def ordenes_del_test():
        while True:
            orden = await ORDENES.get()
            if orden[0] == "raw":
                await enviar(orden[1])
            elif orden[0] == "rechazar":
                await enviar({"type": "operador_rechazado", "motivo": "tiempo_agotado"})
                await socket.close()
            elif orden[0] == "fin":
                await enviar({"type": "game_over", "result": orden[1], "night": 2,
                              "final_stats": {"duration": 125, "tasks_completed": 7, "tasks_failed": 1,
                                              "total_damage": 100, "score": 700},
                              "timestamp": int(time.time() * 1000)})
            else:
                await enviar({"type": "mapa_puntos", "puntos": PUNTOS, "rutas": RUTAS,
                              "jugador": {"nombre": "jugador", "x": 12.0, "z": 2.0}})
                await enviar({"type": "estado_mapa", "partida_activa": True, "noche": 3, "hora": "12:00 AM",
                              "animatronicos": [{"nombre": "freddy", "x": 1.0, "z": 1.0, "retraso_s": 0, "espera_s": 0}],
                              "timestamp": int(time.time() * 1000)})

    async def posiciones():
        i = 0
        # Antes del mapa: la página no debe fallar con esto.
        while True:
            i += 1
            await enviar({"type": "posiciones", "t": int(time.time() * 1000), "camara_yaw": -45.0,
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
                await enviar({"type": "mapa_puntos", "puntos": PUNTOS, "rutas": RUTAS,
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
                              "progreso": 0, "necesario": 1, "habilidad_puppet": False,
                              "ventana_rescate_s": 0, "multa_factor": 1})
                envios.append(asyncio.ensure_future(ordenes_del_test()))
                envios.append(asyncio.ensure_future(sin_cargas_despues(enviar)))
                envios.append(asyncio.ensure_future(ventana_de_rescate(enviar)))
            elif mensaje.get("type") == "ping_operador":
                await enviar({"type": "pong_operador", "t": mensaje["t"]})
            elif mensaje.get("type") == "tarea_pedir":
                if mensaje.get("tipo") == "circuito":
                    await enviar({"type": "tarea", "id": 2, "tipo": "circuito", "noche": 2, "clase": "", "enunciado": "",
                                  "datos": [], "entradas": ["A", "B", "C"], "salida": "G3", "objetivo": True,
                                  "puertas": [{"id": "G1", "op": "AND", "a": "A", "b": "B"},
                                              {"id": "G2", "op": "NOT", "a": "C", "b": ""},
                                              {"id": "G3", "op": "AND", "a": "G1", "b": "G2"}]})
                else:
                    await enviar({"type": "tarea", "id": 1, "tipo": "calculo", "noche": 2, "clase": "binario",
                                  "enunciado": "Pasa 101 de binario a decimal", "datos": ["101"],
                                  "entradas": [], "puertas": [], "salida": "", "objetivo": False})
            elif mensaje.get("type") == "tarea_respuesta":
                RESPUESTAS_DE_TAREA.append(mensaje)
                circuito = mensaje.get("id") == 2
                correcto = mensaje.get("interruptores") == [1, 1, 0] if circuito else mensaje.get("valor") == 5
                await enviar({"type": "tarea_resultado", "id": mensaje.get("id"), "tipo": "circuito" if circuito else "calculo",
                              "correcto": correcto, "bloqueo_s": 0 if correcto else 3})
                if not correcto:
                    await enviar({"type": "estado_operador", "cargas": 1, "max_cargas": 3, "trampas": [], "progreso": 0,
                                  "necesario": 1, "habilidad_puppet": False, "bloqueo_s": 3, "ventana_rescate_s": 0,
                                  "multa_factor": 1})
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

    def captura(self, ruta):
        """Guarda una foto de la página (para mirarla a ojo cuando se toca el dibujo del mapa)."""
        datos = self._pedir("GET", f"/session/{self.sesion}/screenshot")["value"]
        Path(ruta).write_bytes(base64.b64decode(datos))

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
    global ORDENES
    ORDENES = asyncio.Queue()
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
        comprobar("sin cargas, #boton-trampa queda deshabilitado",
                  await esperar(lambda: navegador.ejecutar(
                      "var b=document.getElementById('boton-trampa');return !!b && b.disabled;"), 8.0))
        comprobar("#cargas muestra 0 / 3", (navegador.texto("cargas") or "") == "0 / 3")

        # --- rescate de Puppet (Tarea 9) ---
        comprobar("al salir Puppet aparece #alerta-puppet",
                  await esperar(lambda: navegador.ejecutar(
                      "var a=document.getElementById('alerta-puppet');return !!a && !a.hidden;"), 10.0))
        comprobar("sin la habilidad, #boton-rescate está deshabilitado",
                  navegador.ejecutar("var b=document.getElementById('boton-rescate');return !!b && b.disabled;"))
        comprobar("con la habilidad y la ventana abierta, #boton-rescate se habilita",
                  await esperar(lambda: navegador.ejecutar(
                      "var b=document.getElementById('boton-rescate');return !!b && !b.disabled;"), 8.0))
        navegador.ejecutar("var b=document.getElementById('boton-rescate');if(b)b.click();")
        comprobar("Unity recibe accion_operador al rescatar",
                  await esperar(lambda: RECIBIDOS_POR_UNITY.count("accion_operador") >= 1, 3.0))
        comprobar("al cerrarse la ventana la alerta desaparece",
                  await esperar(lambda: navegador.ejecutar(
                      "var a=document.getElementById('alerta-puppet');return !!a && a.hidden;"), 8.0))

        # --- terminal: un cálculo da carga y un circuito da el rescate (Unity inventa y valida) ---
        ORDENES.put_nowait(("raw", {"type": "estado_operador", "cargas": 1, "max_cargas": 3, "trampas": [], "progreso": 0,
                                    "necesario": 1, "habilidad_puppet": False, "bloqueo_s": 0, "ventana_rescate_s": 0,
                                    "multa_factor": 1}))
        visible_terminal = "var m=document.getElementById('minitarea');return !!m && !m.hidden;"
        oculta_terminal = "var m=document.getElementById('minitarea');return !!m && m.hidden;"
        habilitado = lambda i: ("var b=document.getElementById('" + i + "');return !!b && !b.disabled;")
        comprobar("con cargas por debajo del máximo, Recargar está habilitado",
                  await esperar(lambda: navegador.ejecutar(habilitado("boton-minitarea")), 4.0))
        comprobar("sin el rescate, Desbloquear rescate está habilitado",
                  await esperar(lambda: navegador.ejecutar(habilitado("boton-circuito")), 4.0))
        navegador.ejecutar("document.getElementById('boton-minitarea').click();")
        comprobar("Recargar pide un cálculo a Unity", await esperar(lambda: "tarea_pedir" in RECIBIDOS_POR_UNITY, 3.0))
        comprobar("la terminal muestra el problema que inventó Unity",
                  await esperar(lambda: navegador.ejecutar(visible_terminal) and
                                (navegador.texto("mt-enunciado") or "") == "Pasa 101 de binario a decimal", 3.0))
        comprobar("un cálculo muestra la casilla de respuesta y no el circuito",
                  navegador.ejecutar("return !document.getElementById('mt-calculo').hidden && document.getElementById('mt-circuito').hidden;"))
        responder = lambda texto: navegador.ejecutar(
            "var i=document.getElementById('mt-respuesta');i.value=arguments[0];"
            "document.getElementById('mt-calculo').requestSubmit();", [texto])
        if os.environ.get("CAPTURA_TERMINAL"):
            await asyncio.sleep(0.6)             # que termine el fundido de entrada de la ventana
            navegador.captura(os.environ["CAPTURA_TERMINAL"] + "/terminal_calculo.png")
        responder("abc")
        comprobar("una respuesta que no es un número no se manda y se explica",
                  "entero" in (navegador.texto("mt-mensaje") or "") and not RESPUESTAS_DE_TAREA)
        responder("7")
        comprobar("una respuesta equivocada llega a Unity",
                  await esperar(lambda: len(RESPUESTAS_DE_TAREA) == 1 and RESPUESTAS_DE_TAREA[0].get("valor") == 7
                                and RESPUESTAS_DE_TAREA[0].get("id") == 1, 3.0))
        comprobar("al fallar la terminal se cierra", await esperar(lambda: navegador.ejecutar(oculta_terminal), 3.0))
        comprobar("y se bloquea: Recargar queda deshabilitado y lo dice",
                  await esperar(lambda: not navegador.ejecutar(habilitado("boton-minitarea")) and
                                "bloqueada" in (navegador.texto("boton-minitarea") or "").lower(), 3.0))
        comprobar("pasados 3 s la terminal se desbloquea sola",
                  await esperar(lambda: navegador.ejecutar(habilitado("boton-minitarea")) and
                                "Recargar" in (navegador.texto("boton-minitarea") or ""), 6.0))
        navegador.ejecutar("document.getElementById('boton-minitarea').click();")
        await esperar(lambda: navegador.ejecutar(visible_terminal), 3.0)
        responder("5")
        comprobar("la respuesta correcta llega a Unity",
                  await esperar(lambda: len(RESPUESTAS_DE_TAREA) == 2 and RESPUESTAS_DE_TAREA[1].get("valor") == 5, 3.0))
        comprobar("y la terminal se cierra sin bloquearse",
                  await esperar(lambda: navegador.ejecutar(oculta_terminal) and
                                navegador.ejecutar(habilitado("boton-minitarea")), 3.0))

        navegador.ejecutar("document.getElementById('boton-circuito').click();")
        comprobar("el circuito se muestra con sus 3 interruptores y 3 puertas",
                  await esperar(lambda: navegador.ejecutar(
                      "return document.querySelectorAll('#mt-interruptores button').length===3 &&"
                      "document.querySelectorAll('#mt-puertas li').length===3 &&"
                      "!document.getElementById('mt-circuito').hidden && document.getElementById('mt-calculo').hidden;"), 3.0))
        comprobar("las puertas se leen en español (G1 = A Y B, G2 = NO C...)",
                  navegador.ejecutar("return Array.from(document.querySelectorAll('#mt-puertas li span:first-child'))"
                                     ".map(function(x){return x.textContent;}).join('|');") == "G1 = A Y B|G2 = NO C|G3 = G1 Y G2")
        comprobar("con todo en 0 el foco dice que aún no es lo que se pide",
                  "se pide 1" in (navegador.texto("mt-foco") or "") and
                  not navegador.ejecutar("return document.getElementById('mt-foco').classList.contains('resuelto');"))
        if os.environ.get("CAPTURA_TERMINAL"):
            await asyncio.sleep(0.6)             # que termine el fundido de entrada de la ventana
            navegador.captura(os.environ["CAPTURA_TERMINAL"] + "/terminal_circuito_sin_resolver.png")
        navegador.ejecutar("var b=document.querySelectorAll('#mt-interruptores button');b[0].click();b[1].click();")
        comprobar("al subir A y B (con C en 0) el circuito queda resuelto y el foco lo dice",
                  navegador.ejecutar("return document.getElementById('mt-foco').classList.contains('resuelto');"))
        comprobar("la puerta G1 se enciende al subir A y B",
                  navegador.ejecutar("return document.querySelector('#mt-puertas li[data-id=G1]').classList.contains('encendida');"))
        if os.environ.get("CAPTURA_TERMINAL"):
            await asyncio.sleep(0.6)             # que termine el fundido de entrada de la ventana
            navegador.captura(os.environ["CAPTURA_TERMINAL"] + "/terminal_circuito_resuelto.png")
        navegador.ejecutar("document.getElementById('mt-enviar-circuito').click();")
        comprobar("Enviar manda los interruptores a Unity",
                  await esperar(lambda: len(RESPUESTAS_DE_TAREA) == 3 and RESPUESTAS_DE_TAREA[2].get("interruptores") == [1, 1, 0]
                                and RESPUESTAS_DE_TAREA[2].get("id") == 2, 3.0))
        comprobar("y la terminal se cierra al resolver el circuito", await esperar(lambda: navegador.ejecutar(oculta_terminal), 3.0))

        # --- fin de la partida: el jugador 2 no se queda con la pantalla congelada ---
        js = lambda codigo: navegador.ejecutar(codigo)
        visible = "var f=document.getElementById('fin-partida');return !!f && !f.hidden;"
        ORDENES.put_nowait(("fin", "loss"))
        comprobar("al morir el jugador 1 aparece #fin-partida",
                  await esperar(lambda: js(visible), 5.0))
        comprobar("el fin dice que el jugador 1 fue atrapado",
                  "ATRAPADO" in (navegador.texto("fin-detalle") or "").upper())
        comprobar("el fin muestra la duración 2:05 y las tareas",
                  "2:05" in (navegador.texto("fin-estadisticas") or "") and "7" in (navegador.texto("fin-estadisticas") or ""))
        comprobar("terminada la partida, Recargar, Desbloquear rescate y Colocar trampa quedan deshabilitados",
                  js("return document.getElementById('boton-minitarea').disabled && document.getElementById('boton-circuito').disabled"
                     " && document.getElementById('boton-trampa').disabled;"))
        comprobar("terminada la partida, los botones Retrasar quedan deshabilitados",
                  js("var b=document.querySelectorAll('#lista-amenazas button');"
                     "return b.length>0 && Array.from(b).every(function(x){return x.disabled;});"))
        navegador.ejecutar("document.getElementById('fin-esperar').click();")
        comprobar("«Esperar otra partida» oculta el fin y vuelve a esperar señal",
                  not js(visible) and js("return document.getElementById('mapa').dataset.estado==='esperando';")
                  and (navegador.texto("noche") or "") == "–")
        ORDENES.put_nowait(("nueva",))
        comprobar("al empezar otra noche el mapa vuelve a estar listo y #noche muestra 3",
                  await esperar(lambda: js("return document.getElementById('mapa').dataset.estado==='listo';")
                                and (navegador.texto("noche") or "") == "3", 5.0))
        ORDENES.put_nowait(("fin", "win"))
        comprobar("si el jugador 1 gana, el fin dice que sobrevivió",
                  await esperar(lambda: js(visible) and "SOBREVIVI" in (navegador.texto("fin-detalle") or "").upper(), 5.0))
        navegador.ejecutar("document.getElementById('fin-salir').click();")
        comprobar("«Salir» desconecta y oculta el fin",
                  await esperar(lambda: "Desconectado" in (navegador.texto("estado-conexion") or "")
                                and not js(visible), 5.0))
        await asyncio.sleep(3.5)
        comprobar("tras «Salir» la página no se reconecta sola",
                  "Desconectado" in (navegador.texto("estado-conexion") or ""))
        errores = navegador.ejecutar("return (window.__errores || []).length;")
        comprobar(f"sin errores de JavaScript tras el fin de la partida (errores: {errores})", errores == 0)

        # --- J1 se va / J2 se cae: avisos, 3 intentos en 15 s y rechazo ---
        navegador.ejecutar("document.getElementById('conectar').click();")
        comprobar("«Conectar» vuelve a conectar tras Salir",
                  await esperar(lambda: "Conectado" in (navegador.texto("estado-conexion") or ""), 8.0))

        # --- lo que hace el jugador 1 con la cámara se le ve al jugador 2 ---
        comprobar("el panel del jugador 1 dice hacia dónde mira su cámara",
                  await esperar(lambda: (navegador.texto("j1-camara") or "") == "Izquierda 45°", 4.0))

        # --- el jugador 1 pierde el Wi-Fi un momento: no es el fin de la partida ---
        sin_j1 = "var r=document.getElementById('j1-sin-conexion');return !!r && !r.hidden;"
        ORDENES.put_nowait(("raw", {"type": "jugador1_salio", "motivo": "conexion_perdida"}))
        comprobar("si el jugador 1 pierde la conexión, la página lo avisa sin dar la partida por terminada",
                  await esperar(lambda: js(sin_j1) and not js(visible)
                                and "SIN CONEXI" in (navegador.texto("j1-sin-conexion") or "").upper(), 4.0))
        ORDENES.put_nowait(("raw", {"type": "jugador1_volvio"}))
        comprobar("cuando el jugador 1 vuelve, el aviso desaparece", await esperar(lambda: not js(sin_j1), 4.0))

        # --- un nombre con HTML llegado por la red se muestra como texto, no se ejecuta ---
        hostil = "<img src=x onerror=\"window.__xss=1\">"
        ORDENES.put_nowait(("raw", {"type": "estado_mapa", "partida_activa": True, "noche": 2, "hora": "4:00 AM",
                                    "animatronicos": [{"nombre": "freddy", "x": 1.0, "z": 1.0, "retraso_s": 0, "espera_s": 0},
                                                      {"nombre": hostil, "x": 2.0, "z": 2.0, "retraso_s": 0, "espera_s": 0}],
                                    "timestamp": int(time.time() * 1000)}))
        comprobar("un animatrónico con HTML en el nombre aparece en la lista como texto literal",
                  await esperar(lambda: hostil in (navegador.ejecutar(
                      "var l=document.getElementById('lista-amenazas');return l?l.textContent:'';") or ""), 5.0))
        comprobar("y su HTML no se ejecutó en el navegador del jugador 2 (antes se armaba con innerHTML)",
                  navegador.ejecutar("return window.__xss === undefined;"))
        comprobar("ni quedó una imagen inyectada en la página",
                  navegador.ejecutar("return document.querySelectorAll('#lista-amenazas img').length === 0;"))

        # --- sin guía: el jugador 2 deduce por dónde va cada animatrónico ---
        # Unity sigue mandando las rutas en `mapa_puntos`, pero la página no las enseña.
        comprobar("el panel no muestra la ruta de ningún animatrónico",
                  "Ruta:" not in (navegador.ejecutar(
                      "var l=document.getElementById('lista-amenazas');return l?l.textContent:'';") or "")
                  and navegador.ejecutar("return document.querySelectorAll('[id^=\"ruta-\"]').length === 0;"))
        # Se apunta cada trazo del lienzo. Los animatrónicos se pintan con relleno de su
        # color, nunca con trazo: un trazo de ese color es una ruta (línea o círculo final).
        navegador.ejecutar("""
            window.__trazos = [];
            var original = CanvasRenderingContext2D.prototype.stroke;
            CanvasRenderingContext2D.prototype.stroke = function () {
              window.__trazos.push(String(this.strokeStyle).toLowerCase());
              return original.apply(this, arguments);
            };""")
        await asyncio.sleep(0.6)
        comprobar("ni el mapa dibuja las rutas (ningún trazo del color de un animatrónico)",
                  navegador.ejecutar("return window.__trazos.length > 0 && !window.__trazos.some(function (c) {"
                                     " return ['#6fcf6f', '#ff5a5a', '#d9a066'].indexOf(c) >= 0; });"))
        await asyncio.sleep(0.5)
        if os.environ.get("CAPTURA_PAGINA"):
            navegador.captura(os.environ["CAPTURA_PAGINA"])

        # --- lo que hace el jugador 2 se le cuenta a él en segunda persona ---
        ORDENES.put_nowait(("raw", {"type": "efecto", "animatronic": "puppet", "tipo": "puppet_rescatado",
                                    "segundos": 0, "message": "Tu compañero te salvó de Puppet (caja ×2 más rápida)",
                                    "id": 1, "multa_factor": 2, "segundos_caja": 15}))
        comprobar("el rescate le dice al jugador 2 qué hizo y cuánto dura ahora la caja del jugador 1",
                  await esperar(lambda: "Rescataste a Puppet" in (navegador.texto("aviso") or "")
                                and "ahora dura 15 s" in (navegador.texto("aviso") or ""), 4.0))
        comprobar("y no le repite el texto escrito para el jugador 1",
                  "Tu compañero" not in (navegador.texto("aviso") or ""))
        ORDENES.put_nowait(("raw", {"type": "estado_jugador", "mirando_pantalla": True, "tareas_pendientes": 1,
                                    "caja_pct": 60, "caja_en_peligro": False}))
        ORDENES.put_nowait(("raw", {"type": "estado_operador", "cargas": 1, "max_cargas": 3, "trampas": [],
                                    "progreso": 0, "necesario": 1, "habilidad_puppet": False,
                                    "ventana_rescate_s": 0, "multa_factor": 2, "segundos_caja": 15}))
        comprobar("el panel del jugador 1 muestra cuánto dura ahora su caja (15 s)",
                  await esperar(lambda: "dura 15 s" in (navegador.texto("j1-caja") or ""), 4.0))
        ORDENES.put_nowait(("raw", {"type": "jugador1_salio", "motivo": "salio"}))
        comprobar("si el jugador 1 se va, la página lo avisa",
                  await esperar(lambda: js(visible) and "SE FUE" in (navegador.texto("fin-titulo") or "").upper(), 5.0))
        navegador.ejecutar("document.getElementById('fin-esperar').click();")
        ORDENES.put_nowait(("raw", {"type": "juego_cerrado"}))
        comprobar("si el juego se cierra, la página lo avisa",
                  await esperar(lambda: js(visible) and "JUEGO SE CERR" in (navegador.texto("fin-titulo") or "").upper(), 5.0))
        navegador.ejecutar("document.getElementById('fin-esperar').click();")

        banner = "var r=document.getElementById('reconectando');return !!r && !r.hidden;"
        unity.close()
        await unity.wait_closed()
        comprobar("al caerse la conexión aparece el aviso de reconexión con los intentos",
                  await esperar(lambda: js(banner) and "INTENTO" in (navegador.texto("reconectando") or ""), 5.0))
        unity = await websockets.serve(unity_falso, "127.0.0.1", PUERTO_UNITY)
        comprobar("si el juego vuelve antes de 15 s, la página se reconecta sola",
                  await esperar(lambda: "Conectado" in (navegador.texto("estado-conexion") or "")
                                and not js(banner), 12.0))

        unity.close()
        await unity.wait_closed()
        comprobar("de nuevo sin juego: reconectando",
                  await esperar(lambda: js(banner), 5.0))
        comprobar("a los 15 s sin volver aparece «Sin conexión con el juego»",
                  await esperar(lambda: js(visible) and "SIN CONEXI" in (navegador.texto("fin-titulo") or "").upper(), 17.0))
        comprobar("y el aviso de reconexión desaparece", not js(banner))
        comprobar("la página dejó de intentar (sigue Desconectado)",
                  "Desconectado" in (navegador.texto("estado-conexion") or ""))
        unity = await websockets.serve(unity_falso, "127.0.0.1", PUERTO_UNITY)
        await asyncio.sleep(6.0)
        comprobar("pasada la ventana no se reconecta sola aunque el juego vuelva",
                  "Desconectado" in (navegador.texto("estado-conexion") or ""))
        navegador.ejecutar("document.getElementById('fin-esperar').click();")      # Reconectar
        comprobar("«Reconectar» entra de nuevo si el relay lo acepta",
                  await esperar(lambda: "Conectado" in (navegador.texto("estado-conexion") or "")
                                and not js(visible), 8.0))
        ORDENES.put_nowait(("rechazar",))
        comprobar("si el relay lo rechaza (tiempo agotado) la página lo explica y no insiste",
                  await esperar(lambda: js(visible) and "NO PUEDES" in (navegador.texto("fin-titulo") or "").upper(), 5.0))
        await asyncio.sleep(3.5)
        comprobar("tras el rechazo sigue Desconectado y sin aviso de reconexión",
                  "Desconectado" in (navegador.texto("estado-conexion") or "") and not js(banner))
        errores = navegador.ejecutar("return (window.__errores || []).length;")
        comprobar(f"sin errores de JavaScript en las reconexiones (errores: {errores})", errores == 0)

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
