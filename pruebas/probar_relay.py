"""Pruebas del relay: levanta UnityRelay/relay.py en el puerto 8000 y le habla
con clientes WebSocket que hacen de tablet y de Unity.

Uso: UnityRelay/.venv/bin/python pruebas/probar_relay.py
"""
import asyncio
import json
import os
import subprocess
import sys
import time
from pathlib import Path

import websockets

RAIZ = Path(__file__).resolve().parent.parent
URL = "ws://127.0.0.1:8000"
CONNECT_TUTORIAL = json.dumps({"type": "connect", "device": "tablet", "modo": "tutorial"})
fallos = 0


def comprobar(descripcion, condicion):
    global fallos
    print(("  ok    " if condicion else "  FALLA ") + descripcion)
    if not condicion:
        fallos += 1


async def recibir_o_nada(socket, espera=1.0):
    try:
        return await asyncio.wait_for(socket.recv(), espera)
    except asyncio.TimeoutError:
        return None


async def esperar_relay():
    for _ in range(40):
        try:
            async with websockets.connect(URL):
                return True
        except OSError:
            await asyncio.sleep(0.25)
    return False


async def caso_tablet_ya_se_fue():
    print("Caso 1: la tablet se desconecta y después se abre Unity")
    async with websockets.connect(URL) as tablet:
        await tablet.send(CONNECT_TUTORIAL)
        await asyncio.sleep(0.3)
    await asyncio.sleep(0.3)
    async with websockets.connect(URL) as unity:
        await unity.send(json.dumps({"type": "unity_connect"}))
        mensaje = await recibir_o_nada(unity)
        comprobar("Unity no recibe el connect de una tablet que ya no está", mensaje is None)


async def caso_tablet_se_despide():
    print("Caso 2: la tablet manda disconnect y después se abre Unity")
    async with websockets.connect(URL) as tablet:
        await tablet.send(CONNECT_TUTORIAL)
        await tablet.send(json.dumps({"type": "disconnect"}))
        await asyncio.sleep(0.3)
        async with websockets.connect(URL) as unity:
            await unity.send(json.dumps({"type": "unity_connect"}))
            mensaje = await recibir_o_nada(unity)
            comprobar("Unity no recibe el connect tras el disconnect", mensaje is None)


async def caso_tablet_sigue_conectada():
    print("Caso 3: la tablet sigue conectada cuando se abre Unity")
    async with websockets.connect(URL) as tablet:
        await tablet.send(CONNECT_TUTORIAL)
        await asyncio.sleep(0.3)
        async with websockets.connect(URL) as unity:
            await unity.send(json.dumps({"type": "unity_connect"}))
            mensaje = await recibir_o_nada(unity)
            comprobar("Unity recibe el connect de la tablet presente", mensaje == CONNECT_TUTORIAL)


OPERADOR_CONNECT = json.dumps({"type": "operador_connect"})
ESTADO_MAPA = json.dumps({"type": "estado_mapa", "animatronicos": []})
EFECTO = json.dumps({"type": "efecto", "animatronic": "vixy", "tipo": "ralentizado"})
ACCION = json.dumps({"type": "accion_operador", "accion": "retrasar", "objetivo": "vixy"})


async def caso_operador_se_anuncia_a_unity():
    print("Caso 4: el operador se anuncia y Unity se entera, sin que cuente como tablet")
    async with websockets.connect(URL) as unity:
        await unity.send(json.dumps({"type": "unity_connect"}))
        await asyncio.sleep(0.2)
        async with websockets.connect(URL) as operador:
            await operador.send(OPERADOR_CONNECT)
            comprobar("Unity recibe operador_connect", await recibir_o_nada(unity) == OPERADOR_CONNECT)
    await asyncio.sleep(0.3)
    async with websockets.connect(URL) as unity_nuevo:
        await unity_nuevo.send(json.dumps({"type": "unity_connect"}))
        comprobar("un Unity nuevo no recibe un connect por culpa del operador",
                  await recibir_o_nada(unity_nuevo) is None)


