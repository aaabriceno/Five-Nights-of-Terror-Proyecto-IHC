"""Relay WebSocket de la integración con Unity como autoridad del juego.

Solo reenvía mensajes entre la tablet (jugador 1), la página del jugador 2
(operador) y el juego de Unity. No decide nada del juego.
"""

import asyncio
import json
import os
import socket as red
import time
import websockets
from websockets.asyncio.server import broadcast

HOST = "0.0.0.0"
PORT = 8000

tablets = set()
unity_clients = set()
operadores = set()
identidad_de_tablet = {}        # socket -> player_id con el que se anunció
last_connect_message = None

# Si el jugador 2 se cae a mitad de la partida, tiene este tiempo para volver.
# Pasado ese tiempo el relay no lo deja entrar hasta que empiece otra partida.
SEGUNDOS_PARA_VOLVER = float(os.environ.get("VENTANA_OPERADOR_S", "15"))
operador_perdido_en = None      # time.monotonic() de la caída del jugador 2, o None

# Un cliente que se queda sin Wi-Fi no avisa: el relay solo se entera porque no
# contesta los ping, y después espera a que cierre bien la conexión. Con 20 s +
# 20 s + 10 s (lo que trae la biblioteca) tardaba hasta 50 s en darlo por perdido,
# y en avisar a Unity y al jugador 2; con 5 s + 5 s + 2 s, hasta 12 s.
PING_INTERVALO_S = float(os.environ.get("PING_INTERVALO_S", "5"))
PING_TIMEOUT_S = float(os.environ.get("PING_TIMEOUT_S", "5"))
CIERRE_TIMEOUT_S = float(os.environ.get("CIERRE_TIMEOUT_S", "2"))

# El mensaje más grande del juego (task_list, mapa_puntos) pesa menos de 10 kB.
# Un tope bajo evita que un cliente malicioso o roto llene la memoria del relay
# con mensajes de 1 MB (el máximo que trae la biblioteca).
TAMANO_MAXIMO_BYTES = 64 * 1024

# El juego y el relay corren en la misma PC (iniciar.sh): un `unity_connect` que
# llega desde otra IP es alguien de la red haciéndose pasar por el juego, con lo
# que podría mandar `attack`, `game_over` o `task_list` falsos a la tablet.
# UNITY_DESDE_CUALQUIER_IP=1 lo permite, para montajes raros.
UNITY_DESDE_CUALQUIER_IP = os.environ.get("UNITY_DESDE_CUALQUIER_IP", "") == "1"

# El minimapa, las posiciones, el estado de los jugadores y los rechazos son
# solo para el jugador 2 (operador): la tablet no los usa, así que no se le
# reenvían.
SOLO_PARA_OPERADORES = {
    "estado_mapa", "mapa_puntos", "accion_rechazada",
    "posiciones", "estado_jugador", "estado_operador", "pong_operador",
    "tarea", "tarea_resultado",
}

# Lo que cada rol puede mandar a Unity. Unity no distingue quién le habla: sin
# este filtro un operador podía completar las tareas del jugador 1 o darle cuerda
# a la caja de Puppet, y una tablet colocar trampas. Lo demás se descarta.
PERMITIDO_DE_TABLET = {
    "connect", "reconnect", "disconnect",
    "task_completed", "task_failed",
    "dar_cuerda_inicio", "dar_cuerda_fin", "camara",
    "tutorial_abrir", "tutorial_slide", "tutorial_prueba_tarea", "tutorial_cerrar",
}
PERMITIDO_DE_OPERADOR = {
    "operador_connect", "operador_salir", "ping_operador",
    "accion_operador", "tarea_pedir", "tarea_respuesta",
}


async def send_to(clients, payload):
    """Manda `payload` a todos `clients` SIN esperar a ninguno.

    Antes cada envío esperaba (await client.send) a que el cliente aceptara el
    mensaje. Una tablet o un navegador atascados (Wi-Fi caído, app congelada)
    dejaban esperando a todo el relay: los mensajes de Unity para los demás se
    atrasaban, el relay dejaba de leer a Unity y, al no contestar esta los ping,
    la daba por perdida y le cerraba la conexión. Con `broadcast` el mensaje se
    encola en el momento y a un cliente lento lo saca el keepalive (ver
    PING_INTERVALO_S), sin frenar a nadie más.
    """
    destinos = list(clients)
    if destinos:
        broadcast(destinos, payload)


