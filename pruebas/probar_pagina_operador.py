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
import json
import os
import socketserver
import subprocess
import sys
import tempfile
import threading
import time
import urllib.request
from functools import partial
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(RAIZ / "UnityRelay"))
import servir_pagina  # noqa: E402  el mismo servidor que usa iniciar.sh (con /sustos/)
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
def levantar_web():
    socketserver.TCPServer.allow_reuse_address = True
    servidor = socketserver.ThreadingTCPServer(("127.0.0.1", PUERTO_WEB),
                                               partial(servir_pagina.SinCache, directory=str(CARPETA_PAGINA)))
    threading.Thread(target=servidor.serve_forever, daemon=True).start()
    return servidor


# ------------------------------------------------------------------ Unity falso
RECIBIDOS_POR_UNITY = []     # tipos de mensaje que la página le mandó al Unity falso
RESPUESTAS_DE_TAREA = []     # los `tarea_respuesta` completos
ACCIONES_RECIBIDAS = []      # los `accion_operador` completos (para saber qué tecla mandó qué)
ORDENES = None               # cola de órdenes del test al Unity falso: ("fin", resultado) o ("nueva",)
PUNTOS = [{"nombre": f"Punto{i}", "x": float(i * 3), "z": float((i % 2) * 4)} for i in range(5)]
RUTAS = [{"animatronico": "freddy", "puntos": ["Punto0", "Punto1", "Punto2"]},
         {"animatronico": "vixy", "puntos": ["Punto4", "Punto3"]}]


AVISOS_J2 = []               # lo que la página mandó por su canal de aviso: "pagina_j2", o True/False de «Cómo jugar»
ORDENES_AVISO = None         # mensajes que el test le manda a la página por ese canal (como haría el relay)


async def canal_de_aviso(socket):
    """El relay del lado del canal de aviso de la página (pagina_j2, 2026-11-22)."""
    AVISOS_J2.append("pagina_j2")
    await socket.send(json.dumps({"type": "estado_tutoriales", "jugador1_en_tutorial": False,
                                  "jugador2_en_como_jugar": False}))

    async def ordenes():
        while True:
            await socket.send(json.dumps(await ORDENES_AVISO.get()))

    tarea = asyncio.ensure_future(ordenes())
    try:
        async for crudo in socket:
            mensaje = json.loads(crudo)
            if mensaje.get("type") == "j2_como_jugar":
                AVISOS_J2.append(mensaje.get("abierto"))
    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        tarea.cancel()


async def mensajes_desde(socket, primero):
    yield primero
    async for crudo in socket:
        yield crudo


async def servidor_falso(socket):
    """Un socket que se anuncia con pagina_j2 es el canal de aviso; los demás hablan con «Unity»."""
    try:
        primero = await socket.recv()
    except websockets.exceptions.ConnectionClosed:
        return
    if json.loads(primero).get("type") == "pagina_j2":
        await canal_de_aviso(socket)
    else:
        await unity_falso(socket, primero)


def ataque(animatronico):
    return {"type": "attack", "attack_id": "atk_1", "animatronic": animatronico,
            "message": f"{animatronico} te encontró", "timestamp": int(time.time() * 1000)}


async def unity_falso(socket, primero):
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
            elif orden[0] == "ataque":
                await enviar(ataque(orden[1]))
            elif orden[0] == "fin":
                if orden[1] == "loss":
                    await enviar(ataque("Freddy"))     # como Unity: primero el ataque, después el game_over
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
        async for crudo in mensajes_desde(socket, primero):
            mensaje = json.loads(crudo)
            RECIBIDOS_POR_UNITY.append(mensaje.get("type"))
            if mensaje.get("type") == "accion_operador":
                ACCIONES_RECIBIDAS.append(mensaje)
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
                if mensaje.get("tipo") == "ecuacion":
                    await enviar({"type": "tarea", "id": 2, "tipo": "ecuacion", "noche": 2, "clase": "ecuacion",
                                  "enunciado": "Encuentra x: 2x + 3 = 11", "datos": ["lineal", "2", "+", "3", "11"]})
                else:
                    await enviar({"type": "tarea", "id": 1, "tipo": "calculo", "noche": 2, "clase": "aritmetica",
                                  "enunciado": "Calcula 2 + 3", "datos": ["2", "+", "3"]})
            elif mensaje.get("type") == "tarea_respuesta":
                RESPUESTAS_DE_TAREA.append(mensaje)
                ecuacion = mensaje.get("id") == 2
                correcto = mensaje.get("valor") == (4 if ecuacion else 5)
                await enviar({"type": "tarea_resultado", "id": mensaje.get("id"), "tipo": "ecuacion" if ecuacion else "calculo",
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
            # Sin gesto real (los clics de la prueba son por JavaScript), Firefox no
            # dejaría sonar el grito del susto: se permite el autoplay con sonido.
            "moz:firefoxOptions": {"args": ["-headless", "-profile", self.perfil],
                                   "prefs": {"media.autoplay.default": 0}}}}})
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


def resolver(enunciado):
    """La respuesta de un cálculo («Calcula 7 × 8») o de una ecuación («Encuentra x: 3x + 5 = 20»)."""
    import re
    m = re.fullmatch(r"Calcula (\d+) × (\d+)", enunciado or "")
    if m:
        return int(m[1]) * int(m[2])
    m = re.fullmatch(r"Encuentra x: (\d+)x \+ (\d+) = (\d+)", enunciado or "")
    if m:
        return (int(m[3]) - int(m[2])) // int(m[1])
    return None


