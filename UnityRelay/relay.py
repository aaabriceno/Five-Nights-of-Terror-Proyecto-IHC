"""Small WebSocket relay for the Unity-authoritative integration.

Run this instead of the original game server. It forwards messages only and
does not import or change the original backend.
"""

import asyncio
import json
import os
import time
import websockets

HOST = "0.0.0.0"
PORT = 8000

tablets = set()
unity_clients = set()
operadores = set()
last_connect_message = None

# Si el jugador 2 se cae a mitad de la partida, tiene este tiempo para volver.
# Pasado ese tiempo el relay no lo deja entrar hasta que empiece otra partida.
SEGUNDOS_PARA_VOLVER = float(os.environ.get("VENTANA_OPERADOR_S", "15"))
operador_perdido_en = None      # time.monotonic() de la caída del jugador 2, o None

# El minimapa, las posiciones, el estado de los jugadores y los rechazos son
# solo para el jugador 2 (operador): la tablet no los usa, así que no se le
# reenvían.
SOLO_PARA_OPERADORES = {
    "estado_mapa", "mapa_puntos", "accion_rechazada",
    "posiciones", "estado_jugador", "estado_operador", "pong_operador",
}


async def send_to(clients, payload):
    client_list = list(clients)
    if not client_list:
        return
    results = await asyncio.gather(
        *(client.send(payload) for client in client_list),
        return_exceptions=True,
    )
    for client, result in zip(client_list, results):
        if isinstance(result, Exception):
            tablets.discard(client)
            unity_clients.discard(client)
            operadores.discard(client)


def olvidar_connect_si_no_hay_tablets():
    """El último `connect` solo sirve para un Unity que se abre cuando la
    tablet ya está conectada. Si la tablet se fue, reenviarlo haría que el
    juego arranque solo (o repita el tutorial) con una orden vieja."""
    global last_connect_message
    if not tablets:
        last_connect_message = None


def aviso(tipo, **campos):
    return json.dumps({"type": tipo, **campos})


async def handle_connection(socket):
    global last_connect_message, operador_perdido_en
    role = None
    try:
        async for raw in socket:
            try:
                message = json.loads(raw)
            except (TypeError, json.JSONDecodeError):
                continue

            message_type = message.get("type")
            if message_type == "unity_connect":
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

            if message_type == "operador_salir":
                # El jugador 2 se va a propósito: no cuenta como caída.
                operadores.discard(socket)
                operador_perdido_en = None
                role = None
                continue

            if message_type == "connect":
                role = "tablet"
                tablets.add(socket)
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
            elif role in ("tablet", "operador"):
                await send_to(unity_clients, raw)

            if message_type == "disconnect":
                era_tablet = socket in tablets
                tablets.discard(socket)
                olvidar_connect_si_no_hay_tablets()
                role = None
                if era_tablet:
                    await send_to(operadores, aviso("jugador1_salio", motivo="salio"))
    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        era_tablet = socket in tablets
        era_unity = socket in unity_clients
        era_operador = socket in operadores
        tablets.discard(socket)
        unity_clients.discard(socket)
        operadores.discard(socket)
        olvidar_connect_si_no_hay_tablets()
        if era_operador:
            operador_perdido_en = time.monotonic()
        if era_tablet:
            await send_to(operadores, aviso("jugador1_salio", motivo="conexion_perdida"))
        if era_unity:
            await send_to(operadores, aviso("juego_cerrado"))
        print("Cliente desconectado")


async def main():
    async with websockets.serve(
        handle_connection,
        HOST,
        PORT,
        ping_interval=20,
        ping_timeout=20,
    ):
        print(f"Relay Unity activo en ws://{HOST}:{PORT}")
        await asyncio.Future()


if __name__ == "__main__":
    asyncio.run(main())
