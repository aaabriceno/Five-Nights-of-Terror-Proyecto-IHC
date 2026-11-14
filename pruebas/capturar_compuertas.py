"""Fotos de las luces y las compuertas de los ductos con el juego de verdad, en una
pantalla virtual (Xvfb), para revisar que se ven desde la oficina:

  ductos_inicio.png         al empezar: las dos lámparas en verde
  ductos_rojo_cerrada.png   Vixy al final del ducto (luz izquierda en rojo) y su compuerta cerrada
  ductos_girando.png        la cámara girada a la izquierda, mirando la compuerta cerrada

Usa la `Partida` de probar_vixy_y_puppet_con_juego.py (sin Freddy y con tareas
cada segundo, para que Vixy llegue rápido).

Uso: UnityRelay/.venv/bin/python pruebas/capturar_compuertas.py <carpeta>
"""
import asyncio
import json
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import probar_vixy_y_puppet_con_juego as juego  # noqa: E402


def foto(carpeta, nombre):
    subprocess.run(["import", "-display", juego.PANTALLA, "-window", "root", str(carpeta / nombre)], check=False)
    print("captura:", carpeta / nombre)


async def principal(carpeta):
    carpeta.mkdir(parents=True, exist_ok=True)
    p = juego.Partida(["-prueba-sin-freddy", "-prueba-tareas-cada", "1"])
    try:
        await p.abrir()
        await p.tablet.send(json.dumps({"type": "dar_cuerda_inicio"}))   # Puppet quieto en su caja
        await asyncio.sleep(4)
        foto(carpeta, "ductos_inicio.png")
        await p.esperar(lambda: p.oficina_ahora().get("luz_izquierda") == "rojo", 70)
        await p.compuerta("izquierda", True)
        await asyncio.sleep(1.5)
        foto(carpeta, "ductos_rojo_cerrada.png")
        for _ in range(14):                                             # como el dedo apoyado ~2 s
            await p.tablet.send(json.dumps({"type": "camara", "direccion": -1}))
            await asyncio.sleep(0.15)
        foto(carpeta, "ductos_girando.png")
        print("estado de la oficina:", p.oficina_ahora())
    finally:
        await p.cerrar()


if __name__ == "__main__":
    asyncio.run(principal(Path(sys.argv[1] if len(sys.argv) > 1 else "capturas_ductos")))
