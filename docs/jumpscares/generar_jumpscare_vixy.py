"""Genera el jumpscare de Vixy (sin inteligencia artificial).

Toma la imagen de espera del juego (Unity/Assets/Resources/pantalla_espera_Juego.jpeg),
recorta a Vixy y la anima cuadro por cuadro: empieza lejos y casi a oscuras, y de golpe
se acerca hasta que su cabeza y sus ojos rojos llenan la pantalla, con temblor de cámara,
destellos de luz, brillo en los ojos, grano de película y viñeta.

El resultado es un video .webm (VP8, 1280x720, 30 fps, 1,5 s, sin audio), igual que los
de Freddy y Puppet, para copiar a Unity/Assets/StreamingAssets/jumpscares/.

Requisitos: Python 3, Pillow, numpy y ffmpeg con el codificador libvpx.

Uso (desde la raíz del repositorio):
    python3 docs/jumpscares/generar_jumpscare_vixy.py
    python3 docs/jumpscares/generar_jumpscare_vixy.py ruta/de/salida.webm

Sin argumentos escribe Unity/Assets/StreamingAssets/jumpscares/jumpscare_vixy.webm.
La semilla es fija, así que el resultado es el mismo en cada ejecución.
"""
import random
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

RAIZ = Path(__file__).resolve().parents[2]
IMAGEN_FUENTE = RAIZ / "Unity/Assets/Resources/pantalla_espera_Juego.jpeg"
SALIDA_POR_DEFECTO = RAIZ / "Unity/Assets/StreamingAssets/jumpscares/jumpscare_vixy.webm"

ANCHO, ALTO, FPS = 1280, 720, 30
CUADROS = 45                       # 1,5 s a 30 fps
SEMILLA = 7

# Recorte de la imagen de espera donde está Vixy, y centro de su cabeza dentro del recorte.
RECORTE_X, RECORTE_Y, RECORTE_ANCHO, RECORTE_ALTO = 1000, 480, 760, 980
CABEZA_X, CABEZA_Y = 303, 103
LADO_DEL_LIENZO = 2200             # lienzo negro grande para poder alejar la vista

# Ancho de la ventana (en píxeles de la imagen) que se ve en cada momento:
# lejos al empezar, cerca al final, que es lo que hace crecer a Vixy.
VENTANA_LEJOS, VENTANA_CERCA = 1500.0, 330.0
CUADRO_DONDE_EMPIEZA_EL_SALTO, CUADRO_DONDE_TERMINA_EL_SALTO = 11, 19


