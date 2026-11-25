"""Prueba del servidor de la página del jugador 2 (UnityRelay/servir_pagina.py).

Comprueba que sirve la página sin caché y que, bajo /sustos/, entrega solo los
videos, la imagen y el grito del jumpscare, tomados de las carpetas de Unity
(2026-11-24). Cualquier otro nombre bajo /sustos/ debe dar 404.

Uso: UnityRelay/.venv/bin/python pruebas/probar_servir_pagina.py
"""
import http.server
import sys
import threading
import urllib.error
import urllib.request
from functools import partial
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(RAIZ / "UnityRelay"))
import servir_pagina  # noqa: E402

JUMPSCARES = RAIZ / "Unity" / "Assets" / "StreamingAssets" / "jumpscares"
RECURSOS = RAIZ / "Unity" / "Assets" / "Resources"
fallos = 0


def comprobar(descripcion, condicion):
    global fallos
    print(("  ok    " if condicion else "  FALLA ") + descripcion)
    if not condicion:
        fallos += 1


def pedir(puerto, ruta):
    """(código, cabeceras, cuerpo) de un GET; los errores HTTP también vuelven como código."""
    try:
        with urllib.request.urlopen(f"http://127.0.0.1:{puerto}{ruta}", timeout=5) as r:
            return r.status, r.headers, r.read()
    except urllib.error.HTTPError as error:
        return error.code, error.headers, b""


def principal():
    manejador = partial(servir_pagina.SinCache, directory=str(RAIZ / "UnityRelay" / "operador"))
    servidor = http.server.ThreadingHTTPServer(("127.0.0.1", 0), manejador)
    puerto = servidor.server_address[1]
    threading.Thread(target=servidor.serve_forever, daemon=True).start()
    try:
        codigo, cabeceras, _ = pedir(puerto, "/operador.html")
        comprobar("la página se sirve sin caché", codigo == 200 and cabeceras.get("Cache-Control") == "no-store")

        for animatronico in ("freddy", "vixy", "puppet"):
            archivo = f"jumpscare_{animatronico}.webm"
            codigo, cabeceras, cuerpo = pedir(puerto, f"/sustos/{archivo}")
            comprobar(f"/sustos/{archivo} es el video de Unity, como video/webm",
                      codigo == 200 and cabeceras.get("Content-Type") == "video/webm"
                      and cuerpo == (JUMPSCARES / archivo).read_bytes())

        codigo, cabeceras, cuerpo = pedir(puerto, "/sustos/desktop_jumpscare.ogg")
        comprobar("el grito se sirve como audio/ogg",
                  codigo == 200 and cabeceras.get("Content-Type") == "audio/ogg"
                  and cuerpo == (RECURSOS / "desktop_jumpscare.ogg").read_bytes())
        codigo, cabeceras, cuerpo = pedir(puerto, "/sustos/desktop_jumpscare.jpg")
        comprobar("la imagen fija se sirve como image/jpeg",
                  codigo == 200 and cabeceras.get("Content-Type") == "image/jpeg"
                  and cuerpo == (RECURSOS / "desktop_jumpscare.jpg").read_bytes())
        for animatronico in ("freddy", "vixy", "puppet"):
            archivo = f"jumpscare_{animatronico}.jpg"
            codigo, cabeceras, cuerpo = pedir(puerto, f"/sustos/{archivo}")
            comprobar(f"/sustos/{archivo} es la cara de respaldo de Unity, como image/jpeg",
                      codigo == 200 and cabeceras.get("Content-Type") == "image/jpeg"
                      and cuerpo == (RECURSOS / archivo).read_bytes())
        codigo, _, _ = pedir(puerto, "/sustos/jumpscare_freddy.webm?v=1")
        comprobar("una consulta (?v=1) no cambia el archivo", codigo == 200)

        for ruta in ("/sustos/otro.webm", "/sustos/../servir_pagina.py", "/sustos/%2e%2e/relay.py",
                     "/sustos/", "/sustos/jumpscare_freddy.webm.meta", "/sustos/Jumpscare_Freddy.webm"):
            codigo, _, _ = pedir(puerto, ruta)
            comprobar(f"{ruta} no se sirve (404)", codigo == 404)
    finally:
        servidor.shutdown()
    print()
    print("Todas las pruebas pasaron." if fallos == 0 else f"{fallos} prueba(s) fallaron.")
    return 1 if fallos else 0


if __name__ == "__main__":
    raise SystemExit(principal())