async def caso_estado_del_mapa_solo_al_operador():
    print("Caso 5: el mapa va solo al operador; los efectos, a ambos")
    async with websockets.connect(URL) as unity, websockets.connect(URL) as tablet, \
            websockets.connect(URL) as operador:
        await unity.send(json.dumps({"type": "unity_connect"}))
        await tablet.send(CONNECT_TUTORIAL)
        await operador.send(OPERADOR_CONNECT)
        await asyncio.sleep(0.3)
        while await recibir_o_nada(unity, 0.2) is not None:   # connect y operador_connect
            pass
        await unity.send(ESTADO_MAPA)
        comprobar("el operador recibe estado_mapa", await recibir_o_nada(operador) == ESTADO_MAPA)
        comprobar("la tablet no recibe estado_mapa", await recibir_o_nada(tablet, 0.5) is None)
        await unity.send(EFECTO)
        comprobar("el operador recibe el efecto", await recibir_o_nada(operador) == EFECTO)
        comprobar("la tablet recibe el efecto", await recibir_o_nada(tablet) == EFECTO)


async def caso_accion_del_operador_solo_a_unity():
    print("Caso 6: la acción del operador llega a Unity y no a la tablet")
    async with websockets.connect(URL) as unity, websockets.connect(URL) as tablet, \
            websockets.connect(URL) as operador:
        await unity.send(json.dumps({"type": "unity_connect"}))
        await tablet.send(CONNECT_TUTORIAL)
        await operador.send(OPERADOR_CONNECT)
        await asyncio.sleep(0.3)
        while await recibir_o_nada(unity, 0.2) is not None:
            pass
        await operador.send(ACCION)
        comprobar("Unity recibe la acción", await recibir_o_nada(unity) == ACCION)
        comprobar("la tablet no recibe la acción", await recibir_o_nada(tablet, 0.5) is None)
        await tablet.send(json.dumps({"type": "task_completed", "task_id": 1}))
        comprobar("el operador no recibe lo que manda la tablet", await recibir_o_nada(operador, 0.5) is None)


async def caso_tipos_nuevos_solo_al_operador():
    print("Caso 7: posiciones, estado del jugador, estado del operador, pong y tareas solo al operador")
    tipos = ["posiciones", "estado_jugador", "estado_operador", "pong_operador", "tarea", "tarea_resultado"]
    async with websockets.connect(URL) as unity, websockets.connect(URL) as tablet, \
            websockets.connect(URL) as operador:
        await unity.send(json.dumps({"type": "unity_connect"}))
        await tablet.send(CONNECT_TUTORIAL)
        await operador.send(OPERADOR_CONNECT)
        await asyncio.sleep(0.3)
        while await recibir_o_nada(tablet, 0.2) is not None:
            pass
        while await recibir_o_nada(operador, 0.2) is not None:
            pass
        for tipo in tipos:
            await unity.send(json.dumps({"type": tipo}))
        recibidos = []
        for _ in tipos:
            crudo = await recibir_o_nada(operador)
            recibidos.append(json.loads(crudo)["type"] if crudo else None)
        comprobar("el operador recibe los 4 tipos nuevos", recibidos == tipos)
        comprobar("la tablet no recibe ninguno", await recibir_o_nada(tablet, 0.5) is None)


async def tipo_recibido(socket, espera=1.0):
    crudo = await recibir_o_nada(socket, espera)
    return json.loads(crudo).get("type") if crudo else None