def preparar_lienzo():
    """Vixy con los bordes fundidos a negro, centrada en un lienzo grande.

    Así, al alejar la vista no aparecen Freddy ni Puppet ni el borde del recorte.
    """
    fuente = Image.open(IMAGEN_FUENTE).convert("RGB")
    recorte = fuente.crop((RECORTE_X, RECORTE_Y,
                           RECORTE_X + RECORTE_ANCHO, RECORTE_Y + RECORTE_ALTO))
    yy, xx = np.mgrid[0:RECORTE_ALTO, 0:RECORTE_ANCHO]
    distancia = np.sqrt(((xx - 303) / 330.0) ** 2 + ((yy - 380) / 520.0) ** 2)
    opacidad = np.clip(1.4 - distancia * 1.5, 0, 1)
    mascara = Image.fromarray((opacidad * 255).astype(np.uint8))
    negro = Image.new("RGB", (RECORTE_ANCHO, RECORTE_ALTO), (0, 0, 0))
    vixy = Image.composite(recorte, negro, mascara)

    lienzo = Image.new("RGB", (LADO_DEL_LIENZO, LADO_DEL_LIENZO), (0, 0, 0))
    lienzo.paste(vixy, (LADO_DEL_LIENZO // 2 - CABEZA_X, LADO_DEL_LIENZO // 2 - CABEZA_Y))
    return lienzo


def ancho_de_la_ventana(cuadro):
    inicio, fin = CUADRO_DONDE_EMPIEZA_EL_SALTO, CUADRO_DONDE_TERMINA_EL_SALTO
    if cuadro < inicio:
        return VENTANA_LEJOS - 40.0 * (cuadro / inicio)   # casi quieta, se acerca muy poco
    avance = min((cuadro - inicio) / (fin - inicio), 1.0)
    avance = avance ** 3.2                                 # arranque lento, golpe final
    base = VENTANA_LEJOS - 40.0
    return base + (VENTANA_CERCA - base) * avance


def dibujar_cuadro(lienzo, cuadro):
    inicio, fin = CUADRO_DONDE_EMPIEZA_EL_SALTO, CUADRO_DONDE_TERMINA_EL_SALTO
    ancho_v = ancho_de_la_ventana(cuadro)
    alto_v = ancho_v * ALTO / ANCHO

    # Temblor: fuerte durante el salto y el choque, más suave al final.
    fuerza = 0.0
    if cuadro >= inicio - 2:
        fuerza = 0.035 if cuadro < fin + 8 else 0.018
    desvio_x = random.uniform(-1, 1) * ancho_v * fuerza
    desvio_y = random.uniform(-1, 1) * alto_v * fuerza
    centro_x = LADO_DEL_LIENZO // 2 + desvio_x
    centro_y = LADO_DEL_LIENZO // 2 + 6 + desvio_y
    caja = (centro_x - ancho_v / 2, centro_y - alto_v / 2,
            centro_x + ancho_v / 2, centro_y + alto_v / 2)
    imagen = lienzo.resize((ANCHO, ALTO), Image.LANCZOS, box=caja)
    a = np.asarray(imagen).astype(np.float32)

    # Luz: casi a oscuras al empezar y destellos al saltar.
    brillo = 0.55 + 0.25 * (cuadro / inicio) if cuadro < inicio else 1.0
    if cuadro >= inicio - 1 and random.random() < 0.45:
        brillo *= random.uniform(0.45, 1.35)
    a *= brillo

    # Brillo rojo de los ojos, que crece con el acercamiento.
    rojo = (np.clip((a[:, :, 0] - 110) / 120.0, 0, 1)
            * (a[:, :, 1] < 110) * (a[:, :, 2] < 110))
    resplandor = Image.fromarray((rojo * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(14 + 10 * (cuadro / CUADROS)))
    r = np.asarray(resplandor).astype(np.float32) / 255.0
    intensidad = 0.5 + 1.8 * min(cuadro / fin, 1.0)
    a[:, :, 0] += r * 255 * intensidad
    a[:, :, 1] += r * 25 * intensidad

    # Grano de película y viñeta.
    a += np.random.normal(0, 7 if cuadro < inicio else 11, a.shape)
    yv, xv = np.mgrid[0:ALTO, 0:ANCHO]
    vineta = 1 - 0.55 * (((xv - ANCHO / 2) / (ANCHO / 2)) ** 2
                         + ((yv - ALTO / 2) / (ALTO / 2)) ** 2) / 2
    a *= vineta[:, :, None]
    return np.clip(a, 0, 255).astype(np.uint8)


def principal():
    salida = Path(sys.argv[1]) if len(sys.argv) > 1 else SALIDA_POR_DEFECTO
    random.seed(SEMILLA)
    np.random.seed(SEMILLA)
    lienzo = preparar_lienzo()

    ffmpeg = subprocess.Popen(
        ["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24",
         "-s", f"{ANCHO}x{ALTO}", "-r", str(FPS), "-i", "-", "-an",
         "-c:v", "libvpx", "-b:v", "2M", "-crf", "10", "-auto-alt-ref", "0", str(salida)],
        stdin=subprocess.PIPE)
    for cuadro in range(CUADROS):
        ffmpeg.stdin.write(dibujar_cuadro(lienzo, cuadro).tobytes())
    ffmpeg.stdin.close()
    ffmpeg.wait()
    print("Listo:", salida)


if __name__ == "__main__":
    principal()
