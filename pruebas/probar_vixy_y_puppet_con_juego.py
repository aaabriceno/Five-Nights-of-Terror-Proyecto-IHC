"""Prueba con el juego REAL exportado: Vixy y Puppet caminando sus rutas y el rescate de Puppet.

Tres escenarios, cada uno con su propio arranque del juego:
  vixy     Vixy recorre su ruta fija (con tareas cada 1 s para que se mueva, y sin Freddy).
  rescate  La caja dura 30 s; cada rescate del jugador 2 la deja en la mitad (15 s, 7,5 s).
  puppet   Puppet sale de la caja y recorre su ruta fija hasta el jugador 1.
  tablet   La tablet se cae con la cuerda de la caja y la cámara apretadas: Unity las suelta;
           al volver con `reconnect` la noche sigue (no se reinicia).

El juego se abre en una pantalla virtual (Xvfb) para no tocar tu escritorio. Usa las
opciones de prueba del juego (-prueba-sin-freddy, -prueba-tareas-cada): sin ellas el
juego es el de siempre. Hay que exportar antes el juego (./exportar_linux.sh) y no
tener otro relay en el puerto 8000.

Uso: UnityRelay/.venv/bin/python pruebas/probar_vixy_y_puppet_con_juego.py [vixy|rescate|puppet ...]
"""
import asyncio
import json
import math
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

import websockets

sys.path.insert(0, str(Path(__file__).resolve().parent))
import solucionador_de_tareas as solucion  # noqa: E402

RAIZ = Path(__file__).resolve().parent.parent
URL = "ws://127.0.0.1:8000"
JUEGO = RAIZ / "Unity/Builds/Linux/FiveNightsOfTerror.x86_64"
PANTALLA = ":98"
# El animatrónico se detiene a ~1 m del punto (su distancia de parada): se da por llegado a 1,2 m.
RADIO_DE_LLEGADA_M = 1.2
fallos = 0


def comprobar(descripcion, condicion):
    global fallos
    print(("  ok    " if condicion else "  FALLA ") + descripcion, flush=True)
    if not condicion:
        fallos += 1