async def caso_avisos_de_que_el_jugador_1_se_fue():
    print("Caso 8: el operador se entera de que el jugador 1 o el juego se fueron")
    async with websockets.connect(URL) as operador:
        await operador.send(OPERADOR_CONNECT)
        async with websockets.connect(URL) as unity:
            await unity.send(json.dumps({"type": "unity_connect"}))
            await asyncio.sleep(0.2)
            await tipo_recibido(unity, 0.3)               # el operador_connect no llega (Unity entró después)
            async with websockets.connect(URL) as tablet:
                await tablet.send(CONNECT_TUTORIAL)
                await asyncio.sleep(0.2)
                await tablet.send(json.dumps({"type": "disconnect"}))
                recibido = await recibir_o_nada(operador, 1.0)
                aviso = json.loads(recibido) if recibido else {}
                comprobar("tablet con disconnect: el operador recibe jugador1_salio (salio)",
                          aviso.get("type") == "jugador1_salio" and aviso.get("motivo") == "salio")
            async with websockets.connect(URL) as tablet:
                await tablet.send(CONNECT_TUTORIAL)
                await asyncio.sleep(0.2)
            recibido = await recibir_o_nada(operador, 1.0)
            aviso = json.loads(recibido) if recibido else {}
            comprobar("tablet que se cae sin despedirse: jugador1_salio (conexion_perdida)",
                      aviso.get("type") == "jugador1_salio" and aviso.get("motivo") == "conexion_perdida")
        comprobar("al cerrarse Unity el operador recibe juego_cerrado",
                  await tipo_recibido(operador, 1.0) == "juego_cerrado")


async def caso_operador_vuelve_dentro_del_tiempo():
    print("Caso 9: el operador se cae y tiene una ventana para volver")
    async with websockets.connect(URL) as unity:
        await unity.send(json.dumps({"type": "unity_connect"}))
        async with websockets.connect(URL) as operador:
            await operador.send(OPERADOR_CONNECT)
        await asyncio.sleep(0.4)
        async with websockets.connect(URL) as operador:
            await operador.send(OPERADOR_CONNECT)
            await asyncio.sleep(0.3)
            comprobar("vuelve antes de que se agote la ventana: no lo rechazan",
                      await tipo_recibido(operador, 0.3) != "operador_rechazado")
        await asyncio.sleep(2.0)                          # se agota la ventana (1,5 s en esta prueba)
        async with websockets.connect(URL) as operador:
            await operador.send(OPERADOR_CONNECT)
            comprobar("vuelve tarde: recibe operador_rechazado",
                      await tipo_recibido(operador, 1.0) == "operador_rechazado")
            try:
                await asyncio.wait_for(operador.wait_closed(), 1.0)
            except asyncio.TimeoutError:
                pass
            comprobar("y el relay le cierra la conexión", operador.close_code is not None)
        async with websockets.connect(URL) as tablet:
            await tablet.send(CONNECT_TUTORIAL)           # empieza otra partida
            await asyncio.sleep(0.3)
            async with websockets.connect(URL) as operador:
                await operador.send(OPERADOR_CONNECT)
                comprobar("con una partida nueva puede entrar otra vez",
                          await tipo_recibido(operador, 0.5) != "operador_rechazado")
        async with websockets.connect(URL) as operador:
            await operador.send(OPERADOR_CONNECT)
            await operador.send(json.dumps({"type": "operador_salir"}))
            await asyncio.sleep(0.2)
        await asyncio.sleep(2.0)
        async with websockets.connect(URL) as operador:
            await operador.send(OPERADOR_CONNECT)
            comprobar("salir a propósito no cuenta como caída: puede volver",
                      await tipo_recibido(operador, 0.5) != "operador_rechazado")


