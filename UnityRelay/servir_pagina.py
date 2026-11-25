"""Sirve la página del jugador 2 (carpeta operador/) sin caché.

`python -m http.server` manda Last-Modified pero no Cache-Control, y el
navegador guarda operador.js y logica.js por su cuenta: después de actualizar
el juego, el jugador 2 seguía viendo la versión vieja hasta forzar la recarga.
Con `Cache-Control: no-store` cada visita pide los archivos de nuevo (son
pocos kB en una red local).

Bajo /sustos/ sirve los medios del jumpscare (2026-11-24), para que el jugador
2 también lo vea: los videos, la imagen y el grito se toman de las carpetas de
Unity, así que cambiar un video ahí lo cambia en las dos pantallas. Solo se
sirven los nombres de ARCHIVOS_DE_SUSTO; cualquier otro da 404.

Uso: python servir_pagina.py <puerto> <carpeta>
"""
import functools
import http.server
import sys
import urllib.parse
from pathlib import Path

UNITY_ASSETS = Path(__file__).resolve().parent.parent / "Unity" / "Assets"
ARCHIVOS_DE_SUSTO = {
    **{f"jumpscare_{nombre}.webm": UNITY_ASSETS / "StreamingAssets" / "jumpscares" / f"jumpscare_{nombre}.webm"
       for nombre in ("freddy", "vixy", "puppet")},
    # La cara de cada animatrónico: se ve al instante y queda si el video falla.
    **{f"jumpscare_{nombre}.jpg": UNITY_ASSETS / "Resources" / f"jumpscare_{nombre}.jpg"
       for nombre in ("freddy", "vixy", "puppet")},
    "desktop_jumpscare.ogg": UNITY_ASSETS / "Resources" / "desktop_jumpscare.ogg",
    "desktop_jumpscare.jpg": UNITY_ASSETS / "Resources" / "desktop_jumpscare.jpg",
}
PREFIJO_SUSTOS = "/sustos/"


class SinCache(http.server.SimpleHTTPRequestHandler):
    extensions_map = {
        **http.server.SimpleHTTPRequestHandler.extensions_map,
        ".webm": "video/webm",
        ".ogg": "audio/ogg",
        ".jpg": "image/jpeg",
    }

    def translate_path(self, path):
        ruta = urllib.parse.unquote(urllib.parse.urlsplit(path).path)
        if ruta.startswith(PREFIJO_SUSTOS):
            archivo = ARCHIVOS_DE_SUSTO.get(ruta[len(PREFIJO_SUSTOS):])
            # Un nombre fuera de la lista apunta a algo que no existe: 404.
            return str(archivo) if archivo else str(UNITY_ASSETS / "no-existe" / "no-existe")
        return super().translate_path(path)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, formato, *argumentos):
        pass        # sin una línea por petición: iniciar.sh no la muestra


def main():
    puerto = int(sys.argv[1]) if len(sys.argv) > 1 else 8081
    carpeta = sys.argv[2] if len(sys.argv) > 2 else "."
    manejador = functools.partial(SinCache, directory=carpeta)
    with http.server.ThreadingHTTPServer(("0.0.0.0", puerto), manejador) as servidor:
        servidor.serve_forever()


if __name__ == "__main__":
    main()
