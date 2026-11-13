"""Sirve la página del jugador 2 (carpeta operador/) sin caché.

`python -m http.server` manda Last-Modified pero no Cache-Control, y el
navegador guarda operador.js y logica.js por su cuenta: después de actualizar
el juego, el jugador 2 seguía viendo la versión vieja hasta forzar la recarga.
Con `Cache-Control: no-store` cada visita pide los archivos de nuevo (son
pocos kB en una red local).

Uso: python servir_pagina.py <puerto> <carpeta>
"""
import functools
import http.server
import sys


class SinCache(http.server.SimpleHTTPRequestHandler):
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