class Partida:
    """Arranca el relay y el juego, hace de tablet y de operador, y apunta todo lo que llega."""

    def __init__(self, opciones):
        self.opciones = opciones
        self.procesos = []
        self.t0 = None
        self.nodos = {}
        self.rutas = {}
        self.posiciones = []            # (t, {nombre: (x, z)})
        self.caja = []                  # (t, porcentaje, segundos_caja, multa)
        self.estado_operador = []       # (t, mensaje)
        self.tareas = []                # (t, mensaje) de las tareas de la terminal que entrega Unity
        self.efectos = []               # (t, mensaje)
        self.fin = None                 # (t, mensaje) de game_over
        self.estado_jugador = []        # (t, mensaje)
        self.yaw = []                   # (t, camara_yaw)
        self.avisos = []                # (t, tipo) de jugador1_salio / jugador1_volvio
        self.visitas = {"freddy": [], "vixy": [], "puppet": []}

    def t(self):
        return round(time.time() - self.t0, 1) if self.t0 else 0.0

    async def abrir(self):
        entorno = dict(os.environ)
        if shutil.which("Xvfb"):
            self.procesos.append(subprocess.Popen(["Xvfb", PANTALLA, "-screen", "0", "1280x720x24"],
                                                  stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
            entorno["DISPLAY"] = PANTALLA
            await asyncio.sleep(1.5)
        self.procesos.append(subprocess.Popen([sys.executable, "-u", str(RAIZ / "UnityRelay/relay.py")],
                                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
        await asyncio.sleep(1.0)
        self.procesos.append(subprocess.Popen([str(JUEGO), "-screen-fullscreen", "0", "-screen-width", "1280",
                                               "-screen-height", "720", *self.opciones],
                                              env=entorno, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
        await asyncio.sleep(12.0)       # el juego tarda en abrir y conectarse al relay
        self.tablet = await websockets.connect(URL, ping_interval=None, max_queue=None)
        self.operador = await websockets.connect(URL, ping_interval=None, max_queue=None)
        await self.operador.send(json.dumps({"type": "operador_connect"}))
        while True:
            m = json.loads(await asyncio.wait_for(self.operador.recv(), 10))
            if m["type"] == "mapa_puntos":
                self.nodos = {p["nombre"]: (p["x"], p["z"]) for p in m["puntos"]}
                self.rutas = {r["animatronico"]: r["puntos"] for r in m.get("rutas", [])}
                break
        self.lectores = [asyncio.ensure_future(self._leer_operador()), asyncio.ensure_future(self._leer_tablet()),
                         asyncio.ensure_future(self._latidos())]
        await self.tablet.send(json.dumps({"type": "connect", "device": "tablet", "player_id": "prueba_vp", "modo": "nuevo"}))
        self.t0 = time.time()

    async def _latidos(self):
        while True:
            await self.operador.send(json.dumps({"type": "ping_operador", "t": 1}))
            await asyncio.sleep(2.0)

    async def _leer_operador(self):
        try:
            async for crudo in self.operador:
                m = json.loads(crudo)
                tipo, t = m.get("type"), self.t()
                if tipo == "posiciones":
                    if "camara_yaw" in m:
                        self.yaw.append((t, m["camara_yaw"]))
                    actuales = {a["nombre"]: (a["x"], a["z"]) for a in m["animatronicos"]}
                    self.posiciones.append((t, actuales))
                    for nombre, (x, z) in actuales.items():
                        for nodo, (nx, nz) in self.nodos.items():
                            if math.hypot(x - nx, z - nz) <= RADIO_DE_LLEGADA_M and (
                                    not self.visitas[nombre] or self.visitas[nombre][-1][0] != nodo):
                                self.visitas[nombre].append((nodo, t))
                elif tipo == "estado_operador":
                    self.estado_operador.append((t, m))
                elif tipo == "tarea":
                    self.tareas.append((t, m))
                elif tipo == "estado_jugador":
                    self.estado_jugador.append((t, m))
                elif tipo in ("jugador1_salio", "jugador1_volvio"):
                    self.avisos.append((t, tipo))
                elif tipo == "efecto":
                    self.efectos.append((t, m))
                elif tipo == "game_over":
                    self.fin = (t, m)
        except websockets.exceptions.ConnectionClosed:
            pass

    async def _leer_tablet(self):
        try:
            async for crudo in self.tablet:
                m = json.loads(crudo)
                if m.get("type") == "estado_puppet":
                    self.caja.append((self.t(), m["valor_caja_porcentaje"], m.get("segundos_caja"), m.get("multa_factor")))
                elif m.get("type") == "game_over" and self.fin is None:
                    self.fin = (self.t(), m)
        except websockets.exceptions.ConnectionClosed:
            pass

    async def esperar(self, condicion, limite_s, cada=0.1):
        fin = time.time() + limite_s
        while time.time() < fin:
            if condicion():
                return True
            if self.fin is not None:
                return condicion()
            await asyncio.sleep(cada)
        return condicion()

    async def _resolver_tarea(self, tipo, espera_minima):
        """Pide una tarea de la terminal, espera lo mínimo que exige Unity y la responde bien."""
        antes = len(self.tareas)
        await self.operador.send(json.dumps({"type": "tarea_pedir", "tipo": tipo}))
        if not await self.esperar(lambda: len(self.tareas) > antes, 3):
            return False
        tarea = self.tareas[-1][1]
        await asyncio.sleep(espera_minima)
        await self.operador.send(json.dumps(solucion.respuesta_para(tarea)))
        return True

    async def ganar_carga(self):
        """Un cálculo bien resuelto: 1 carga de trampa."""
        return await self._resolver_tarea("calculo", 1.7)

    async def ganar_rescate(self):
        """Un circuito bien resuelto: desbloquea el rescate de Puppet."""
        return await self._resolver_tarea("circuito", 3.2)

    async def cerrar(self):
        for tarea in getattr(self, "lectores", []):
            tarea.cancel()
        for socket in (getattr(self, "tablet", None), getattr(self, "operador", None)):
            if socket:
                await socket.close()
        for proceso in reversed(self.procesos):
            proceso.terminate()
            try:
                proceso.wait(timeout=10)
            except subprocess.TimeoutExpired:
                proceso.kill()

    def ultima_posicion(self, nombre):
        for _, p in reversed(self.posiciones):
            if nombre in p:
                return p[nombre]
        return None

    def cruzo_la_oficina(self, nombre, hasta_t):
        """¿Pisó el animatrónico la oficina del jugador 1 antes de `hasta_t`?"""
        for t, p in self.posiciones:
            if t <= hasta_t and nombre in p:
                x, z = p[nombre]
                if 17.6 < x < 22.4 and z < 5.9:
                    return True
        return False


def en_orden(visitas, ruta, permitidos_extra=()):
    """Los nodos visitados siguen la ruta en orden, sin saltar hacia atrás (los desvíos permitidos se ignoran)."""
    ultimo = -1
    for nodo, _ in visitas:
        if nodo in permitidos_extra:
            continue
        if nodo not in ruta:
            return False
        i = ruta.index(nodo)
        if i < ultimo:
            return False
        ultimo = max(ultimo, i)
    return True


async def escenario_vixy():
    print("== Vixy recorre su ruta fija ==")
    p = Partida(["-prueba-sin-freddy", "-prueba-tareas-cada", "1"])
    try:
        await p.abrir()
        ruta = p.rutas.get("vixy", [])
        comprobar("el mapa trae la ruta de Vixy", ruta == ["P02", "P11", "P12", "V01", "V02", "V03", "V04"])
        # Hasta que llegue al punto penúltimo (V04) o termine la partida.
        llego = await p.esperar(lambda: any(n == "V04" for n, _ in p.visitas["vixy"]), 150)
        comprobar("sin Freddy: la partida no lo trae (solo Vixy y Puppet en el mapa)", p.visitas["freddy"] == [])
        nombres = [n for n, _ in p.visitas["vixy"]]
        print("   recorrido de Vixy:", " → ".join(f"{n}@{t}" for n, t in p.visitas["vixy"]))
        print("   última posición de Vixy:", p.ultima_posicion("vixy"), "| fin:", p.fin and (p.fin[0], p.fin[1].get("result")))
        comprobar("Vixy sale de P02", nombres[:1] == ["P02"])
        comprobar("Vixy llega al punto penúltimo V04", llego)
        # P08 queda a ~1 m del camino que hace por el comedor hacia P11 (no es un desvío).
        comprobar("sigue su ruta en orden (solo se permite pasar junto a P08)",
                  en_orden(p.visitas["vixy"], ruta, permitidos_extra=("P08",)))
        # Ya no tiene desvío: antes podía subir hasta P10 (arriba del todo) y bajar otra vez.
        comprobar("nunca sube a la esquina de arriba del pasillo izquierdo (P10)", "P10" not in nombres)
        comprobar("pasa por todos los puntos de la ruta: P11, P12, V01, V02 y V03",
                  all(n in nombres for n in ["P11", "P12", "V01", "V02", "V03"]))
        t_v04 = next((t for n, t in p.visitas["vixy"] if n == "V04"), p.t())
        comprobar("no pisa la oficina del jugador 1 antes de llegar a V04", not p.cruzo_la_oficina("vixy", t_v04 - 0.5))
        # Tras llegar al penúltimo, con las tareas sin resolver, termina atacando.
        await p.esperar(lambda: p.fin is not None, 40)
        comprobar("con las tareas sin resolver, Vixy termina atacando desde el ducto (game_over)",
                  p.fin is not None and p.fin[1].get("result") == "loss")
    finally:
        await p.cerrar()


async def escenario_rescate():
    print("== La caja dura 30 s y cada rescate la deja en la mitad ==")
    p = Partida(["-prueba-sin-freddy"])
    try:
        await p.abrir()
        # El jugador 2 gana la habilidad de rescatar resolviendo un circuito lógico.
        await p.ganar_rescate()
        comprobar("tras resolver un circuito el jugador 2 tiene la habilidad de rescatar",
                  await p.esperar(lambda: p.estado_operador and p.estado_operador[-1][1]["habilidad_puppet"], 3))
        t_vacia = next((t for t, pct, _, _ in p.caja if pct <= 0), None)
        if t_vacia is None:
            await p.esperar(lambda: any(pct <= 0 for _, pct, _, _ in p.caja), 40)
            t_vacia = next((t for t, pct, _, _ in p.caja if pct <= 0), None)
        comprobar(f"sin darle cuerda la caja se vacía a los 30 s (se vació a los {t_vacia} s)",
                  t_vacia is not None and 27.5 <= t_vacia <= 32.5)

        esperados = [(15.0, 2.0), (7.5, 4.0)]
        for numero, (segundos, factor) in enumerate(esperados, start=1):
            saltar_a = len(p.estado_operador)
            ventana = await p.esperar(
                lambda: any(m["ventana_rescate_s"] > 0 for _, m in p.estado_operador[saltar_a:]), 120)
            comprobar(f"rescate {numero}: Puppet sale de la caja y se abre la ventana de rescate", ventana)
            if not ventana:
                return
            antes = len(p.efectos)
            await p.operador.send(json.dumps({"type": "accion_operador", "id": 100 + numero, "accion": "rescatar_puppet"}))
            ok = await p.esperar(lambda: any(m.get("tipo") == "puppet_rescatado" for _, m in p.efectos[antes:]), 3)
            efecto = next((m for _, m in p.efectos[antes:] if m.get("tipo") == "puppet_rescatado"), None)
            comprobar(f"rescate {numero}: Unity lo acepta y avisa que la caja ahora dura {segundos} s",
                      ok and efecto["segundos_caja"] == segundos and efecto["multa_factor"] == factor)
            t_rescate = p.t()
            if numero < len(esperados):
                await p.ganar_rescate()              # gana la habilidad otra vez mientras tanto
            await p.esperar(lambda: any(t > t_rescate + 1 and pct <= 0 for t, pct, _, _ in p.caja), segundos + 6)
            t_fin = next((t for t, pct, _, _ in p.caja if t > t_rescate + 1 and pct <= 0), None)
            if t_fin is not None:
                duracion = t_fin - t_rescate
                comprobar(f"rescate {numero}: la caja vuelve a vaciarse en {segundos} s (tardó {duracion:.1f} s)",
                          abs(duracion - segundos) <= 2.0)
            else:
                comprobar(f"rescate {numero}: la caja volvió a vaciarse", False)
            ultimo = next(((s, m) for _, _, s, m in reversed(p.caja)), None)
            comprobar(f"rescate {numero}: la tablet recibe estado_puppet con segundos_caja {segundos}",
                      ultimo is not None and ultimo[0] == segundos and ultimo[1] == factor)
    finally:
        await p.cerrar()


async def escenario_puppet():
    print("== Puppet sale de la caja y recorre su ruta fija ==")
    p = Partida(["-prueba-sin-freddy"])
    try:
        await p.abrir()
        ruta = p.rutas.get("puppet", [])
        comprobar("el mapa trae la ruta de Puppet",
                  ruta == ["P13", "P14", "P15", "P16", "V05", "V06", "V07", "V08"])
        salio = await p.esperar(lambda: any(n == "P14" for n, _ in p.visitas["puppet"]), 150)
        comprobar("Puppet sale de la caja y empieza a caminar (llega a P14)", salio)
        llego = await p.esperar(lambda: any(n == "V08" for n, _ in p.visitas["puppet"]), 80)
        print("   recorrido de Puppet:", " → ".join(f"{n}@{t}" for n, t in p.visitas["puppet"]))
        nombres = [n for n, _ in p.visitas["puppet"]]
        comprobar("empieza en P13 (la caja)", nombres[:1] == ["P13"])
        comprobar("sigue su ruta en orden", en_orden(p.visitas["puppet"], ruta))
        comprobar("pasa por P14, P15, P16, V05, V06 y V07", all(n in nombres for n in ["P14", "P15", "P16", "V05", "V06", "V07"]))
        comprobar("llega a la boca del ducto derecho (V08)", llego)
        t_v08 = next((t for n, t in p.visitas["puppet"] if n == "V08"), p.t())
        comprobar("no pisa la oficina antes de llegar a V08", not p.cruzo_la_oficina("puppet", t_v08 - 0.5))
        await p.esperar(lambda: p.fin is not None, 30)
        comprobar("al llegar al jugador 1, Puppet termina la partida (game_over)", p.fin is not None and p.fin[1].get("result") == "loss")
    finally:
        await p.cerrar()


async def escenario_tablet():
    print("== La tablet se cae y vuelve: la cuerda y la cámara se sueltan y la noche sigue ==")
    p = Partida(["-prueba-sin-freddy"])
    try:
        await p.abrir()
        caja = lambda: p.estado_jugador[-1][1]["caja_pct"] if p.estado_jugador else None
        await p.esperar(lambda: p.t() >= 10, 20)
        comprobar(f"sin darle cuerda la caja baja (a los 10 s va en {caja()} %)", caja() is not None and caja() < 80)
        # El jugador 1 aprieta la cuerda y la cámara, y se queda sin Wi-Fi sin soltarlas.
        await p.tablet.send(json.dumps({"type": "dar_cuerda_inicio"}))

        async def mantener_la_camara():              # como el dedo apoyado: repite la orden hasta que se cae
            while True:
                await p.tablet.send(json.dumps({"type": "camara", "direccion": -1}))
                await asyncio.sleep(0.15)

        camara = asyncio.ensure_future(mantener_la_camara())
        await p.esperar(lambda: (caja() or 0) >= 85, 12)
        comprobar(f"dándole cuerda la caja sube (va en {caja()} %)", (caja() or 0) >= 85)
        yaw = p.yaw[-1][1] if p.yaw else 0
        comprobar(f"la cámara está girada a la izquierda (va en {yaw}°)", yaw < -30)
        caja_al_caer = caja()
        t_caida = p.t()
        camara.cancel()
        await p.tablet.close()                       # se cae sin soltar nada
        await p.esperar(lambda: any(t > t_caida and y == 0 for t, y in p.yaw), 4)
        comprobar("Unity centra la cámara al caerse la tablet", any(t > t_caida and y == 0 for t, y in p.yaw))
        await asyncio.sleep(4)
        comprobar(f"y suelta la cuerda: la caja ya no se queda llena (de {caja_al_caer} % a {caja()} %)",
                  caja() is not None and caja() <= caja_al_caer - 8)
        comprobar("el jugador 2 se entera (jugador1_salio)", any(a == "jugador1_salio" for _, a in p.avisos))
        # Pasa un rato con la tablet caída: la noche sigue corriendo.
        await p.esperar(lambda: (caja() or 100) <= 55, 20)
        caja_antes = caja()
        # La tablet vuelve: lo que manda la app tras una caída es `reconnect`.
        tablet = await websockets.connect(URL, ping_interval=None, max_queue=None)
        await tablet.send(json.dumps({"type": "reconnect", "device": "tablet", "player_id": "prueba_vp", "modo": "nuevo"}))
        recibidos = {}
        fin = time.time() + 3
        while time.time() < fin:
            try:
                m = json.loads(await asyncio.wait_for(tablet.recv(), 0.5))
            except asyncio.TimeoutError:
                continue
            recibidos.setdefault(m.get("type"), m)
        await tablet.close()
        comprobar("al volver, Unity le manda a la tablet el estado: night_status, task_list y estado_puppet",
                  all(t in recibidos for t in ("night_status", "task_list", "estado_puppet")))
        hora = recibidos.get("night_status", {}).get("in_game_time", "")
        comprobar(f"la noche sigue donde iba (son las {hora}, no las 12:00 AM de una noche nueva)", hora and not hora.startswith("12:0"))
        pct = recibidos.get("estado_puppet", {}).get("valor_caja_porcentaje", 100)
        comprobar(f"la caja de Puppet sigue donde iba ({pct} % y no el 100 % de una noche nueva; antes de volver: {caja_antes} %)",
                  pct <= (caja_antes or 100) + 3)
        await asyncio.sleep(0.5)
        comprobar("el jugador 2 se entera de que volvió (jugador1_volvio)", any(a == "jugador1_volvio" for _, a in p.avisos))
    finally:
        await p.cerrar()


ESCENARIOS = {"vixy": escenario_vixy, "rescate": escenario_rescate, "puppet": escenario_puppet, "tablet": escenario_tablet}


async def principal(nombres):
    for nombre in nombres:
        await ESCENARIOS[nombre]()
        await asyncio.sleep(2.0)
    print()
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


if __name__ == "__main__":
    elegidos = sys.argv[1:] or list(ESCENARIOS)
    sys.exit(asyncio.run(principal(elegidos)))
