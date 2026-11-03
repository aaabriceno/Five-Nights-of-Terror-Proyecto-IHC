"""Prueba manual de punta a punta del jugador 2 con el juego REAL exportado.

Levanta el relay y el juego, y hace de tablet (inicia la noche) y de operador
(pide el mapa y retrasa a Vixy). Abre la ventana del juego; ciérrala al final
con Ctrl+C si algo falla. Antes hay que exportar el juego (./exportar_linux.sh
o un build propio) y no tener otro relay en el puerto 8000.

Uso: UnityRelay/.venv/bin/python pruebas/probar_operador_con_juego.py [ruta/al/FiveNightsOfTerror.x86_64]
"""
import asyncio
import json
import subprocess
import sys
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
URL = "ws://127.0.0.1:8000"
JUEGO = Path(sys.argv[1]) if len(sys.argv) > 1 else RAIZ / "Unity/Builds/Linux/FiveNightsOfTerror.x86_64"
fallos = 0


def comprobar(descripcion, condicion):
    global fallos
    print(("  ok    " if condicion else "  FALLA ") + descripcion)
    if not condicion:
        fallos += 1


async def esperar_tipo(socket, tipo, espera=10.0):
    """Lee mensajes hasta que llegue uno del tipo pedido (o se acabe la espera)."""
    try:
        async with asyncio.timeout(espera):
            while True:
                mensaje = json.loads(await socket.recv())
                if mensaje.get("type") == tipo:
                    return mensaje
    except TimeoutError:
        return None


async def recolectar(socket, segundos):
    """Lee todo lo que llegue durante `segundos` y lo devuelve como lista."""
    mensajes = []
    try:
        async with asyncio.timeout(segundos):
            while True:
                mensajes.append(json.loads(await socket.recv()))
    except TimeoutError:
        pass
    return mensajes


async def principal():
    relay = subprocess.Popen([sys.executable, "-u", str(RAIZ / "UnityRelay/relay.py")],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    juego = None
    try:
        await asyncio.sleep(1.0)
        juego = subprocess.Popen([str(JUEGO)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        await asyncio.sleep(12.0)   # el juego tarda en abrir y conectarse al relay

        async with websockets.connect(URL) as tablet, websockets.connect(URL) as operador:
            await operador.send(json.dumps({"type": "operador_connect"}))
            puntos = await esperar_tipo(operador, "mapa_puntos")
            comprobar("el operador recibe mapa_puntos al conectarse", puntos is not None)
            if puntos:
                comprobar("el mapa trae puntos de ruta", len(puntos["puntos"]) > 0)
                comprobar("el mapa trae la posición del jugador", puntos.get("jugador") is not None)

            retrasar = json.dumps({"type": "accion_operador", "accion": "retrasar", "objetivo": "vixy"})
            await operador.send(retrasar)
            rechazo = await esperar_tipo(operador, "accion_rechazada")
            comprobar("sin partida, la acción se rechaza (sin_partida)",
                      rechazo is not None and rechazo["motivo"] == "sin_partida")

            await tablet.send(json.dumps({"type": "connect", "device": "tablet", "player_id": "prueba", "modo": "nuevo"}))
            estado = None
            for _ in range(10):
                estado = await esperar_tipo(operador, "estado_mapa", 3.0)
                if estado and estado["partida_activa"]:
                    break
            comprobar("con partida, estado_mapa llega con partida_activa", bool(estado and estado["partida_activa"]))
            if estado:
                nombres = sorted(a["nombre"] for a in estado["animatronicos"])
                comprobar("estado_mapa trae a los tres animatrónicos", nombres == ["freddy", "puppet", "vixy"])

            await operador.send(retrasar)
            efecto = await esperar_tipo(operador, "efecto")
            comprobar("retrasar a Vixy devuelve efecto", efecto is not None and efecto["animatronic"] == "vixy")
            efecto_tablet = await esperar_tipo(tablet, "efecto")
            comprobar("la tablet también recibe el efecto", efecto_tablet is not None)

            await operador.send(retrasar)
            rechazo = await esperar_tipo(operador, "accion_rechazada")
            comprobar("repetir enseguida se rechaza por enfriamiento",
                      rechazo is not None and rechazo["motivo"] == "enfriamiento" and rechazo["espera_s"] > 0)

            estado = await esperar_tipo(operador, "estado_mapa")
            vixy = next((a for a in estado["animatronicos"] if a["nombre"] == "vixy"), None) if estado else None
            comprobar("estado_mapa marca a Vixy como retrasada y en espera",
                      vixy is not None and vixy["retraso_s"] > 0 and vixy["espera_s"] > 0)

            # --- J1 -> J2: posiciones a 20 Hz, estado del jugador, estado del operador, pong ---
            await operador.send(json.dumps({"type": "ping_operador", "t": 123}))
            recibidos = await recolectar(operador, 2.0)
            tipos = [m.get("type") for m in recibidos]
            comprobar(f"llegan al menos 30 mensajes posiciones en 2 s (llegaron {tipos.count('posiciones')})",
                      tipos.count("posiciones") >= 30)
            jugador_estado = next((m for m in recibidos if m.get("type") == "estado_jugador"), None)
            comprobar("llega estado_jugador con sus 4 datos",
                      jugador_estado is not None and all(
                          k in jugador_estado for k in ("mirando_pantalla", "tareas_pendientes", "caja_pct", "caja_en_peligro")))
            comprobar("llega estado_operador", "estado_operador" in tipos)
            pong = next((m for m in recibidos if m.get("type") == "pong_operador"), None)
            comprobar("el ping se contesta con pong_operador y el mismo t", pong is not None and pong.get("t") == 123)
            posicion = next((m for m in recibidos if m.get("type") == "posiciones"), None)
            comprobar("posiciones trae a los 3 animatrónicos y al jugador",
                      posicion is not None and len(posicion["animatronicos"]) == 3 and "jugador" in posicion)

            # Un segundo operador que se conecta a mitad de la noche recibe todo de golpe.
            async with websockets.connect(URL) as operador2:
                await operador2.send(json.dumps({"type": "operador_connect"}))
                inicial = {m.get("type") for m in await recolectar(operador2, 1.5)}
                comprobar("un operador que reconecta recibe mapa, estado del mapa, del jugador y del operador",
                          {"mapa_puntos", "estado_mapa", "estado_jugador", "estado_operador"} <= inicial)

            await tablet.send(json.dumps({"type": "disconnect"}))
    finally:
        if juego:
            juego.terminate()
            juego.wait()
        relay.terminate()
        relay.wait()
    print()
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


sys.exit(asyncio.run(principal()))
