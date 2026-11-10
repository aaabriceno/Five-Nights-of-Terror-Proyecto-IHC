"""Prueba manual de punta a punta del jugador 2 con el juego REAL exportado.

Levanta el relay y el juego, y hace de tablet (inicia la noche) y de operador
(pide el mapa y retrasa a Vixy). Abre la ventana del juego; ciérrala al final
con Ctrl+C si algo falla. Antes hay que exportar el juego (./exportar_linux.sh
o un build propio) y no tener otro relay en el puerto 8000.

Uso: UnityRelay/.venv/bin/python pruebas/probar_operador_con_juego.py [ruta/al/FiveNightsOfTerror.x86_64]
"""
import asyncio
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

import websockets

sys.path.insert(0, str(Path(__file__).resolve().parent))
import solucionador_de_tareas as solucion  # noqa: E402

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


async def esperar_efecto(socket, tipo, espera=10.0):
    """Lee mensajes hasta un `efecto` de ese tipo (los otros efectos se saltan)."""
    try:
        async with asyncio.timeout(espera):
            while True:
                mensaje = json.loads(await socket.recv())
                if mensaje.get("type") == "efecto" and mensaje.get("tipo") == tipo:
                    return mensaje
    except TimeoutError:
        return None


async def yaw_de_la_camara(socket, espera=2.0):
    """El último `camara_yaw` que llegue en `posiciones` durante `espera` segundos (None si ninguno)."""
    yaw = None
    for mensaje in await recolectar(socket, espera):
        if mensaje.get("type") == "posiciones" and "camara_yaw" in mensaje:
            yaw = mensaje["camara_yaw"]
    return yaw


async def mantener_boton(tablet, direccion, segundos):
    """Como el dedo apoyado en la tablet: repite la orden cada 0,15 s y al final manda 0."""
    fin = asyncio.get_event_loop().time() + segundos
    while asyncio.get_event_loop().time() < fin:
        await tablet.send(json.dumps({"type": "camara", "direccion": direccion}))
        await asyncio.sleep(0.15)
    await tablet.send(json.dumps({"type": "camara", "direccion": 0}))


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


async def esperar_respuesta(socket, identificador, espera=5.0):
    """Lee hasta la respuesta (efecto o accion_rechazada) a la acción con ese id."""
    try:
        async with asyncio.timeout(espera):
            while True:
                mensaje = json.loads(await socket.recv())
                if mensaje.get("type") in ("efecto", "accion_rechazada") and mensaje.get("id") == identificador:
                    return mensaje
    except TimeoutError:
        return None


async def esperar_estado_operador(socket, condicion, espera=5.0):
    """Lee estado_operador hasta que cumpla la condición (o se acabe la espera)."""
    try:
        async with asyncio.timeout(espera):
            while True:
                mensaje = json.loads(await socket.recv())
                if mensaje.get("type") == "estado_operador" and condicion(mensaje):
                    return mensaje
    except TimeoutError:
        return None


async def latidos(socket):
    """Como la página: un ping cada 2 s para que Unity cuente al operador como conectado."""
    while True:
        await socket.send(json.dumps({"type": "ping_operador", "t": 1}))
        await asyncio.sleep(2.0)