def olvidar_connect_si_no_hay_tablets():
    """El último `connect` solo sirve para un Unity que se abre cuando la
    tablet ya está conectada. Si la tablet se fue, reenviarlo haría que el
    juego arranque solo (o repita el tutorial) con una orden vieja."""
    global last_connect_message
    if not tablets:
        last_connect_message = None


def confirmar_de_inmediato(connection):
    """Pide a Linux que confirme (ACK) enseguida lo que acaba de recibir.

    Unity (ClientWebSocket de Mono) abre su socket con el algoritmo de Nagle
    encendido (TCP_NODELAY = 0): un mensaje pequeño espera a que llegue el ACK
    del anterior, y por defecto Linux demora ese ACK hasta 40 ms si no tiene
    nada que contestar. Con TCP_QUICKACK el ACK sale al instante. Linux lo
    apaga solo después de unos paquetes, por eso se vuelve a pedir con cada
    mensaje. Si no se puede (otro sistema), no pasa nada."""
    try:
        connection.transport.get_extra_info("socket").setsockopt(
            red.IPPROTO_TCP, red.TCP_QUICKACK, 1)
    except (AttributeError, OSError):
        pass


def es_de_esta_pc(connection):
    """True si el cliente se conectó desde esta misma PC (loopback)."""
    try:
        direccion = connection.remote_address[0]
    except (AttributeError, IndexError, TypeError):
        return False
    return direccion in ("::1",) or direccion.startswith("127.") or direccion.startswith("::ffff:127.")


def aviso(tipo, **campos):
    return json.dumps({"type": tipo, **campos})


async def rechazar(socket, motivo):
    await socket.send(aviso("conexion_rechazada", motivo=motivo))
    await socket.close()


def otra_tablet_jugando(socket, player_id):
    """True si hay otra tablet conectada con un `player_id` distinto: mientras
    el jugador 1 juega, nadie más puede mandar un `connect` (reiniciaría su noche
    o, con modo «salir», cerraría el juego)."""
    return any(otro is not socket and identidad_de_tablet.get(otro) != player_id for otro in tablets)


def soltar_conexiones_viejas(socket, player_id):
    """La misma tablet volvió por otra conexión (se quedó sin Wi-Fi y regresó,
    o la app se cerró sin despedirse): la conexión anterior se cierra ya, sin
    esperar los 12 s del keepalive, y sin que cuente como que el jugador 1 se fue."""
    for viejo in list(tablets):
        if viejo is not socket and identidad_de_tablet.get(viejo) == player_id:
            tablets.discard(viejo)
            identidad_de_tablet.pop(viejo, None)
            asyncio.ensure_future(viejo.close(code=1000, reason="la tablet volvió por otra conexión"))


