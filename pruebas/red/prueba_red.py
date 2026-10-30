"""Prueba de red entre dos computadoras a través del relay del juego.

Mide cuánto tarda un mensaje en ir de una computadora al relay, pasar a la otra
y volver, con el mismo canal que usa el juego (WebSocket sobre TCP, puerto 8000).

Se usan dos papeles, con los mensajes que el relay ya conoce:
  eco    Se conecta como si fuera la tablet (`connect`), muestra el chat, acusa
         recibo (y deja escribir en la terminal) y devuelve cada
         `prueba_ping` como `prueba_pong`. Se deja corriendo en la PC del relay.
  medir  Se conecta como si fuera Unity (`unity_connect`), manda pings y mide
         el viaje de ida y vuelta (RTT). Se corre en la OTRA computadora.

IMPORTANTE: hacerlo con el juego cerrado. `eco` manda un `connect` que Unity
interpretaría como "inicio de partida".

Uso:
    python3 prueba_red.py eco   [--host 127.0.0.1] [--puerto 8000]
    python3 prueba_red.py medir --host 192.168.1.7 [--cantidad 200] [--bytes 300]

Requisito: `pip install websockets` (el venv de UnityRelay ya lo trae).
"""
import argparse
import asyncio
import json
import socket as red
import statistics
import sys
import time

import websockets


def url(host, puerto):
    return f"ws://{host}:{puerto}"


async def escribir_desde_teclado(socket, nombre):
    """Cada línea escrita en la terminal sale como mensaje de chat."""
    bucle = asyncio.get_running_loop()
    contador = 0
    while True:
        linea = await bucle.run_in_executor(None, sys.stdin.readline)
        if not linea:           # sin teclado (EOF): solo se escucha
            return
        if linea.strip():
            contador += 1
            await socket.send(json.dumps({"type": "prueba_chat", "id": f"eco-{contador}",
                                          "de": nombre, "texto": linea.strip()}))


async def hacer_eco(host, puerto):
    """Devuelve cada ping como pong, muestra el chat y acusa recibo."""
    nombre = f"Laptop terminal ({red.gethostname()})"
    async with websockets.connect(url(host, puerto)) as socket:
        await socket.send(json.dumps({"type": "connect", "device": "prueba_red"}))
        print(f"Eco conectado a {url(host, puerto)}. Escribe y Enter para chatear; Ctrl+C para terminar.")
        escritor = asyncio.create_task(escribir_desde_teclado(socket, nombre))
        recibidos = 0
        async for crudo in socket:
            try:
                mensaje = json.loads(crudo)
            except json.JSONDecodeError:
                continue
            tipo = mensaje.get("type")
            if tipo == "prueba_ping":
                mensaje["type"] = "prueba_pong"
                await socket.send(json.dumps(mensaje))
                recibidos += 1
                if recibidos % 50 == 0:
                    print(f"  {recibidos} pings devueltos")
            elif tipo == "prueba_chat":
                print(f"[{time.strftime('%H:%M:%S')}] {mensaje.get('de')}: {mensaje.get('texto')}")
                await socket.send(json.dumps({"type": "prueba_chat_ack", "id": mensaje.get("id"), "de": nombre}))
            elif tipo == "prueba_chat_ack":
                print(f"  ✓ recibido por {mensaje.get('de')}")
        escritor.cancel()


def percentil(ordenados, fraccion):
    return ordenados[min(len(ordenados) - 1, int(len(ordenados) * fraccion))]


def resumir(rtts_ms, enviados):
    """Estadísticas de una tanda: pérdida y RTT en milisegundos."""
    perdidos = enviados - len(rtts_ms)
    resumen = {"enviados": enviados, "recibidos": len(rtts_ms), "perdidos": perdidos,
               "perdida_pct": 100.0 * perdidos / enviados if enviados else 0.0}
    if rtts_ms:
        ordenados = sorted(rtts_ms)
        resumen.update({
            "min": ordenados[0],
            "media": statistics.fmean(ordenados),
            "mediana": statistics.median(ordenados),
            "p95": percentil(ordenados, 0.95),
            "max": ordenados[-1],
            "jitter": statistics.pstdev(ordenados),
        })
    return resumen


async def medir(host, puerto, cantidad, intervalo, tamano):
    inicio = time.perf_counter()
    try:
        socket = await websockets.connect(url(host, puerto), open_timeout=5)
    except Exception as error:
        print(f"No se pudo conectar a {url(host, puerto)}: {error}")
        print("Revisa: ¿el relay está corriendo? ¿la IP es la correcta? ¿el firewall deja el puerto?")
        return 1
    conexion_ms = (time.perf_counter() - inicio) * 1000
    await socket.send(json.dumps({"type": "unity_connect"}))
    print(f"Conectado a {url(host, puerto)} en {conexion_ms:.1f} ms. Enviando {cantidad} pings de ~{tamano} bytes…")

    enviados_en = {}
    rtts = []

    async def recibir():
        async for crudo in socket:
            ahora = time.perf_counter()
            try:
                mensaje = json.loads(crudo)
            except json.JSONDecodeError:
                continue
            if mensaje.get("type") == "prueba_pong" and mensaje.get("n") in enviados_en:
                rtts.append((ahora - enviados_en.pop(mensaje["n"])) * 1000)

    lector = asyncio.create_task(recibir())
    relleno = "x" * tamano
    for n in range(cantidad):
        enviados_en[n] = time.perf_counter()
        await socket.send(json.dumps({"type": "prueba_ping", "n": n, "relleno": relleno}))
        await asyncio.sleep(intervalo)
    await asyncio.sleep(1.5)  # espera los últimos pongs
    lector.cancel()
    await socket.close()

    r = resumir(rtts, cantidad)
    print(f"\nEnviados {r['enviados']} · recibidos {r['recibidos']} · perdidos {r['perdidos']} ({r['perdida_pct']:.1f} %)")
    if not rtts:
        print("No llegó ningún pong: ¿está corriendo `eco` en la otra computadora?")
        return 1
    print(f"RTT (ms): mín {r['min']:.1f} · media {r['media']:.1f} · mediana {r['mediana']:.1f} · "
          f"p95 {r['p95']:.1f} · máx {r['max']:.1f} · jitter {r['jitter']:.1f}")
    print(f"Un sentido ≈ RTT/2 = {r['mediana'] / 2:.1f} ms (mediana). Tick del juego: 500 ms.")
    return 0


def principal():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("modo", choices=["eco", "medir"])
    parser.add_argument("--host", default="127.0.0.1", help="IP de la PC donde corre el relay")
    parser.add_argument("--puerto", type=int, default=8000)
    parser.add_argument("--cantidad", type=int, default=200, help="pings a enviar (medir)")
    parser.add_argument("--intervalo", type=float, default=0.05, help="segundos entre pings (medir)")
    parser.add_argument("--bytes", type=int, default=300, dest="tamano", help="relleno de cada ping (medir)")
    a = parser.parse_args()
    try:
        if a.modo == "eco":
            asyncio.run(hacer_eco(a.host, a.puerto))
            return 0
        return asyncio.run(medir(a.host, a.puerto, a.cantidad, a.intervalo, a.tamano))
    except KeyboardInterrupt:
        return 0


if __name__ == "__main__":
    sys.exit(principal())