async def principal():
    relay = subprocess.Popen([sys.executable, "-u", str(RAIZ / "UnityRelay/relay.py")],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    juego = None
    pantalla = None
    entorno = dict(os.environ)
    try:
        # En una pantalla virtual (Xvfb) y en ventana chica: la prueba no te tapa el escritorio. Xvfb dibuja
        # por software y las posiciones salen una por cuadro, así que la ventana es chica para que lleguen a 30 por segundo.
        if shutil.which("Xvfb"):
            pantalla = subprocess.Popen(["Xvfb", ":97", "-screen", "0", "1280x720x24"],
                                        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            entorno["DISPLAY"] = ":97"
            await asyncio.sleep(1.5)
        await asyncio.sleep(1.0)
        juego = subprocess.Popen([str(JUEGO), "-screen-fullscreen", "0", "-screen-width", "640", "-screen-height", "360"],
                                 env=entorno, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        await asyncio.sleep(12.0)   # el juego tarda en abrir y conectarse al relay

        async with websockets.connect(URL, ping_interval=None, max_queue=None) as tablet, \
                websockets.connect(URL, ping_interval=None, max_queue=None) as operador:
            await operador.send(json.dumps({"type": "operador_connect"}))
            puntos = await esperar_tipo(operador, "mapa_puntos")
            comprobar("el operador recibe mapa_puntos al conectarse", puntos is not None)
            if puntos:
                comprobar("el mapa trae puntos de ruta", len(puntos["puntos"]) > 0)
                comprobar("el mapa trae la posición del jugador", puntos.get("jugador") is not None)
                rutas = {r["animatronico"]: r["puntos"] for r in puntos.get("rutas", [])}
                comprobar("el mapa trae la guía: la ruta fija de los tres animatrónicos",
                          sorted(rutas) == ["freddy", "puppet", "vixy"])
                comprobar("la ruta de Freddy es la del plano y termina en el punto penúltimo, sin el jugador",
                          rutas.get("freddy") == ["P01", "P03", "P04", "P05", "P06", "P07"])
                comprobar("la ruta de Vixy va por el pasillo izquierdo hasta el ducto (P10 es solo un desvío)",
                          rutas.get("vixy") == ["P02", "P11", "P12", "V01", "V02", "V03", "V04"])
                comprobar("la ruta de Puppet va por el pasillo derecho hasta el ducto",
                          rutas.get("puppet") == ["P13", "P14", "P15", "P16", "V05", "V06", "V07", "V08"])

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

            # El jugador 1 (tablet) se entera de que tiene compañero: con él los
            # animatrónicos van más rápido.
            conectado = await esperar_efecto(tablet, "operador_conectado", 5.0)
            comprobar("la tablet recibe el efecto operador_conectado al empezar la partida",
                      conectado is not None and "compañero" in conectado.get("message", ""))

            await operador.send(retrasar)
            efecto = await esperar_tipo(operador, "efecto")
            comprobar("retrasar a Vixy devuelve efecto", efecto is not None and efecto["animatronic"] == "vixy")
            efecto_tablet = await esperar_efecto(tablet, "ralentizado")
            comprobar("la tablet también recibe el efecto de retrasar, con el animatrónico y los segundos",
                      efecto_tablet is not None and efecto_tablet["animatronic"] == "vixy" and efecto_tablet["segundos"] > 0)
            puppet_tablet = await esperar_tipo(tablet, "estado_puppet", 3.0)
            comprobar("la tablet recibe estado_puppet con multa_factor 1 mientras nadie rescate a Puppet",
                      puppet_tablet is not None and puppet_tablet.get("multa_factor") == 1)

            await operador.send(retrasar)
            rechazo = await esperar_tipo(operador, "accion_rechazada")
            comprobar("repetir enseguida se rechaza por enfriamiento",
                      rechazo is not None and rechazo["motivo"] == "enfriamiento" and rechazo["espera_s"] > 0)

            estado = await esperar_tipo(operador, "estado_mapa")
            vixy = next((a for a in estado["animatronicos"] if a["nombre"] == "vixy"), None) if estado else None
            comprobar("estado_mapa marca a Vixy como retrasada y en espera",
                      vixy is not None and vixy["retraso_s"] > 0 and vixy["espera_s"] > 0)

            # --- J1 -> J2: posiciones a 30 Hz, estado del jugador, estado del operador, pong ---
            await operador.send(json.dumps({"type": "ping_operador", "t": 123}))
            recibidos = await recolectar(operador, 2.0)
            tipos = [m.get("type") for m in recibidos]
            comprobar(f"llegan al menos 50 mensajes posiciones en 2 s: a 30 Hz son ~60 (llegaron {tipos.count('posiciones')})",
                      tipos.count("posiciones") >= 50)
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

            # --- cámara del jugador 1: los botones de la tablet la giran y el jugador 2 lo ve ---
            tarea_latidos = asyncio.ensure_future(latidos(operador))   # sin latidos Unity da al operador por caído (y calla las posiciones)
            comprobar("al empezar la partida la cámara mira al frente", await yaw_de_la_camara(operador, 0.5) == 0)
            tarea_boton = asyncio.ensure_future(mantener_boton(tablet, -1, 1.2))
            yaw_izquierda = await yaw_de_la_camara(operador, 1.3)
            await tarea_boton
            comprobar(f"mantener «izquierda» 1,2 s gira la cámara a la izquierda (llegó a {yaw_izquierda}°)",
                      yaw_izquierda is not None and -90 < yaw_izquierda < -60)
            yaw_vuelta = await yaw_de_la_camara(operador, 1.6)
            comprobar(f"al soltar vuelve sola al frente (quedó en {yaw_vuelta}°)", yaw_vuelta == 0)
            tarea_boton = asyncio.ensure_future(mantener_boton(tablet, 1, 5.0))
            yaw_derecha = await yaw_de_la_camara(operador, 5.0)
            await tarea_boton
            comprobar(f"mantener «derecha» no pasa de 75° (llegó a {yaw_derecha}°)", yaw_derecha is not None and 70 < yaw_derecha <= 75)
            yaw_vuelta = await yaw_de_la_camara(operador, 1.6)
            comprobar(f"y también vuelve al frente (quedó en {yaw_vuelta}°)", yaw_vuelta == 0)
            await tablet.send(json.dumps({"type": "camara", "direccion": 1}))     # el «soltar» se pierde
            yaw_perdido = await yaw_de_la_camara(operador, 3.0)
            comprobar(f"si se pierde el «soltar», la cámara no se queda girando (quedó en {yaw_perdido}°)", yaw_perdido == 0)

            # Un segundo operador que se conecta a mitad de la noche recibe todo de golpe.
            async with websockets.connect(URL, ping_interval=None, max_queue=None) as operador2:
                await operador2.send(json.dumps({"type": "operador_connect"}))
                inicial = {m.get("type") for m in await recolectar(operador2, 1.5)}
                comprobar("un operador que reconecta recibe mapa, estado del mapa, del jugador y del operador",
                          {"mapa_puntos", "estado_mapa", "estado_jugador", "estado_operador"} <= inicial)

            # --- trampas, tareas de la terminal y rescate de Puppet ---
            # Los últimos puntos están lejos de donde nacen los animatrónicos: una trampa en
            # el punto de salida (P01, P02, P13) se dispara al instante y deja de estar ocupada.
            punto_a, punto_b = puntos["puntos"][-1]["nombre"], puntos["puntos"][-2]["nombre"]

            def accion(identificador, **campos):
                return json.dumps({"type": "accion_operador", "id": identificador, **campos})

            await operador.send(accion(1, accion="trampa", punto=punto_a))
            r = await esperar_respuesta(operador, 1)
            comprobar("colocar una trampa con carga devuelve efecto trampa_colocada con el mismo id",
                      r is not None and r["type"] == "efecto" and r.get("tipo") == "trampa_colocada")
            trampa_tablet = await esperar_efecto(tablet, "trampa_colocada", 3.0)
            comprobar("la tablet recibe que el compañero puso una trampa", trampa_tablet is not None)
            e = await esperar_estado_operador(operador, lambda m: m["cargas"] == 0 and punto_a in m["trampas"])
            comprobar("estado_operador muestra 0 cargas y la trampa puesta", e is not None)

            await operador.send(accion(2, accion="trampa", punto=punto_b))
            r = await esperar_respuesta(operador, 2)
            comprobar("sin cargas se rechaza (sin_cargas)", r is not None and r.get("motivo") == "sin_cargas")

            # --- terminal: un cálculo da 1 carga y un circuito lógico desbloquea el rescate ---
            # Unity inventa el problema y comprueba la respuesta; aquí lo resuelve solucionador_de_tareas.
            def pedir(tipo):
                return operador.send(json.dumps({"type": "tarea_pedir", "tipo": tipo}))

            await operador.send(json.dumps({"type": "tarea_respuesta", "id": 999, "valor": 1}))
            r = await esperar_tipo(operador, "accion_rechazada")
            comprobar("responder sin haber pedido una tarea se rechaza (tarea_sin_pedir)",
                      r is not None and r.get("objetivo") == "tarea" and r.get("motivo") == "tarea_sin_pedir")
            await pedir("raro")
            r = await esperar_tipo(operador, "accion_rechazada")
            comprobar("un tipo de tarea que no existe se rechaza (tarea_desconocida)",
                      r is not None and r.get("motivo") == "tarea_desconocida")

            await pedir("calculo")
            calculo = await esperar_tipo(operador, "tarea")
            comprobar("Unity entrega un cálculo con enunciado, clase y datos de la noche 1",
                      calculo is not None and calculo["tipo"] == "calculo" and calculo["noche"] == 1
                      and calculo["enunciado"] and calculo["clase"] in ("binario", "aritmetica") and calculo["datos"])
            await pedir("calculo")
            repetido = await esperar_tipo(operador, "tarea")
            comprobar("pedirlo otra vez da el mismo problema (cerrar y abrir no sirve para buscar uno fácil)",
                      repetido is not None and repetido["id"] == calculo["id"] and repetido["enunciado"] == calculo["enunciado"])
            await operador.send(json.dumps(solucion.respuesta_para(calculo)))
            r = await esperar_tipo(operador, "accion_rechazada")
            comprobar("responderlo al instante se rechaza por muy rápido (tarea_muy_rapida)",
                      r is not None and r.get("motivo") == "tarea_muy_rapida")
            await asyncio.sleep(1.6)
            equivocada = solucion.respuesta_para(calculo)
            equivocada["valor"] += 1
            await operador.send(json.dumps(equivocada))
            resultado = await esperar_tipo(operador, "tarea_resultado")
            comprobar("una respuesta equivocada es incorrecta y bloquea la terminal ~3 s",
                      resultado is not None and resultado["correcto"] is False and 2.0 <= resultado["bloqueo_s"] <= 3.1)
            await pedir("calculo")
            r = await esperar_tipo(operador, "accion_rechazada")
            comprobar("durante el bloqueo no se entrega otra tarea (bloqueado, con los segundos que faltan)",
                      r is not None and r.get("motivo") == "bloqueado" and r.get("espera_s", 0) > 0)
            await asyncio.sleep(3.3)
            await pedir("calculo")
            calculo = await esperar_tipo(operador, "tarea")
            comprobar("pasado el bloqueo se entrega un problema nuevo", calculo is not None and calculo["id"] != repetido["id"])
            await asyncio.sleep(1.6)
            await operador.send(json.dumps(solucion.respuesta_para(calculo)))
            resultado = await esperar_tipo(operador, "tarea_resultado")
            comprobar("la respuesta correcta se acepta", resultado is not None and resultado["correcto"] is True)
            e = await esperar_estado_operador(operador, lambda m: m["cargas"] == 1 and not m["habilidad_puppet"])
            comprobar("y da 1 carga de trampa, sin desbloquear el rescate", e is not None)

            await pedir("circuito")
            circuito = await esperar_tipo(operador, "tarea")
            comprobar("Unity entrega un circuito de la noche 1 (3 entradas y puertas encadenadas)",
                      circuito is not None and circuito["tipo"] == "circuito" and len(circuito["entradas"]) == 3
                      and len(circuito["puertas"]) >= 2 and circuito["salida"] == circuito["puertas"][-1]["id"])
            comprobar("el circuito tiene solución", solucion.resolver_circuito(circuito) is not None)
            await asyncio.sleep(3.3)
            await operador.send(json.dumps(solucion.respuesta_para(circuito)))
            resultado = await esperar_tipo(operador, "tarea_resultado")
            comprobar("resolver el circuito se acepta", resultado is not None and resultado["correcto"] is True)
            e = await esperar_estado_operador(operador, lambda m: m["habilidad_puppet"] and m["progreso"] == m["necesario"] == 1)
            comprobar("y desbloquea el rescate de Puppet (1 circuito)", e is not None)
            await pedir("circuito")
            r = await esperar_tipo(operador, "accion_rechazada")
            comprobar("con el rescate ya desbloqueado no se pide otro circuito (habilidad_lista)",
                      r is not None and r.get("motivo") == "habilidad_lista")
            comprobar("las tareas no llegan a la tablet (son solo del jugador 2)",
                      all(m.get("type") not in ("tarea", "tarea_resultado") for m in await recolectar(tablet, 0.5)))

            await operador.send(accion(3, accion="trampa", punto="PuntoQueNoExiste"))
            r = await esperar_respuesta(operador, 3)
            comprobar("un punto que no existe se rechaza (punto_invalido)", r is not None and r.get("motivo") == "punto_invalido")

            await operador.send(accion(4, accion="trampa", punto=punto_a))
            r = await esperar_respuesta(operador, 4)
            comprobar("un punto ya ocupado se rechaza (punto_ocupado)", r is not None and r.get("motivo") == "punto_ocupado")

            await operador.send(accion(5, accion="rescatar_puppet"))
            r = await esperar_respuesta(operador, 5)
            comprobar("rescatar a Puppet sin que haya salido se rechaza (sin_ventana)",
                      r is not None and r.get("motivo") == "sin_ventana")
            tarea_latidos.cancel()

            # Sin latidos durante 6 s Unity da por caído al compañero y se lo cuenta a la tablet.
            perdido = await esperar_efecto(tablet, "operador_perdido", 12.0)
            comprobar("sin latidos del compañero, la tablet recibe operador_perdido",
                      perdido is not None and "desconect" in perdido.get("message", ""))

            await tablet.send(json.dumps({"type": "disconnect"}))
    finally:
        if juego:
            juego.terminate()
            juego.wait()
        relay.terminate()
        relay.wait()
        if pantalla:
            pantalla.terminate()
            pantalla.wait()
    print()
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


sys.exit(asyncio.run(principal()))
