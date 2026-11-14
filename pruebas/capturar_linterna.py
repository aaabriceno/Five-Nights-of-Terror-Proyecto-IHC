"""Fotos de la linterna con el juego de verdad, en una pantalla virtual (Xvfb), para
revisar que se ve desde la oficina:

  linterna_apagada.png    Freddy en el pasillo del frente (P06), la linterna apagada
  linterna_prendida.png   la linterna prendida sobre Freddy (antes de los 2 s)
  linterna_huye.png       Freddy retrocediendo por el pasillo, todavía alumbrado

Usa la `Partida` de probar_vixy_y_puppet_con_juego.py (noche larga: Freddy tarda
~45 s en llegar a P06).

Uso: UnityRelay/.venv/bin/python pruebas/capturar_linterna.py <carpeta>
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


async def mantener_apretada(p, segundos):
    for _ in range(int(segundos / 0.15)):
        await p.tablet.send(json.dumps({"type": "linterna", "encendida": True}))
        await asyncio.sleep(0.15)


async def principal(carpeta):
    carpeta.mkdir(parents=True, exist_ok=True)
    p = juego.Partida(["-prueba-segundos-noche", "400"])
    try:
        await p.abrir()
        await p.tablet.send(json.dumps({"type": "dar_cuerda_inicio"}))   # Puppet quieto en su caja
        await p.esperar(lambda: any(n == "P06" for n, _ in p.visitas["freddy"]), 220)
        await asyncio.sleep(1.0)
        foto(carpeta, "linterna_apagada.png")
        await mantener_apretada(p, 1.2)
        foto(carpeta, "linterna_prendida.png")
        await mantener_apretada(p, 2.4)
        foto(carpeta, "linterna_huye.png")
        await p.tablet.send(json.dumps({"type": "linterna", "encendida": False}))
        print("estado de la oficina:", p.oficina_ahora())
    finally:
        await p.cerrar()


if __name__ == "__main__":
    asyncio.run(principal(Path(sys.argv[1] if len(sys.argv) > 1 else "capturas_linterna")))