async def probar_como_jugar(navegador, esperar, abierto, menu):
    """«Cómo jugar» con 8 diapositivas y práctica (2026-11-20; cupo de cálculos 2026-11-22): se recorre entero y se practica todo."""
    js = navegador.ejecutar
    # Clic con un evento (los elementos del SVG no tienen .click()) y teclas como las manda el navegador.
    clic = lambda id_: js("var e=document.getElementById(arguments[0]);if(!e)return false;"
                          "e.dispatchEvent(new MouseEvent('click',{bubbles:true}));return true;", [id_])
    tecla = lambda clave, id_=None: js(
        "var e=arguments[1]?document.getElementById(arguments[1]):document;"
        "e.dispatchEvent(new KeyboardEvent('keydown',{key:arguments[0],bubbles:true}));", [clave, id_])
    diapositiva = lambda: js("return document.getElementById('como-jugar').dataset.diapositiva;")
    habilitado = lambda id_: js("var b=document.getElementById(arguments[0]);return !!b && !b.disabled;", [id_])
    texto = lambda id_: navegador.texto(id_) or ""

    js("document.getElementById('menu-como-jugar').click();")
    comprobar("«Cómo jugar» abre en la diapositiva 1 de 7, encima del menú",
              abierto() and diapositiva() == "1" and texto("cj-contador") == "1 / 8")
    comprobar("se ve una sola diapositiva a la vez",
              js("return document.querySelectorAll('#como-jugar .cj-diapositiva:not([hidden])').length;") == 1)
    comprobar("en la primera no hay «Anterior»", not habilitado("cj-anterior"))
    clic("cj-siguiente")
    comprobar("«Siguiente» pasa a la 2", diapositiva() == "2" and texto("cj-contador") == "2 / 8")
    tecla("ArrowLeft")
    comprobar("la flecha izquierda vuelve a la 1", diapositiva() == "1")
    tecla("ArrowRight")
    comprobar("la flecha derecha avanza a la 2", diapositiva() == "2")

    # 2 · El mapa: tocar a cada animatrónico
    clic("cj-conocer-vixy")
    comprobar("tocar a Vixy dice que entra por el ducto izquierdo", "ducto izquierdo" in texto("cj-ficha").lower())
    clic("cj-conocer-puppet")
    comprobar("tocar a Puppet habla de su caja de música", "caja" in texto("cj-ficha").lower())
    clic("cj-conocer-freddy")
    comprobar("tocar a los tres marca «3 / 3»", "3 / 3" in texto("cj-conocidos"))

    # 3 · Jugador 1 en vivo: encontrar las filas
    tecla("ArrowRight")
    comprobar("la 3 pregunta por la fila de la mirada", diapositiva() == "3" and "mirando" in texto("cj-pregunta-panel"))
    clic("cj-fila-caja")
    comprobar("tocar otra fila dice «Esa no»", "Esa no" in texto("cj-mensaje-panel"))
    clic("cj-fila-mirada")
    comprobar("tocar «Mirada» pasa a la pregunta de la caja", "caja" in texto("cj-pregunta-panel").lower())
    clic("cj-fila-caja")
    comprobar("tocar la caja termina el panel", "Bien" in texto("cj-mensaje-panel") or "Eso" in texto("cj-mensaje-panel"))

    # 4 · Retrasar
    tecla("ArrowRight")
    mapa_de = lambda id_: js("var m=document.getElementById(arguments[0]);return m?m.dataset.freddy:null;", [id_])
    comprobar("la 4 muestra a Freddy caminando", diapositiva() == "4" and mapa_de("cj-mapa-retrasar") == "normal")
    clic("cj-retrasar")
    comprobar("«Retrasar» lo pone LENTO", await esperar(lambda: mapa_de("cj-mapa-retrasar") == "lento", 2.0))
    comprobar("y dice cuánto le queda", "retrasado" in texto("cj-detalle-retrasar"))
    comprobar("mientras tanto «Retrasar» queda deshabilitado", not habilitado("cj-retrasar"))

    # 5 · Trampa
    tecla("ArrowRight")
    modo = lambda: js("return document.getElementById('cj-mapa-trampa').dataset.modo || '';")
    comprobar("la 5 empieza con 1 trampa en el bolsillo", diapositiva() == "5" and texto("cj-cargas") == "1 / 3")
    comprobar("y explica que las trampas salen del bolsillo y se ganan con cálculos",
              all(p in texto("cj-diapositiva-5").lower() for p in ("bolsillo", "cálculo")))
    clic("cj-punto-pasillo-der")
    comprobar("tocar un punto sin «Colocar trampa» pide apretarlo primero", "Colocar trampa" in texto("cj-mensaje-trampa"))
    clic("cj-poner-trampa")
    comprobar("«Colocar trampa» marca los puntos para elegir", modo() == "trampa")
    clic("cj-punto-pasillo-der")
    comprobar("fuera del camino de Freddy dice que no pasa por ahí, sin gastar la carga",
              "no pasa por ahí" in texto("cj-mensaje-trampa") and texto("cj-cargas") == "1 / 3" and modo() == "")
    clic("cj-poner-trampa")
    clic("cj-punto-pasillo-izq")
    comprobar("en su camino la trampa queda puesta y gasta la carga",
              "puesta" in texto("cj-mensaje-trampa").lower() and texto("cj-cargas") == "0 / 3")
    comprobar("Freddy cae en la trampa (ATRAPADO)", await esperar(lambda: mapa_de("cj-mapa-trampa") == "atrapado", 25.0))
    clic("cj-poner-trampa")
    clic("cj-punto-comedor")
    comprobar("sin cargas manda a resolver un cálculo", "cálculo" in texto("cj-mensaje-trampa"))

    # 6 · Terminal: el cálculo y lo que da (cupo de cálculos, 2026-11-22)
    tecla("ArrowRight")
    comprobar("la 6 es la terminal del cálculo", diapositiva() == "6")
    comprobar("dice qué da un cálculo y el cupo por noche (6, 5, 4, 3, 3, 3)",
              all(p in texto("cj-diapositiva-6") for p in ("+1 trampa", "+20 %", "6", "5", "4", "3")))
    comprobar("el contador empieza en 6 / 6, la batería del jugador 1 en 60 % y el bolsillo en 0 / 3",
              texto("cj-cupo") == "6 / 6" and texto("cj-bateria-j1") == "60 %" and texto("cj-cargas-terminal") == "0 / 3")
    clic("cj-calculo")
    comprobar("«Resolver cálculo» muestra un cálculo como el del juego",
              await esperar(lambda: resolver(texto("cj-enunciado")) is not None, 2.0) and texto("cj-enunciado").startswith("Calcula"))
    tecla("ArrowRight", "cj-respuesta")
    comprobar("la flecha derecha escribiendo en la respuesta no cambia de diapositiva", diapositiva() == "6")
    responder = lambda valor, casilla="cj-respuesta", enviar="cj-enviar": js(
        "document.getElementById(arguments[1]).value=arguments[0];document.getElementById(arguments[2]).click();",
        [str(valor), casilla, enviar])
    responder("abc")
    comprobar("«abc» pide un número entero", "Escribe solo un número entero." in texto("cj-mensaje-terminal"))
    responder(resolver(texto("cj-enunciado")) + 1)
    comprobar("una respuesta mala bloquea la terminal y no gasta cupo",
              "Incorrecto" in texto("cj-mensaje-terminal") and not habilitado("cj-calculo") and texto("cj-cupo") == "6 / 6")
    comprobar("a los 3 s la terminal vuelve", await esperar(lambda: habilitado("cj-calculo"), 5.0))
    clic("cj-calculo")
    await esperar(lambda: resolver(texto("cj-enunciado")) is not None, 2.0)
    responder(resolver(texto("cj-enunciado")))
    comprobar("al acertar sale volando una ficha «+1 TRAMPA» hacia el bolsillo",
              "+1 TRAMPA" in (js("var f=document.querySelector('.cj-ficha-volando');return f?f.textContent:'';") or ""))
    comprobar("el cupo baja a 5 / 6, el bolsillo sube a 1 / 3 y la batería a 80 %",
              texto("cj-cupo") == "5 / 6" and texto("cj-cargas-terminal") == "1 / 3" and texto("cj-bateria-j1") == "80 %")
    comprobar("y el mensaje lo dice", "+1 trampa" in texto("cj-mensaje-terminal") and "+20 %" in texto("cj-mensaje-terminal"))

    # 7 · Salvar a Puppet: ecuación y rescate
    tecla("ArrowRight")
    comprobar("la 7 es salvar a Puppet, con la caja durando 30 s",
              diapositiva() == "7" and texto("cj-segundos-caja") == "30 s")
    comprobar("el rescate empieza bloqueado", not habilitado("cj-rescatar"))
    clic("cj-ecuacion")
    comprobar("«Resolver ecuación» muestra una ecuación",
              await esperar(lambda: texto("cj-enunciado-ecuacion").startswith("Encuentra x"), 2.0))
    responder(resolver(texto("cj-enunciado-ecuacion")), "cj-respuesta-ecuacion", "cj-enviar-ecuacion")
    comprobar("la ecuación resuelta hace salir a Puppet de su caja y habilita «Rescatar a Puppet»",
              "PUPPET" in texto("cj-alerta-puppet").upper() and habilitado("cj-rescatar")
              and js("return document.getElementById('cj-caja-puppet').dataset.puppet;") == "fuera")
    clic("cj-rescatar")
    comprobar("«Rescatar a Puppet» lo devuelve a su caja y la caja pasa a durar 15 s",
              "Rescataste" in texto("cj-mensaje-puppet") and texto("cj-segundos-caja") == "15 s"
              and js("return document.getElementById('cj-caja-puppet').dataset.puppet;") == "dentro")

    # 8 · Listo
    tecla("ArrowRight")
    hechos = lambda: js("return Array.from(document.querySelectorAll('#cj-lista [data-paso]'))"
                        ".map(function (e) { return e.dataset.hecho; }).join(',');")
    comprobar("la 8 tiene la lista con los 7 pasos hechos", diapositiva() == "8" and hechos() == ",".join(["true"] * 7))
    comprobar("y el resumen «Acción · Te cuesta · Te da» de las 5 acciones",
              all(p in texto("cj-resumen") for p in ("Te cuesta", "Te da", "Retrasar", "Trampa", "Cálculo", "Ecuación", "Rescate")))
    comprobar("y el «OJO»: más rápido, 15 s para volver, si cae el jugador 1 termina",
              all(p in texto("cj-diapositiva-8") for p in ("más rápido", "15 s", "termina")))
    comprobar("en la última, «Siguiente» cambia por «Entendido»",
              js("return document.getElementById('cj-siguiente').hidden && !document.getElementById('cj-entendido').hidden;"))
    clic("cj-entendido")
    comprobar("«Entendido» la cierra y deja el menú", not abierto() and menu())
    js("document.getElementById('menu-como-jugar').click();")
    comprobar("al volver a abrir empieza en la 1", abierto() and diapositiva() == "1")
    for _ in range(7):
        tecla("ArrowRight")
    comprobar("y conserva lo practicado", hechos() == ",".join(["true"] * 7))
    clic("cj-cerrar")
    comprobar("«Cerrar ×» la cierra", not abierto() and menu())