async def caso_la_tablet_vuelve_sin_reiniciar_la_noche():
    print("Caso 10: la tablet se cae y vuelve con reconnect (no reinicia la noche)")
    async with websockets.connect(URL) as unity:
        await unity.send(json.dumps({"type": "unity_connect"}))
        async with websockets.connect(URL) as operador:
            await operador.send(OPERADOR_CONNECT)
            await tipo_recibido(unity, 0.5)                       # el operador_connect que le pasa el relay
            tablet = await websockets.connect(URL)
            await tablet.send(CONNECT_TUTORIAL)
            comprobar("Unity recibe el connect de la tablet", await tipo_recibido(unity, 1.0) == "connect")
            await tablet.close()                                  # se cae sin despedirse
            comprobar("al caerse la tablet sin despedirse, Unity recibe tablet_caida",
                      await tipo_recibido(unity, 1.0) == "tablet_caida")
            await tipo_recibido(operador, 0.5)                    # jugador1_salio (conexion_perdida)

            async with websockets.connect(URL) as tablet2:
                await tablet2.send(json.dumps({"type": "reconnect", "device": "tablet", "modo": "continuar"}))
                recibido = await recibir_o_nada(unity, 1.0)
                comprobar("Unity recibe reconnect (y no un connect, que reiniciaría la noche)",
                          recibido is not None and json.loads(recibido).get("type") == "reconnect")
                comprobar("el operador se entera de que el jugador 1 volvió (jugador1_volvio)",
                          await tipo_recibido(operador, 1.0) == "jugador1_volvio")

        # El reconnect no pisa el connect guardado para un Unity que se abra después.
        async with websockets.connect(URL) as tablet_a:
            await tablet_a.send(json.dumps({"type": "connect", "device": "tablet", "modo": "tutorial", "marca": "original"}))
            await asyncio.sleep(0.2)
            async with websockets.connect(URL) as tablet_b:
                await tablet_b.send(json.dumps({"type": "reconnect", "device": "tablet", "modo": "continuar"}))
                await asyncio.sleep(0.2)
                async with websockets.connect(URL) as unity_nuevo:
                    await unity_nuevo.send(json.dumps({"type": "unity_connect"}))
                    recibido = await recibir_o_nada(unity_nuevo, 1.0)
                    guardado = json.loads(recibido) if recibido else {}
                    comprobar("un Unity que se abre después recibe el connect original, no el reconnect",
                              guardado.get("type") == "connect" and guardado.get("marca") == "original")

    print("Caso 11: despedirse a propósito no cuenta como caída")
    async with websockets.connect(URL) as unity:
        await unity.send(json.dumps({"type": "unity_connect"}))
        async with websockets.connect(URL) as tablet:
            await tablet.send(CONNECT_TUTORIAL)
            await tipo_recibido(unity, 0.5)
            await tablet.send(json.dumps({"type": "disconnect"}))
            comprobar("con disconnect, Unity recibe disconnect", await tipo_recibido(unity, 1.0) == "disconnect")
        comprobar("y no recibe tablet_caida", await tipo_recibido(unity, 0.6) is None)


async def caso_una_tablet_muda_se_detecta_pronto():
    print("Caso 12: una tablet que se queda sin Wi-Fi (no contesta los ping) se detecta pronto")
    import importlib.util
    especificacion = importlib.util.spec_from_file_location("relay_para_leer", RAIZ / "UnityRelay" / "relay.py")
    relay_modulo = importlib.util.module_from_spec(especificacion)
    especificacion.loader.exec_module(relay_modulo)
    peor_caso = relay_modulo.PING_INTERVALO_S + relay_modulo.PING_TIMEOUT_S + relay_modulo.CIERRE_TIMEOUT_S
    comprobar(f"por defecto el relay tarda como mucho 12 s en darla por perdida ({relay_modulo.PING_INTERVALO_S:g} + {relay_modulo.PING_TIMEOUT_S:g} + {relay_modulo.CIERRE_TIMEOUT_S:g} s = {peor_caso:g} s)",
              peor_caso <= 12)
    async with websockets.connect(URL) as unity:
        await unity.send(json.dumps({"type": "unity_connect"}))
        async with websockets.connect(URL, ping_interval=None) as tablet:
            await tablet.send(CONNECT_TUTORIAL)
            await tipo_recibido(unity, 0.5)
            tablet.transport.pause_reading()                      # deja de leer: no contesta los ping del relay
            comprobar("sin contestar los ping, el relay avisa a Unity (tablet_caida) en pocos segundos",
                      await tipo_recibido(unity, 4.0) == "tablet_caida")