async def handle_connection(socket):
    global last_connect_message, operador_perdido_en
    role = None
    try:
        async for raw in socket:
            confirmar_de_inmediato(socket)
            try:
                message = json.loads(raw)
            except (TypeError, json.JSONDecodeError):
                continue
            if not isinstance(message, dict):
                continue

            message_type = message.get("type")
            if message_type == "unity_connect":
                if role is not None:
                    continue                      # una tablet o un operador no pasan a ser Unity
                if not UNITY_DESDE_CUALQUIER_IP and not es_de_esta_pc(socket):
                    print("Rechazado un unity_connect desde otra IP:", socket.remote_address)
                    await rechazar(socket, "unity_solo_local")
                    return
                role = "unity"
                unity_clients.add(socket)
                if last_connect_message is not None:
                    await socket.send(last_connect_message)
                print("Unity conectado")
                continue

            if message_type == "operador_connect":
                # Jugador 2: ve el mapa y manda acciones a Unity. No es una
                # tablet: su anuncio no debe guardarse como `connect`, porque
                # reiniciaría la partida de un Unity que se abre después.
                if role not in (None, "operador"):
                    continue
                if (operador_perdido_en is not None
                        and time.monotonic() - operador_perdido_en > SEGUNDOS_PARA_VOLVER):
                    await socket.send(aviso("operador_rechazado", motivo="tiempo_agotado"))
                    await socket.close()
                    print("Operador rechazado: pasó el tiempo para volver")
                    return
                operador_perdido_en = None
                role = "operador"
                operadores.add(socket)
                await send_to(unity_clients, raw)
                print("Operador conectado")
                continue

            if message_type in ("connect", "reconnect"):
                if role not in (None, "tablet"):
                    continue                      # un operador no pasa a ser tablet
                player_id = message.get("player_id")
                if otra_tablet_jugando(socket, player_id):
                    print("Rechazada otra tablet mientras el jugador 1 juega:", player_id)
                    await rechazar(socket, "ya_hay_jugador1")
                    return
                soltar_conexiones_viejas(socket, player_id)
                role = "tablet"
                tablets.add(socket)
                identidad_de_tablet[socket] = player_id
                if message_type == "reconnect":
                    # La tablet perdió la conexión y volvió: Unity retoma la noche en
                    # curso (con `connect` la reiniciaría). No se toca el `connect`
                    # guardado ni la ventana del jugador 2.
                    await send_to(unity_clients, raw)
                    await send_to(operadores, aviso("jugador1_volvio"))
                    print("Flutter reconectado; Unity retoma la noche en curso")
                else:
                    operador_perdido_en = None      # partida nueva: el jugador 2 puede entrar de nuevo
                    last_connect_message = raw
                    await send_to(unity_clients, raw)
                    print("Flutter conectado; inicio de partida enviado a Unity")
                continue

            if role == "unity":
                if message_type == "game_over":
                    operador_perdido_en = None  # entre partidas nadie queda fuera
                if message_type not in SOLO_PARA_OPERADORES:
                    await send_to(tablets, raw)
                await send_to(operadores, raw)
            elif role == "tablet":
                if message_type not in PERMITIDO_DE_TABLET:
                    continue
                await send_to(unity_clients, raw)
                if message_type == "disconnect":
                    tablets.discard(socket)
                    identidad_de_tablet.pop(socket, None)
                    olvidar_connect_si_no_hay_tablets()
                    role = None
                    await send_to(operadores, aviso("jugador1_salio", motivo="salio"))
            elif role == "operador":
                if message_type not in PERMITIDO_DE_OPERADOR:
                    continue
                if message_type == "operador_salir":
                    # El jugador 2 se va a propósito: no cuenta como caída.
                    operadores.discard(socket)
                    operador_perdido_en = None
                    role = None
                    continue
                await send_to(unity_clients, raw)
    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        era_tablet = socket in tablets
        era_unity = socket in unity_clients
        era_operador = socket in operadores
        tablets.discard(socket)
        identidad_de_tablet.pop(socket, None)
        unity_clients.discard(socket)
        operadores.discard(socket)
        olvidar_connect_si_no_hay_tablets()
        if era_operador:
            operador_perdido_en = time.monotonic()
        if era_tablet:
            await send_to(operadores, aviso("jugador1_salio", motivo="conexion_perdida"))
            # Unity tiene que soltar lo que la tablet dejó apretado (la cuerda
            # de la caja de Puppet, la cámara): nadie le va a mandar el «soltar».
            await send_to(unity_clients, aviso("tablet_caida"))
        if era_unity:
            await send_to(operadores, aviso("juego_cerrado"))
        print("Cliente desconectado")


async def main():
    async with websockets.serve(
        handle_connection,
        HOST,
        PORT,
        ping_interval=PING_INTERVALO_S,
        ping_timeout=PING_TIMEOUT_S,
        close_timeout=CIERRE_TIMEOUT_S,
        # Sin permessage-deflate: el navegador y la tablet lo piden, y comprimir
        # cada uno de los 30 mensajes por segundo cuesta CPU y tiempo para
        # ahorrar unos bytes en una red local.
        compression=None,
        max_size=TAMANO_MAXIMO_BYTES,
    ):
        print(f"Relay Unity activo en ws://{HOST}:{PORT}")
        await asyncio.Future()


if __name__ == "__main__":
    asyncio.run(main())
