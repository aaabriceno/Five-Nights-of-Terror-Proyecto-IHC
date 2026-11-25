"""Recorta los jumpscares para que el animatrónico aparezca ya en la cara (2026-11-24).

Cada video original (1,5 s, 45 cuadros a 30 fps) tenía ~0,6 s del animatrónico
acercándose por el pasillo antes de llenar la pantalla, mientras el grito ya
sonaba a todo volumen. El recorte:
  1. empieza 2-3 cuadros antes de que la cara llene la pantalla (un «salto» de
     0,1 s hacia la cámara),
  2. sigue con la cara temblando hasta el final del tramo útil,
  3. y repite ese tramo de ida y vuelta hasta volver a 45 cuadros (1,5 s), para
     que la cara siga en pantalla mientras dura el grito (1,6 s) sin tocar el
     código de Unity, la tablet ni la página del jugador 2.

También saca la imagen de respaldo de cada animatrónico (`jumpscare_<a>.jpg`):
el cuadro 3 del recorte, el primero con la cara llena. Se ve al instante,
antes de que cargue el video, y queda si el video no se puede reproducir.

Mismo formato que los originales: WebM/VP8, 1280x720, 30 fps, sin audio.

OJO: hay que recortar los ORIGINALES, no los ya recortados. Están en la
historia de git (antes del 2026-11-24):
    git show 5d23eaf:Unity/Assets/StreamingAssets/jumpscares/jumpscare_freddy.webm > originales/jumpscare_freddy.webm
    (ídem vixy y puppet)

Uso: python3 docs/jumpscares/recortar_jumpscares.py <carpeta_originales> <carpeta_salida>
Después se copian los .webm a Unity/Assets/StreamingAssets/jumpscares/ y a
Flutter/assets/videos/, y los .jpg a Unity/Assets/Resources/ y a
Flutter/assets/images/ (la de Freddy además como desktop_jumpscare.jpg y
jumpscare.jpg, el respaldo genérico).
"""
import subprocess
import sys
import tempfile
from pathlib import Path

CUADROS = 45
CUADRO_DE_LA_IMAGEN = 3
# animatrónico: (primer cuadro del salto, primer cuadro con la cara ya estable, último cuadro útil)
PLAN = {
    "freddy": (18, 22, 39),   # 40-44 son negros y un destello del pasillo
    "vixy": (19, 20, 44),     # generar_jumpscare_vixy.py ya la hace saltar de golpe en el 19
    "puppet": (23, 26, 43),   # 44 es negro
}


def secuencia(salto, estable, ultimo):
    """Índices de los 45 cuadros: salto → cara → ida y vuelta sobre el tramo estable."""
    indices = list(range(salto, ultimo + 1))
    ida_y_vuelta = list(range(ultimo - 1, estable - 1, -1)) + list(range(estable + 1, ultimo + 1))
    while len(indices) < CUADROS:
        indices.extend(ida_y_vuelta)
    return indices[:CUADROS]


def recortar(original, salida, plan):
    with tempfile.TemporaryDirectory() as tmp:
        subprocess.run(["ffmpeg", "-v", "error", "-i", str(original), f"{tmp}/c_%03d.png"], check=True)
        lista = Path(tmp) / "lista.txt"
        lista.write_text("".join(f"file '{tmp}/c_{i + 1:03d}.png'\nduration {1 / 30:.6f}\n"
                                 for i in secuencia(*plan)))
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", str(lista),
                        "-vf", "fps=30,format=yuv420p", "-frames:v", str(CUADROS), "-an",
                        "-c:v", "libvpx", "-b:v", "2M", "-crf", "10", "-auto-alt-ref", "0", str(salida)],
                       check=True)


def sacar_imagen(video, salida):
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(video),
                    "-vf", f"select=eq(n\\,{CUADRO_DE_LA_IMAGEN})", "-frames:v", "1", "-q:v", "3", str(salida)],
                   check=True)


if __name__ == "__main__":
    origen, destino = Path(sys.argv[1]), Path(sys.argv[2])
    destino.mkdir(parents=True, exist_ok=True)
    for animatronico, plan in PLAN.items():
        video = destino / f"jumpscare_{animatronico}.webm"
        recortar(origen / video.name, video, plan)
        sacar_imagen(video, destino / f"jumpscare_{animatronico}.jpg")
        print(video, secuencia(*plan))