async def caso_un_cliente_lento_no_frena_a_los_demas():
    print("Caso 13: una tablet que no lee no frena a los demás")
    async with websockets.connect(URL) as unity:
        await unity.send(json.dumps({"type": "unity_connect"}))
        async with websockets.connect(URL) as operador:
            await operador.send(OPERADOR_CONNECT)
            await tipo_recibido(unity, 0.5)
            tablet = await websockets.connect(URL, ping_interval=None, max_queue=1)
            await tablet.send(CONNECT_TUTORIAL)
            await tipo_recibido(unity, 0.5)
            tablet.transport.pause_reading()                  # se queda sin leer (Wi-Fi caído, app congelada)

            llegadas = []

            async def leer():
                async for crudo in operador:
                    if json.loads(crudo).get("type") == "estado_puppet":
                        llegadas.append(time.perf_counter())

            lector = asyncio.ensure_future(leer())
            # Unity manda 30 mensajes por segundo (de los que también reciben las tablets).
            relleno = "x" * 2000
            unity_cerrada = False
            try:
                for i in range(120):
                    await unity.send(json.dumps({"type": "estado_puppet", "n": i, "relleno": relleno}))
                    await asyncio.sleep(1 / 30)
            except websockets.exceptions.ConnectionClosed:
                unity_cerrada = True
            await asyncio.sleep(0.3)
            lector.cancel()
            huecos = [b - a for a, b in zip(llegadas, llegadas[1:])] or [99.0]
            print(f"  llegaron {len(llegadas)} de 120 al operador; el mayor hueco entre dos fue de {max(huecos) * 1000:.0f} ms")
            comprobar("el relay no le cierra la conexión a Unity (antes, con la tablet atascada, dejaba de leerla y la daba por perdida)",
                      not unity_cerrada)
            comprobar("al operador le llegan todos los mensajes de Unity", len(llegadas) == 120)
            comprobar("y ninguno se atrasa más de 0,5 s por culpa de la tablet que no lee (antes se quedaba esperando a que la cerraran)",
                      max(huecos) < 0.5)
            tablet.transport.resume_reading()
            await tablet.close()


async def caso_sin_compresion_ni_mensajes_gigantes():
    print("Caso 14: el relay no negocia compresión y corta un mensaje gigante")
    # El cliente (como el navegador y la tablet) pide permessage-deflate: el relay no debe aceptarlo.
    async with websockets.connect(URL) as cliente:
        cabeceras = getattr(cliente, "response", None)
        cabeceras = cabeceras.headers if cabeceras is not None else getattr(cliente, "response_headers", {})
        extensiones = cabeceras.get("Sec-WebSocket-Extensions", "") or ""
        comprobar("la respuesta del relay no acepta permessage-deflate (comprimir 30 mensajes por segundo cuesta CPU y tiempo)",
                  "permessage-deflate" not in extensiones)
    async with websockets.connect(URL) as cliente:
        await cliente.send(json.dumps({"type": "unity_connect"}))
        codigo = None
        try:
            await cliente.send("x" * 200_000)
            await asyncio.wait_for(cliente.recv(), 2)
        except websockets.exceptions.ConnectionClosed as cierre:
            codigo = cierre.rcvd.code if cierre.rcvd else -1
        except asyncio.TimeoutError:
            pass
        comprobar(f"un mensaje de 200 kB cierra la conexión con 1009 (los mensajes del juego pesan menos de 10 kB); código: {codigo}",
                  codigo == 1009)


def ip_de_la_red_local():
    """Una IP de esta PC que no sea loopback ni de Docker, para conectarse «desde afuera»."""
    import socket as red
    try:
        with red.socket(red.AF_INET, red.SOCK_DGRAM) as s:
            s.connect(("8.8.8.8", 65530))
            ip = s.getsockname()[0]
        return None if ip.startswith("127.") else ip
    except OSError:
        return None


