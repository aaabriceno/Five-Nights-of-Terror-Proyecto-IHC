"""Small WebSocket relay for the Unity-authoritative integration.

Run this instead of the original game server. It forwards messages only and
does not import or change the original backend.
"""

import asyncio
import json
import websockets

HOST = "0.0.0.0"
PORT = 8000

tablets = set()
unity_clients = set()
last_connect_message = None


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


async def handle_connection(socket):
    global last_connect_message
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

            if message_type == "connect":
                role = "tablet"
                tablets.add(socket)
                last_connect_message = raw
                await send_to(unity_clients, raw)
                print("Flutter conectado; inicio de partida enviado a Unity")
                continue

            if role == "unity":
                await send_to(tablets, raw)
            elif role == "tablet":
                await send_to(unity_clients, raw)

            if message_type == "disconnect":
                tablets.discard(socket)
                role = None
    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        tablets.discard(socket)
        unity_clients.discard(socket)
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