async def principal(captura):
    global ORDENES, ORDENES_AVISO
    ORDENES = asyncio.Queue()
    ORDENES_AVISO = asyncio.Queue()
    web = levantar_web()
    unity = await websockets.serve(servidor_falso, "127.0.0.1", PUERTO_UNITY)
    navegador = None
    try:
        navegador = await asyncio.get_running_loop().run_in_executor(None, Navegador)
        # Sin «auto=1»: la página abre en el menú, como la tablet (2026-11-17).
        url = f"http://127.0.0.1:{PUERTO_WEB}/operador.html?host=127.0.0.1&puerto={PUERTO_UNITY}"
        await asyncio.get_running_loop().run_in_executor(None, navegador.abrir, url)

        def esperar(condicion, segundos=10.0):
            return asyncio.get_running_loop().run_in_executor(None, navegador.esperar, condicion, segundos)

        # --- Menú del jugador 2 ---
        menu = lambda: navegador.ejecutar("var m=document.getElementById('menu');return !!m && !m.hidden;")
        abierto = lambda: navegador.ejecutar("var c=document.getElementById('como-jugar');return !!c && !c.hidden;")
        opciones = lambda: navegador.ejecutar("var o=document.getElementById('menu-panel-opciones');return !!o && !o.hidden;")
        comprobar("la página abre en el menú, sin conectarse sola",
                  await esperar(menu, 5.0) and "Desconectado" in (navegador.texto("estado-conexion") or ""))
        comprobar("el menú tiene Cómo jugar, Entrar a la sala de control, Opciones y Salir, en ese orden",
                  navegador.ejecutar("return Array.from(document.querySelectorAll('#menu .menu-opciones > button'))"
                                     ".map(function (b) { return b.textContent.trim(); }).join('|');")
                  == "Cómo jugar|Entrar a la sala de control|Opciones|Salir")
        comprobar("«Cómo jugar» no se abre solo: está en el menú", not abierto())

        # --- Nadie empieza con el otro en el tutorial (2026-11-22) ---
        aviso_visible = lambda: navegador.ejecutar(
            "var a=document.getElementById('aviso-companero');return !!a && !a.hidden;")
        comprobar("desde el menú, sin entrar a la sala, la página abre su canal de aviso (pagina_j2)",
                  await esperar(lambda: "pagina_j2" in AVISOS_J2, 5.0))
        comprobar("y eso no la conecta a la sala (sigue Desconectado)",
                  "Desconectado" in (navegador.texto("estado-conexion") or ""))
        comprobar("sin nadie en el tutorial no hay aviso del compañero", not aviso_visible())
        navegador.ejecutar("document.getElementById('menu-como-jugar').click();")
        comprobar("abrir «Cómo jugar» lo avisa por el canal (abierto: true)",
                  await esperar(lambda: AVISOS_J2[-1] is True, 3.0))
        ORDENES_AVISO.put_nowait({"type": "jugador1_quiere_empezar"})
        comprobar("si el jugador 1 quiere empezar, aparece el aviso encima de «Cómo jugar»",
                  await esperar(lambda: aviso_visible() and
                                "QUIERE EMPEZAR" in (navegador.texto("aviso-companero") or "").upper(), 3.0))
        comprobar("y dice que cierre «Cómo jugar»", "cierra" in (navegador.texto("aviso-companero") or "").lower())
        # El aviso no recibe clics (pointer-events: none), así que elementFromPoint no lo ve:
        # se mira que vaya encima (z-index) y que el diálogo se corra para no quedar debajo.
        comprobar("el aviso va encima del diálogo y la caja de «Cómo jugar» se corre para no quedar debajo",
                  navegador.ejecutar(
                      "var a=document.getElementById('aviso-companero');"
                      "var z=+getComputedStyle(a).zIndex, zd=+getComputedStyle(document.getElementById('como-jugar')).zIndex;"
                      "var caja=document.querySelector('#como-jugar .cj-caja').getBoundingClientRect();"
                      "return z>zd && a.getBoundingClientRect().bottom<=caja.top+1;"))
        comprobar("y no tapa el botón «Cerrar ×» del diálogo", navegador.ejecutar(
            "var b=document.getElementById('cj-cerrar').getBoundingClientRect();"
            "var e=document.elementFromPoint(b.left+b.width/2,b.top+b.height/2);"
            "return !!e && !!e.closest('#cj-cerrar');"))
        navegador.ejecutar("document.getElementById('cj-cerrar').click();")
        comprobar("cerrar «Cómo jugar» lo avisa (abierto: false)", await esperar(lambda: AVISOS_J2[-1] is False, 3.0))
        comprobar("y el aviso de «quiere empezar» se va", await esperar(lambda: not aviso_visible(), 2.0))
        ORDENES_AVISO.put_nowait({"type": "estado_tutoriales", "jugador1_en_tutorial": True,
                                  "jugador2_en_como_jugar": False})
        comprobar("jugador 1 en el tutorial: el menú del J2 lo avisa",
                  await esperar(lambda: aviso_visible() and
                                "TUTORIAL" in (navegador.texto("aviso-companero") or "").upper(), 3.0))
        comprobar("y dice que la partida empieza cuando termine",
                  "cuando termine" in (navegador.texto("aviso-companero") or "").lower())
        comprobar("el aviso no tapa los botones del menú", navegador.ejecutar(
            "var b=document.getElementById('menu-como-jugar').getBoundingClientRect();"
            "var e=document.elementFromPoint(b.left+b.width/2,b.top+b.height/2);"
            "return !!e && !!e.closest('#menu-como-jugar');"))
        await probar_como_jugar(navegador, esperar, abierto, menu)
        comprobar("las opciones (IP y puerto del juego) empiezan ocultas", not opciones())
        navegador.ejecutar("document.getElementById('menu-opciones').click();")
        comprobar("«Opciones» muestra la IP y el puerto del juego, con lo que vino en la dirección",
                  opciones() and navegador.ejecutar("return document.getElementById('host').value;") == "127.0.0.1"
                  and navegador.ejecutar("return document.getElementById('puerto').value;") == str(PUERTO_UNITY))
        navegador.ejecutar("window.__cerrar = 0; window.close = function () { window.__cerrar++; };"
                           "document.getElementById('menu-salir').click();")
        comprobar("«Salir» intenta cerrar la pestaña y, si el navegador no deja, dice que se puede cerrar",
                  navegador.ejecutar("return window.__cerrar;") == 1
                  and await esperar(lambda: "cerrar esta pestaña" in (navegador.texto("menu-nota") or ""), 2.0))
        navegador.ejecutar("document.getElementById('menu-entrar').click();")
        comprobar("«Entrar a la sala de control» cierra el menú", not menu())
        comprobar("ya en la sala, sigue el aviso de que el jugador 1 está en el tutorial",
                  aviso_visible() and "TUTORIAL" in (navegador.texto("aviso-companero") or "").upper())
        ORDENES_AVISO.put_nowait({"type": "estado_tutoriales", "jugador1_en_tutorial": False,
                                  "jugador2_en_como_jugar": False})
        comprobar("cuando el jugador 1 sale del tutorial, el aviso se va",
                  await esperar(lambda: not aviso_visible(), 3.0))

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

        # --- «? Cómo jugar» en la barra, ya dentro de la sala ---
        navegador.ejecutar("document.getElementById('ayuda').click();")
        comprobar("el botón «? Cómo jugar» de la barra lo abre durante la partida", abierto() and not menu())
        navegador.ejecutar("document.dispatchEvent(new KeyboardEvent('keydown', {key: 'Escape'}));")
        comprobar("Escape lo cierra", not abierto())
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

        # --- ductos: copia del semáforo del jugador 1, sus compuertas y la batería ---
        ORDENES.put_nowait(("raw", {"type": "estado_oficina", "bateria": 76, "compuerta_izquierda": True,
                                    "compuerta_derecha": False, "luz_izquierda": "rojo", "luz_derecha": "verde"}))
        comprobar("el panel del jugador 1 muestra el ducto izquierdo en rojo y con la compuerta cerrada",
                  await esperar(lambda: (navegador.texto("j1-ducto-izquierdo") or "") == "Rojo · compuerta cerrada", 4.0))
        comprobar("el derecho en verde y abierto", (navegador.texto("j1-ducto-derecho") or "") == "Verde · abierta")
        comprobar("y la batería de las compuertas", (navegador.texto("j1-bateria") or "") == "76 %")
        comprobar("la luz también se ve como color (punto rojo junto al texto)",
                  navegador.ejecutar("return document.getElementById('j1-ducto-izquierdo').dataset.luz === 'rojo';"))
        habilitado_ = lambda i: ("var b=document.getElementById('" + i + "');return !!b && !b.disabled;")
        ORDENES.put_nowait(("raw", {"type": "estado_operador", "cargas": 3, "max_cargas": 3, "trampas": [], "progreso": 0,
                                    "necesario": 1, "habilidad_puppet": False, "bloqueo_s": 0, "ventana_rescate_s": 0,
                                    "multa_factor": 1}))
        comprobar("con las cargas llenas pero batería gastada, Recargar sigue habilitado (también recarga batería)",
                  await esperar(lambda: (navegador.texto("cargas") or "").startswith("3") and navegador.ejecutar(habilitado_("boton-minitarea")), 4.0))
        ORDENES.put_nowait(("raw", {"type": "estado_oficina", "bateria": 100, "compuerta_izquierda": False,
                                    "compuerta_derecha": False, "luz_izquierda": "verde", "luz_derecha": "verde"}))
        comprobar("con cargas y batería llenas, Recargar se deshabilita",
                  await esperar(lambda: not navegador.ejecutar(habilitado_("boton-minitarea")), 4.0))

        # --- terminal: un cálculo da carga y una ecuación da el rescate (Unity inventa y valida) ---
        ORDENES.put_nowait(("raw", {"type": "estado_operador", "cargas": 1, "max_cargas": 3, "trampas": [], "progreso": 0,
                                    "necesario": 1, "habilidad_puppet": False, "bloqueo_s": 0, "ventana_rescate_s": 0,
                                    "multa_factor": 1}))
        ORDENES.put_nowait(("raw", {"type": "estado_oficina", "bateria": 60, "compuerta_izquierda": False,
                                    "compuerta_derecha": False, "luz_izquierda": "verde", "luz_derecha": "verde"}))
        visible_terminal = "var m=document.getElementById('minitarea');return !!m && !m.hidden;"
        oculta_terminal = "var m=document.getElementById('minitarea');return !!m && m.hidden;"
        habilitado = lambda i: ("var b=document.getElementById('" + i + "');return !!b && !b.disabled;")
        comprobar("con cargas por debajo del máximo, Recargar está habilitado",
                  await esperar(lambda: navegador.ejecutar(habilitado("boton-minitarea")), 4.0))
        comprobar("sin el rescate, Desbloquear rescate está habilitado",
                  await esperar(lambda: navegador.ejecutar(habilitado("boton-ecuacion")), 4.0))
        comprobar("y el panel dice que el rescate se gana con una ecuación",
                  (navegador.texto("habilidad") or "") == "Resuelve la ecuación"
                  and "ecuación" in (navegador.texto("panel-terminal") or "")
                  and "circuito" not in (navegador.texto("panel-terminal") or "").lower())
        navegador.ejecutar("document.getElementById('boton-minitarea').click();")
        comprobar("Recargar pide un cálculo a Unity", await esperar(lambda: "tarea_pedir" in RECIBIDOS_POR_UNITY, 3.0))
        comprobar("la terminal muestra el problema que inventó Unity",
                  await esperar(lambda: navegador.ejecutar(visible_terminal) and
                                (navegador.texto("mt-enunciado") or "") == "Calcula 2 + 3", 3.0))
        comprobar("un cálculo muestra la casilla de respuesta",
                  navegador.ejecutar("return !document.getElementById('mt-formulario').hidden;"))
        comprobar("y dice para qué sirve: una carga de trampa",
                  "carga de trampa" in (navegador.texto("mt-porque") or ""))
        comprobar("ya no queda nada del circuito lógico (ni interruptores ni puertas)",
                  navegador.ejecutar("return !document.getElementById('mt-circuito') && !document.getElementById('mt-interruptores');"))
        responder = lambda texto: navegador.ejecutar(
            "var i=document.getElementById('mt-respuesta');i.value=arguments[0];"
            "document.getElementById('mt-formulario').requestSubmit();", [texto])
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
                                "Resolver cálculo" in (navegador.texto("boton-minitarea") or ""), 6.0))
        navegador.ejecutar("document.getElementById('boton-minitarea').click();")
        await esperar(lambda: navegador.ejecutar(visible_terminal), 3.0)
        responder("5")
        comprobar("la respuesta correcta llega a Unity",
                  await esperar(lambda: len(RESPUESTAS_DE_TAREA) == 2 and RESPUESTAS_DE_TAREA[1].get("valor") == 5, 3.0))
        comprobar("el aviso dice lo que ganó: carga de trampa y batería",
                  await esperar(lambda: (navegador.texto("aviso") or "") == "Correcto: +1 carga de trampa y +20 % de batería", 3.0))
        comprobar("y la terminal se cierra sin bloquearse",
                  await esperar(lambda: navegador.ejecutar(oculta_terminal) and
                                navegador.ejecutar(habilitado("boton-minitarea")), 3.0))

        navegador.ejecutar("document.getElementById('boton-ecuacion').click();")
        comprobar("Desbloquear rescate muestra la ecuación que inventó Unity, con la misma casilla de respuesta",
                  await esperar(lambda: navegador.ejecutar(visible_terminal) and
                                (navegador.texto("mt-enunciado") or "") == "Encuentra x: 2x + 3 = 11" and
                                navegador.ejecutar("return !document.getElementById('mt-formulario').hidden;"), 3.0))
        comprobar("el título dice que es el rescate de Puppet",
                  "rescate de Puppet" in (navegador.texto("mt-titulo") or ""))
        comprobar("y explica para qué sirve: reiniciar la caja de música y salvar al jugador 1",
                  "caja de música de Puppet" in (navegador.texto("mt-porque") or "")
                  and "jugador 1" in (navegador.texto("mt-porque") or ""))
        comprobar("la casilla empieza vacía (no arrastra la respuesta del cálculo)",
                  navegador.ejecutar("return document.getElementById('mt-respuesta').value === '';"))
        if os.environ.get("CAPTURA_TERMINAL"):
            await asyncio.sleep(0.6)             # que termine el fundido de entrada de la ventana
            navegador.captura(os.environ["CAPTURA_TERMINAL"] + "/terminal_ecuacion.png")
        responder("4")
        comprobar("Enviar manda la x a Unity",
                  await esperar(lambda: len(RESPUESTAS_DE_TAREA) == 3 and RESPUESTAS_DE_TAREA[2].get("valor") == 4
                                and RESPUESTAS_DE_TAREA[2].get("id") == 2, 3.0))
        comprobar("y la terminal se cierra al resolver la ecuación", await esperar(lambda: navegador.ejecutar(oculta_terminal), 3.0))
        comprobar("avisando que el rescate quedó desbloqueado",
                  await esperar(lambda: "Ecuación resuelta" in (navegador.texto("aviso") or ""), 3.0))

        # --- acciones ordenadas por lo que hacen y cupo de cálculos por noche (2026-11-22) ---
        titulos = navegador.ejecutar("return Array.from(document.querySelectorAll('.lateral .panel h2'))"
                                     ".map(function (h) { return h.textContent.trim(); }).join('|');") or ""
        comprobar(f"la sala agrupa las acciones en Frenar, Terminal y Salvar a Puppet ({titulos})",
                  all(t in titulos for t in ("Frenar", "Terminal", "Salvar a Puppet")))
        dentro = lambda hijo, panel: navegador.ejecutar(
            "var h=document.getElementById(arguments[0]);return !!h && !!h.closest('#' + arguments[1]);", [hijo, panel])
        comprobar("Retrasar y Colocar trampa están en Frenar",
                  dentro("lista-amenazas", "panel-frenar") and dentro("boton-trampa", "panel-frenar") and dentro("cargas", "panel-frenar"))
        comprobar("el cálculo y la ecuación están en Terminal",
                  dentro("boton-minitarea", "panel-terminal") and dentro("boton-ecuacion", "panel-terminal") and dentro("calculos", "panel-terminal"))
        comprobar("el rescate está en Salvar a Puppet", dentro("boton-rescate", "panel-puppet") and dentro("habilidad", "panel-puppet"))
        comprobar("la terminal dice qué da un cálculo (+1 trampa y +20 % de batería)",
                  all(p in (navegador.texto("nota-calculo") or "") for p in ("+1 trampa", "+20 %")))
        ORDENES.put_nowait(("raw", {"type": "estado_operador", "cargas": 1, "max_cargas": 3, "trampas": [], "progreso": 1,
                                    "necesario": 1, "habilidad_puppet": True, "bloqueo_s": 0, "ventana_rescate_s": 0,
                                    "multa_factor": 1, "calculos_restantes": 4, "calculos_de_la_noche": 6}))
        ORDENES.put_nowait(("raw", {"type": "estado_oficina", "bateria": 10, "compuerta_izquierda": False,
                                    "compuerta_derecha": False, "luz_izquierda": "verde", "luz_derecha": "verde"}))
        comprobar("la terminal dice cuántos cálculos quedan esta noche (4 / 6)",
                  await esperar(lambda: (navegador.texto("calculos") or "") == "4 / 6", 4.0))
        comprobar("con cupo, el botón dice «Resolver cálculo» y se puede usar",
                  (navegador.texto("boton-minitarea") or "") == "Resolver cálculo" and navegador.ejecutar(habilitado("boton-minitarea")))
        ORDENES.put_nowait(("raw", {"type": "estado_operador", "cargas": 0, "max_cargas": 3, "trampas": [], "progreso": 1,
                                    "necesario": 1, "habilidad_puppet": True, "bloqueo_s": 0, "ventana_rescate_s": 0,
                                    "multa_factor": 1, "calculos_restantes": 0, "calculos_de_la_noche": 6}))
        comprobar("sin cupo, el botón dice «Sin cálculos esta noche» y no se usa aunque falten batería y trampas",
                  await esperar(lambda: (navegador.texto("boton-minitarea") or "") == "Sin cálculos esta noche"
                                and not navegador.ejecutar(habilitado("boton-minitarea")), 4.0))
        comprobar("y el contador queda en 0 / 6", (navegador.texto("calculos") or "") == "0 / 6")
        ORDENES.put_nowait(("raw", {"type": "accion_rechazada", "objetivo": "tarea", "motivo": "sin_calculos"}))
        comprobar("si Unity rechaza un cálculo por cupo, se explica",
                  await esperar(lambda: "cálculos esta noche" in (navegador.texto("aviso") or ""), 3.0))
        ORDENES.put_nowait(("raw", {"type": "estado_operador", "cargas": 1, "max_cargas": 3, "trampas": [], "progreso": 0,
                                    "necesario": 1, "habilidad_puppet": False, "bloqueo_s": 0, "ventana_rescate_s": 0,
                                    "multa_factor": 1, "calculos_restantes": 3, "calculos_de_la_noche": 3}))
        comprobar("al empezar otra noche (cupo 3) el cálculo vuelve a estar disponible",
                  await esperar(lambda: (navegador.texto("calculos") or "") == "3 / 3"
                                and navegador.ejecutar(habilitado("boton-minitarea")), 4.0))

        # --- sala inmersiva (2026-11-24): teclas, sonidos, borde rojo y animaciones ---
        js = lambda codigo: navegador.ejecutar(codigo)

        def tecla(key, **extra):
            """Pulsa una tecla sobre lo que tenga el foco, como haría el teclado."""
            return navegador.ejecutar(
                "var o=arguments[1]||{};o.key=arguments[0];o.bubbles=true;o.cancelable=true;"
                "var t=document.activeElement||document.body;t.dispatchEvent(new KeyboardEvent('keydown',o));"
                "return t.id||t.tagName;", [key, extra])

        sonidos = lambda: navegador.ejecutar("return window.Sonidos ? Sonidos.historial() : [];")
        nivel = lambda: navegador.ejecutar("return document.getElementById('borde-peligro') ? "
                                           "document.getElementById('borde-peligro').dataset.nivel || '' : null;")
        etiquetas = {"boton-freddy": "1", "boton-vixy": "2", "boton-puppet": "3", "boton-trampa": "T",
                     "boton-minitarea": "C", "boton-ecuacion": "E", "boton-rescate": "R"}
        comprobar("cada botón muestra su tecla y la anuncia (aria-keyshortcuts)", all(navegador.ejecutar(
            "var b=document.getElementById(arguments[0]);if(!b)return false;"
            "return getComputedStyle(b,'::before').content.indexOf(arguments[1])>=0"
            " && b.getAttribute('aria-keyshortcuts')===arguments[1];", [boton, key])
            for boton, key in etiquetas.items()))
        comprobar("el texto de los botones no cambia por la etiqueta de la tecla",
                  (navegador.texto("boton-freddy") or "") == "Retrasar"
                  and (navegador.texto("boton-minitarea") or "") == "Resolver cálculo")

        ORDENES.put_nowait(("raw", {"type": "estado_mapa", "partida_activa": True, "noche": 3, "hora": "1:00 AM",
                                    "animatronicos": [{"nombre": n, "x": 1.0, "z": 1.0, "retraso_s": 0, "espera_s": 0}
                                                      for n in ("freddy", "vixy", "puppet")],
                                    "timestamp": int(time.time() * 1000)}))
        ORDENES.put_nowait(("raw", {"type": "estado_operador", "cargas": 2, "max_cargas": 3, "trampas": [],
                                    "progreso": 0, "necesario": 1, "habilidad_puppet": True, "bloqueo_s": 0,
                                    "ventana_rescate_s": 0, "multa_factor": 1, "calculos_restantes": 3,
                                    "calculos_de_la_noche": 3}))
        await esperar(lambda: js("return !document.getElementById('boton-freddy').disabled;"), 3.0)
        js("if (document.activeElement) document.activeElement.blur();")
        antes = len(ACCIONES_RECIBIDAS)
        pulsado = navegador.ejecutar(
            "document.body.dispatchEvent(new KeyboardEvent('keydown',{key:'1',bubbles:true,cancelable:true}));"
            "return document.getElementById('boton-freddy').classList.contains('pulsado');")
        comprobar("la tecla 1 retrasa a Freddy", await esperar(
            lambda: len(ACCIONES_RECIBIDAS) > antes and ACCIONES_RECIBIDAS[-1].get("accion") == "retrasar"
            and ACCIONES_RECIBIDAS[-1].get("objetivo") == "freddy", 2.0))
        comprobar("y el botón se ve hundido, como si lo hubieras pulsado", pulsado)
        antes = len(ACCIONES_RECIBIDAS)
        tecla("2", ctrlKey=True)
        tecla("3", repeat=True)
        await asyncio.sleep(0.6)
        comprobar("Ctrl+2 y una tecla mantenida no mandan nada", len(ACCIONES_RECIBIDAS) == antes)

        pedidos = RECIBIDOS_POR_UNITY.count("tarea_pedir")
        tecla("c")
        comprobar("la tecla C abre la terminal y pide un cálculo",
                  await esperar(lambda: js(visible_terminal) and RECIBIDOS_POR_UNITY.count("tarea_pedir") > pedidos, 3.0))
        await esperar(lambda: js("return document.activeElement && document.activeElement.id==='mt-respuesta';"), 3.0)
        antes = len(ACCIONES_RECIBIDAS)
        donde = tecla("1")
        await asyncio.sleep(0.6)
        comprobar(f"escribir 1 en la respuesta no retrasa a nadie (tecla en {donde})",
                  donde == "mt-respuesta" and len(ACCIONES_RECIBIDAS) == antes)
        tecla("Escape")
        comprobar("Esc cierra la terminal", await esperar(lambda: js(oculta_terminal), 2.0))

        js("if (document.activeElement) document.activeElement.blur();")
        tecla("t")
        comprobar("la tecla T entra al modo trampa", js("return document.getElementById('mapa').dataset.modo==='trampa';"))
        tecla("T")
        comprobar("y otra T sale", js("return document.getElementById('mapa').dataset.modo!=='trampa';"))

        js("document.getElementById('ayuda').click();")
        antes = len(ACCIONES_RECIBIDAS)
        tecla("2")
        await asyncio.sleep(0.6)
        comprobar("con «Cómo jugar» abierto las teclas no hacen nada", abierto() and len(ACCIONES_RECIBIDAS) == antes)
        tecla("Escape")
        comprobar("«Cómo jugar» dice cuáles son las teclas",
                  "1 2 3" in (js("return document.getElementById('cj-diapositiva-8').textContent;") or ""))

        # Borde rojo: Puppet (en el Unity falso) está a 3,4 m del jugador 1 → alerta.
        comprobar("con un animatrónico a menos de 4 m, el borde se pone rojo suave (alerta)",
                  await esperar(lambda: nivel() == "alerta", 3.0))
        comprobar("el borde rojo no tapa clics y queda debajo del susto",
                  js("var b=document.getElementById('borde-peligro'),s=document.getElementById('susto');"
                     "return getComputedStyle(b).pointerEvents==='none'"
                     " && parseInt(getComputedStyle(b).zIndex) < parseInt(getComputedStyle(s).zIndex);"))
        ORDENES.put_nowait(("raw", {"type": "estado_operador", "cargas": 2, "max_cargas": 3, "trampas": [],
                                    "progreso": 1, "necesario": 1, "habilidad_puppet": True, "bloqueo_s": 0,
                                    "ventana_rescate_s": 2.5, "multa_factor": 1, "calculos_restantes": 3,
                                    "calculos_de_la_noche": 3}))
        comprobar("si Puppet sale de la caja, el borde late en rojo fuerte (peligro)",
                  await esperar(lambda: nivel() == "peligro", 2.0))
        comprobar("y suena la alarma", await esperar(lambda: "alarma" in sonidos(), 2.0))
        js("if (document.activeElement) document.activeElement.blur();")
        await esperar(lambda: js("return !document.getElementById('boton-rescate').disabled;"), 2.0)
        antes = len(ACCIONES_RECIBIDAS)
        tecla("r")
        comprobar("la tecla R rescata a Puppet", await esperar(
            lambda: len(ACCIONES_RECIBIDAS) > antes and ACCIONES_RECIBIDAS[-1].get("accion") == "rescatar_puppet", 2.0))
        comprobar("al cerrarse la ventana de rescate vuelve a alerta",
                  await esperar(lambda: nivel() == "alerta", 5.0))

        # Animaciones y sonidos que responden a lo que confirma Unity.
        cuantos = len(sonidos())
        ORDENES.put_nowait(("raw", {"type": "efecto", "animatronic": "freddy", "tipo": "ralentizado",
                                    "segundos": 8, "message": "Tu compañero retrasó a Freddy", "id": 0}))
        comprobar("al confirmar el retraso, la fila de Freddy destella",
                  await esperar(lambda: js("return document.getElementById('amenaza-freddy').classList.contains('destello');"), 2.0))
        comprobar("y su punto del mapa lanza una onda",
                  await esperar(lambda: int(js("return document.getElementById('mapa').dataset.ondas||'0';")) > 0, 2.0))
        comprobar("y suena el bip de acción", "accion" in sonidos()[cuantos:])
        ORDENES.put_nowait(("raw", {"type": "tarea_resultado", "id": 1, "tipo": "calculo", "correcto": True, "bloqueo_s": 0}))
        comprobar("al acertar un cálculo el contador de trampas da un salto y suena el acierto",
                  await esperar(lambda: js("return document.getElementById('cargas').classList.contains('salto');")
                                and "acierto" in sonidos(), 2.0))
        ORDENES.put_nowait(("raw", {"type": "tarea_resultado", "id": 1, "tipo": "calculo", "correcto": False, "bloqueo_s": 0}))
        comprobar("al fallar, la terminal tiembla y suena el error",
                  await esperar(lambda: js("return document.getElementById('panel-terminal').classList.contains('temblor');")
                                and "error" in sonidos(), 2.0))
        js("var c=document.getElementById('sonidos');c.checked=false;c.dispatchEvent(new Event('change'));")
        cuantos = len(sonidos())
        ORDENES.put_nowait(("raw", {"type": "efecto", "animatronic": "vixy", "tipo": "ralentizado",
                                    "segundos": 8, "message": "x", "id": 0}))
        await esperar(lambda: js("return document.getElementById('amenaza-vixy').classList.contains('destello');"), 2.0)
        comprobar("con «Sonidos de la sala» apagado en Opciones no suena nada",
                  len(sonidos()) == cuantos and js("return Sonidos.activo();") is False)
        js("var c=document.getElementById('sonidos');c.checked=true;c.dispatchEvent(new Event('change'));")
        errores = navegador.ejecutar("return (window.__errores || []).length;")
        comprobar(f"sin errores de JavaScript con teclas, sonidos y animaciones (errores: {errores})", errores == 0)

        # --- fin de la partida: el jugador 2 no se queda con la pantalla congelada ---
        visible = "var f=document.getElementById('fin-partida');return !!f && !f.hidden;"
        susto = "var s=document.getElementById('susto');return !!s && !s.hidden;"
        dato = lambda clave: navegador.ejecutar(
            "var s=document.getElementById('susto');return s ? (s.dataset[arguments[0]] || '') : '';", [clave])
        comprobar("la página precargó el susto (videos, imagen y grito) desde /sustos/",
                  await esperar(lambda: js("return !!window.Susto && Susto.listo();"), 8.0))
        comprobar("mientras juegan no hay susto en pantalla", not js(susto))
        ORDENES.put_nowait(("fin", "loss"))
        # --- el susto también le llega al jugador 2 (2026-11-24) ---
        comprobar("al atrapar al jugador 1, el jugador 2 ve el susto al instante (≤ 1 s)",
                  await esperar(lambda: js(susto), 1.0))
        comprobar("el susto es el de Freddy, en video", dato("animatronico") == "freddy" and dato("modo") == "video")
        cara_lista = ("var i=document.getElementById('susto-imagen');"
                      "return !i.hidden && i.complete && i.naturalWidth > 0;")
        comprobar("la cara de Freddy ya está cargada y visible desde el primer instante, debajo del video",
                  dato("imagen") == "jumpscare_freddy.jpg" and js(cara_lista))
        comprobar("el video del susto se está reproduciendo",
                  await esperar(lambda: js("var v=document.getElementById('susto-video');"
                                           "return !v.hidden && (!v.paused || v.currentTime > 0);"), 1.0))
        comprobar("suena el grito", await esperar(lambda: dato("sonido") == "sonando", 1.0))
        # Toda la ventana (innerWidth/innerHeight), también donde iría la barra de
        # desplazamiento: durante el susto la página la oculta.
        medidas = js("var r=document.getElementById('susto').getBoundingClientRect();"
                     "return [r.left,r.top,r.right,r.bottom,innerWidth,innerHeight];")
        comprobar(f"el susto tapa toda la ventana, sin barras de desplazamiento al lado (capa y ventana: {medidas})",
                  medidas[0] <= 0 and medidas[1] <= 0 and medidas[2] >= medidas[4] and medidas[3] >= medidas[5])
        comprobar("el susto va encima de la tarjeta de fin, la mini-tarea y el aviso del compañero",
                  js("var z=function(id){return parseInt(getComputedStyle(document.getElementById(id)).zIndex)||0;};"
                     "var s=z('susto');return s>z('fin-partida') && s>z('minitarea') && s>z('aviso-companero');"))
        comprobar("encima de la cara se ve el video",
                  js("var e=document.elementFromPoint(innerWidth/2, innerHeight/2);return !!e && e.id==='susto-video';"))
        comprobar("en el centro y en las esquinas lo que se ve es el susto",
                  js("var s=document.getElementById('susto');"
                     "var w=innerWidth,h=innerHeight;"
                     "return [[w/2,h/2],[2,2],[w-3,h-3],[w/2,4]]"
                     ".every(function(p){var e=document.elementFromPoint(p[0],p[1]);return !!e && s.contains(e);});"))
        comprobar("un segundo ataque con el susto en pantalla no lo repite",
                  js("return Susto.mostrar('vixy');") is False and dato("animatronico") == "freddy")
        comprobar("al terminar el susto (≤ 3 s) se ve la tarjeta de fin",
                  await esperar(lambda: not js(susto), 3.0) and js(visible))
        comprobar("y la página vuelve a poder desplazarse",
                  js("return getComputedStyle(document.documentElement).overflow !== 'hidden';"))
        comprobar("al morir el jugador 1 aparece #fin-partida",
                  await esperar(lambda: js(visible), 5.0))
        comprobar("terminada la partida, el borde rojo se apaga", await esperar(lambda: nivel() == "", 2.0))
        comprobar("el fin dice que el jugador 1 fue atrapado",
                  "ATRAPADO" in (navegador.texto("fin-detalle") or "").upper())
        comprobar("el fin muestra la duración 2:05 y las tareas",
                  "2:05" in (navegador.texto("fin-estadisticas") or "") and "7" in (navegador.texto("fin-estadisticas") or ""))
        comprobar("terminada la partida, Recargar, Desbloquear rescate y Colocar trampa quedan deshabilitados",
                  js("return document.getElementById('boton-minitarea').disabled && document.getElementById('boton-ecuacion').disabled"
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
        # Un atacante sin video (el genérico «Animatrónico») muestra la imagen fija.
        ORDENES.put_nowait(("ataque", "Animatrónico"))
        comprobar("sin video para el atacante, el susto es la imagen de respaldo general",
                  await esperar(lambda: js(susto), 1.0) and dato("modo") == "imagen"
                  and dato("imagen") == "desktop_jumpscare.jpg" and js(cara_lista)
                  and js("return document.getElementById('susto-video').hidden;"))
        comprobar("la imagen fija también se va sola (≤ 3 s)", await esperar(lambda: not js(susto), 3.0))
        # El navegador no deja sonar el grito (no hubo clic): el susto se ve igual.
        js("window.__playOriginal = HTMLMediaElement.prototype.play;"
           "HTMLMediaElement.prototype.play = function () {"
           "  if (this instanceof HTMLAudioElement) return Promise.reject(new DOMException('sin gesto', 'NotAllowedError'));"
           "  return window.__playOriginal.apply(this, arguments); };")
        ORDENES.put_nowait(("ataque", "Vixy"))
        comprobar("si el navegador bloquea el grito, el susto de Vixy se ve igual",
                  await esperar(lambda: js(susto), 1.0) and dato("animatronico") == "vixy" and dato("modo") == "video" and dato("imagen") == "jumpscare_vixy.jpg"
                  and await esperar(lambda: dato("sonido") == "bloqueado", 1.0))
        comprobar("y se va solo (≤ 3 s)", await esperar(lambda: not js(susto), 3.0))
        # Ahora es el video el que no se puede reproducir: queda la cara de ese animatrónico.
        js("HTMLMediaElement.prototype.play = function () {"
           "  if (this instanceof HTMLVideoElement) return Promise.reject(new DOMException('roto', 'NotSupportedError'));"
           "  return window.__playOriginal.apply(this, arguments); };")
        ORDENES.put_nowait(("ataque", "Puppet"))
        comprobar("si el video de Puppet no se reproduce, queda la cara de Puppet",
                  await esperar(lambda: js(susto) and dato("modo") == "imagen", 1.0)
                  and dato("imagen") == "jumpscare_puppet.jpg" and js(cara_lista)
                  and js("return document.getElementById('susto-video').hidden;"))
        comprobar("y también se va solo (≤ 3 s)", await esperar(lambda: not js(susto), 3.0))
        js("HTMLMediaElement.prototype.play = window.__playOriginal;")
        errores = navegador.ejecutar("return (window.__errores || []).length;")
        comprobar(f"sin errores de JavaScript con el susto (errores: {errores})", errores == 0)
        ORDENES.put_nowait(("fin", "win"))
        comprobar("si el jugador 1 gana, el fin dice que sobrevivió",
                  await esperar(lambda: js(visible) and "SOBREVIVI" in (navegador.texto("fin-detalle") or "").upper(), 5.0))
        comprobar("si el jugador 1 gana no hay susto", not js(susto))
        navegador.ejecutar("document.getElementById('fin-salir').click();")
        comprobar("«Salir» desconecta, oculta el fin y vuelve al menú",
                  await esperar(lambda: "Desconectado" in (navegador.texto("estado-conexion") or "")
                                and not js(visible) and menu(), 5.0))
        await asyncio.sleep(3.5)
        comprobar("tras «Salir» la página no se reconecta sola",
                  "Desconectado" in (navegador.texto("estado-conexion") or ""))
        errores = navegador.ejecutar("return (window.__errores || []).length;")
        comprobar(f"sin errores de JavaScript tras el fin de la partida (errores: {errores})", errores == 0)

        # --- J1 se va / J2 se cae: avisos, 3 intentos en 15 s y rechazo ---
        navegador.ejecutar("document.getElementById('menu-entrar').click();")
        comprobar("«Entrar» del menú vuelve a conectar tras Salir",
                  await esperar(lambda: "Conectado" in (navegador.texto("estado-conexion") or "") and not menu(), 8.0))
        navegador.ejecutar("document.getElementById('volver-al-menu').click();")
        comprobar("el botón «Menú» de la sala desconecta y vuelve al menú",
                  await esperar(lambda: menu() and "Desconectado" in (navegador.texto("estado-conexion") or ""), 5.0))
        navegador.ejecutar("document.getElementById('menu-entrar').click();")
        comprobar("y desde el menú se vuelve a entrar",
                  await esperar(lambda: "Conectado" in (navegador.texto("estado-conexion") or ""), 8.0))

        # --- lo que hace el jugador 1 con la cámara se le ve al jugador 2 ---
        comprobar("el panel del jugador 1 dice hacia dónde mira su cámara",
                  await esperar(lambda: (navegador.texto("j1-camara") or "") == "Izquierda 45°", 4.0))

        # --- el jugador 1 pierde la conexión: la partida terminó y no hay vuelta (2026-11-17) ---
        ORDENES.put_nowait(("raw", {"type": "jugador1_salio", "motivo": "conexion_perdida"}))
        comprobar("si el jugador 1 pierde la conexión, la página tapa el monitor: «se desconectó»",
                  await esperar(lambda: js(visible) and "DESCONECT" in (navegador.texto("fin-titulo") or "").upper(), 4.0))
        comprobar("y ya no queda el aviso de «esperando que vuelva»",
                  navegador.ejecutar("return document.getElementById('j1-sin-conexion') === null;"))
        ORDENES.put_nowait(("raw", {"type": "game_over", "result": "conexion_perdida", "night": 2,
                                    "final_stats": {"duration": 95, "tasks_completed": 3, "tasks_failed": 0}}))
        comprobar("el game_over de Unity («conexion_perdida») lo explica en el detalle",
                  await esperar(lambda: js(visible) and "DESCONECT" in (navegador.texto("fin-detalle") or "").upper(), 4.0))
        navegador.ejecutar("document.getElementById('fin-esperar').click();")

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
        unity = await websockets.serve(servidor_falso, "127.0.0.1", PUERTO_UNITY)
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
        unity = await websockets.serve(servidor_falso, "127.0.0.1", PUERTO_UNITY)
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