async def caso_unity_solo_desde_la_misma_pc():
    print("Caso 15: solo un cliente de la misma PC puede hacerse pasar por Unity")
    ip = ip_de_la_red_local()
    if ip is None:
        print("  (sin red local: no se puede probar desde otra IP)")
        return
    async with websockets.connect(f"ws://{ip}:8000") as intruso:
        await intruso.send(json.dumps({"type": "unity_connect"}))
        crudo = await recibir_o_nada(intruso)
        rechazo = json.loads(crudo) if crudo else {}
        comprobar(f"desde {ip} recibe conexion_rechazada con motivo unity_solo_local",
                  rechazo.get("type") == "conexion_rechazada" and rechazo.get("motivo") == "unity_solo_local")
        cerrado = False
        try:
            await asyncio.wait_for(intruso.recv(), 2)
        except websockets.exceptions.ConnectionClosed:
            cerrado = True
        except asyncio.TimeoutError:
            pass
        comprobar("y el relay le cierra la conexión", cerrado)
    async with websockets.connect(URL) as unity, websockets.connect(f"ws://{ip}:8000") as tablet:
        await unity.send(json.dumps({"type": "unity_connect"}))
        await tablet.send(CONNECT_TUTORIAL)
        comprobar("una tablet desde la red local sí entra y Unity recibe su connect",
                  await recibir_o_nada(unity) == CONNECT_TUTORIAL)


async def caso_cada_rol_manda_solo_lo_suyo():
    print("Caso 16: el relay descarta lo que no corresponde al rol (un operador no resuelve tareas del jugador 1)")
    async with websockets.connect(URL) as unity, websockets.connect(URL) as tablet, \
            websockets.connect(URL) as operador:
        await unity.send(json.dumps({"type": "unity_connect"}))
        await tablet.send(CONNECT_TUTORIAL)
        await operador.send(OPERADOR_CONNECT)
        await asyncio.sleep(0.3)
        while await recibir_o_nada(unity, 0.2) is not None:
            pass
        for tipo in ("task_completed", "dar_cuerda_inicio", "tutorial_abrir", "connect"):
            await operador.send(json.dumps({"type": tipo, "task_id": 1, "modo": "nuevo"}))
        comprobar("Unity no recibe task_completed, dar_cuerda_inicio, tutorial_abrir ni connect de un operador",
                  await recibir_o_nada(unity, 0.6) is None)
        await operador.send(ACCION)
        comprobar("pero sí recibe su accion_operador (el operador sigue siendo operador)",
                  await recibir_o_nada(unity) == ACCION)
        for tipo in ("accion_operador", "tarea_pedir", "tarea_respuesta", "operador_connect"):
            await tablet.send(json.dumps({"type": tipo, "accion": "trampa", "punto": "P01"}))
        comprobar("Unity no recibe accion_operador, tarea_pedir, tarea_respuesta ni operador_connect de una tablet",
                  await recibir_o_nada(unity, 0.6) is None)
        camara = json.dumps({"type": "camara", "direccion": 1})
        await tablet.send(camara)
        comprobar("pero sí recibe su camara", await recibir_o_nada(unity) == camara)


async def caso_una_sola_tablet_por_partida():
    print("Caso 17: mientras una tablet juega, otra distinta no puede entrar; la misma tablet sí vuelve")
    connect_a = json.dumps({"type": "connect", "device": "tablet", "modo": "nuevo", "player_id": "tablet_a"})
    connect_b = json.dumps({"type": "connect", "device": "tablet", "modo": "salir", "player_id": "tablet_b"})
    reconnect_a = json.dumps({"type": "reconnect", "device": "tablet", "modo": "nuevo", "player_id": "tablet_a"})
    camara = json.dumps({"type": "camara", "direccion": -1})
    async with websockets.connect(URL) as unity, websockets.connect(URL) as tablet_a, \
            websockets.connect(URL) as operador:
        await unity.send(json.dumps({"type": "unity_connect"}))
        await operador.send(OPERADOR_CONNECT)
        await recibir_o_nada(unity, 0.5)
        await tablet_a.send(connect_a)
        comprobar("Unity recibe el connect de la tablet A", await recibir_o_nada(unity) == connect_a)
        async with websockets.connect(URL) as tablet_b:
            await tablet_b.send(connect_b)
            crudo = await recibir_o_nada(tablet_b)
            rechazo = json.loads(crudo) if crudo else {}
            comprobar("la tablet B recibe conexion_rechazada con motivo ya_hay_jugador1 (antes su «salir» cerraba el juego del otro)",
                      rechazo.get("type") == "conexion_rechazada" and rechazo.get("motivo") == "ya_hay_jugador1")
            comprobar("Unity no recibe el connect de la tablet B", await recibir_o_nada(unity, 0.5) is None)
        await tablet_a.send(camara)
        comprobar("la tablet A sigue jugando", await recibir_o_nada(unity) == camara)
        async with websockets.connect(URL) as tablet_a2:
            await tablet_a2.send(reconnect_a)
            comprobar("la misma tablet (mismo player_id) vuelve con reconnect y Unity lo recibe",
                      await recibir_o_nada(unity) == reconnect_a)
            vieja_cerrada = False
            try:
                await asyncio.wait_for(tablet_a.recv(), 2)
            except websockets.exceptions.ConnectionClosed:
                vieja_cerrada = True
            except asyncio.TimeoutError:
                pass
            comprobar("el relay cierra la conexión vieja de esa tablet (no espera 12 s de keepalive)", vieja_cerrada)
            avisos = []
            while (tipo := await tipo_recibido(operador, 0.4)) is not None:
                avisos.append(tipo)
            comprobar(f"y no avisa al operador de que el jugador 1 se fue (sigue ahí); recibió {avisos}",
                      "jugador1_salio" not in avisos)
            await tablet_a2.send(camara)
            comprobar("la conexión nueva de la tablet A manda al juego", await recibir_o_nada(unity) == camara)
            await tablet_a2.send(json.dumps({"type": "disconnect"}))
            await recibir_o_nada(unity, 0.5)
            async with websockets.connect(URL) as tablet_b:
                await tablet_b.send(connect_b)
                comprobar("cuando la tablet A se despide, otra tablet ya puede entrar",
                          await recibir_o_nada(unity) == connect_b)


async def con_relay(entorno, caso):
    """Corre `caso` con un relay propio (otras esperas de keepalive que el de las demás pruebas)."""
    relay = subprocess.Popen([sys.executable, "-u", str(RAIZ / "UnityRelay" / "relay.py")],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env={**os.environ, **entorno})
    try:
        if not await esperar_relay():
            print("El relay no arrancó (¿puerto 8000 ocupado?).")
            return
        await caso()
    finally:
        relay.terminate()
        relay.wait()


async def principal():
    relay = subprocess.Popen(
        [sys.executable, "-u", str(RAIZ / "UnityRelay" / "relay.py")],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        env={**os.environ, "VENTANA_OPERADOR_S": "1.5", "PING_INTERVALO_S": "0.4", "PING_TIMEOUT_S": "0.4", "CIERRE_TIMEOUT_S": "0.4"},
    )
    try:
        if not await esperar_relay():
            print("El relay no arrancó (¿puerto 8000 ocupado?).")
            return 2
        await caso_tablet_ya_se_fue()
        await caso_tablet_se_despide()
        await caso_tablet_sigue_conectada()
        await caso_operador_se_anuncia_a_unity()
        await caso_estado_del_mapa_solo_al_operador()
        await caso_accion_del_operador_solo_a_unity()
        await caso_tipos_nuevos_solo_al_operador()
        await caso_avisos_de_que_el_jugador_1_se_fue()
        await caso_operador_vuelve_dentro_del_tiempo()
        await caso_la_tablet_vuelve_sin_reiniciar_la_noche()
        await caso_una_tablet_muda_se_detecta_pronto()
        await caso_sin_compresion_ni_mensajes_gigantes()
        await caso_unity_solo_desde_la_misma_pc()
        await caso_cada_rol_manda_solo_lo_suyo()
        await caso_una_sola_tablet_por_partida()
    finally:
        relay.terminate()
        relay.wait()
    await asyncio.sleep(0.5)
    # Con el cierre de 2 s se nota si cerrar a un cliente que no contesta frena a los demás.
    await con_relay({"PING_INTERVALO_S": "0.5", "PING_TIMEOUT_S": "0.5", "CIERRE_TIMEOUT_S": "2"},
                    caso_un_cliente_lento_no_frena_a_los_demas)
    print()
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


sys.exit(asyncio.run(principal()))
