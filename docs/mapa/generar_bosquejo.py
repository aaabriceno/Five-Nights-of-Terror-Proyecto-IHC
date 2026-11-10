"""Genera el bosquejo 2D del mapa nuevo (v2, para debatirlo con Marcelo).

Salida (en esta misma carpeta): bosquejo_mapa_v2.png, bosquejo_mapa_v2.pdf (2 páginas),
bosquejo_mapa_v2_datos.png y nodos_mapa_v2.csv. Coordenadas en metros: x hacia el este,
z hacia el norte (igual que el minimapa del jugador 2 y que el plano XZ de Unity).

Uso: python3 docs/mapa/generar_bosquejo.py
"""
import csv
import textwrap
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.backends.backend_pdf import PdfPages
from matplotlib.patches import Rectangle, FancyArrowPatch, Circle

CARPETA = Path(__file__).resolve().parent
VERSION = "v2"

# ------------------------------------------------------------------ colores
PAPEL = "#f6f1e4"
TINTA = "#1d2b3a"
SUAVE = "#6b7a89"
PARED = "#1d2b3a"
DUCTO = "#c5d0dc"
FREDDY = "#2f8f3a"
VIXY = "#d23a3a"
PUPPET = "#b9803a"
ALERTA = "#e0a800"

# --------------------------------------------------------------- espacios
# (nombre, x0, z0, x1, z1, relleno)
ESPACIOS = [
    ("OFICINA", 17, 0, 23, 6, "#fbe9b7"),
    ("PASILLO PRINCIPAL", 18, 6, 22, 10, "#e8edf2"),
    ("COMEDOR", 10, 10, 30, 22, "#eaf1e4"),
    ("ESCENARIO", 10, 22, 30, 29, "#e4e1ef"),
    ("PASILLO IZQUIERDO", 5, 6, 10, 29, "#e8edf2"),
    ("PASILLO DERECHO", 30, 6, 35, 22, "#e8edf2"),
    ("RINCÓN DE PREMIOS", 30, 22, 38, 29, "#f4e8d4"),
]

# Puertas (huecos en las paredes): (x0, z0, x1, z1)
PUERTAS = [
    (19, 5.85, 21, 6.15),            # oficina - pasillo principal
    (18.15, 9.85, 21.85, 10.15),     # pasillo principal - comedor (abierto)
    (11, 21.85, 29, 22.15),          # comedor - escenario (abierto)
    (9.85, 24, 10.15, 27),           # pasillo izquierdo - escenario
    (9.85, 14, 10.15, 18),           # pasillo izquierdo - comedor
    (29.85, 14, 30.15, 18),          # pasillo derecho - comedor
    (31, 21.85, 34, 22.15),          # pasillo derecho - rincón de premios
]

ANCHO_DUCTO = 2.0   # metros: por aquí caminan los animatrónicos
# Ductos (rectángulos x0, z0, x1, z1); el centro de cada tramo está a z = 3 o a x = 7.5 / 32.5
DUCTOS = [
    (6.5, 2, 8.5, 6),      # izquierdo, tramo vertical (baja desde el pasillo)
    (6.5, 2, 17, 4),       # izquierdo, tramo horizontal hasta la oficina
    (31.5, 2, 33.5, 6),    # derecho, tramo vertical
    (23, 2, 33.5, 4),      # derecho, tramo horizontal hasta la oficina
]

# Mesas del comedor (centro x, z): dejan libre el pasillo central x = 18.5 – 21.5
MESAS = [(x, z) for z in (12.3, 18.5) for x in (13.0, 17.0, 23.0, 27.0)]

# ------------------------------------------------------------------- nodos
# (id, nombre, x, z, tipo, uso)
NODOS = [
    ("J1", "Jugador 1 (cámara en la oficina)", 20.0, 2.5, "jugador", "—"),
    # Escenario
    ("P01", "Escenario – Freddy", 16.5, 24.3, "sala", "Freddy (inicio)"),
    ("P02", "Escenario – Vixy", 23.5, 26.3, "sala", "Vixy (inicio)"),
    # Comedor y pasillo principal (ruta de Freddy)
    ("P03", "Comedor – norte", 20.0, 19.5, "sala", "Freddy"),
    ("P04", "Comedor – centro", 20.0, 15.8, "sala", "Freddy"),
    ("P05", "Comedor – sur", 20.0, 11.8, "sala", "Freddy"),
    ("P06", "Pasillo principal", 20.0, 8.0, "pasillo", "Freddy"),
    ("P07", "Puerta de la oficina", 20.0, 6.2, "pasillo", "Freddy (penúltimo punto)"),
    ("P08", "Comedor – oeste (cruce)", 13.5, 16.0, "sala", "desvío Freddy / Vixy"),
    ("P09", "Comedor – este (cruce)", 26.5, 16.0, "sala", "desvío Freddy / Puppet"),
    # Pasillo izquierdo (ruta de Vixy)
    ("P10", "Pasillo izquierdo – norte", 7.5, 25.5, "pasillo", "Vixy"),
    ("P11", "Pasillo izquierdo – medio", 7.5, 16.0, "pasillo", "Vixy"),
    ("P12", "Pasillo izquierdo – sur", 7.5, 10.0, "pasillo", "Vixy"),
    # Rincón de premios y pasillo derecho (ruta de Puppet)
    ("P13", "Rincón de premios – caja de música", 34.0, 25.5, "caja", "Puppet (inicio, oculto)"),
    ("P14", "Pasillo derecho – norte", 32.5, 19.5, "pasillo", "Puppet"),
    ("P15", "Pasillo derecho – medio", 32.5, 16.0, "pasillo", "Puppet"),
    ("P16", "Pasillo derecho – sur", 32.5, 10.0, "pasillo", "Puppet"),
    # Ducto izquierdo
    ("V01", "Rejilla de entrada – izquierda", 7.5, 7.0, "ducto", "Vixy"),
    ("V02", "Codo – ducto izquierdo", 7.5, 3.0, "ducto", "Vixy"),
    ("V03", "Ducto izquierdo – medio", 12.5, 3.0, "ducto", "Vixy"),
    ("V04", "Boca de la ventilación izquierda", 16.3, 3.0, "ducto", "Vixy (penúltimo punto)"),
    # Ducto derecho
    ("V05", "Rejilla de entrada – derecha", 32.5, 7.0, "ducto", "Puppet"),
    ("V06", "Codo – ducto derecho", 32.5, 3.0, "ducto", "Puppet"),
    ("V07", "Ducto derecho – medio", 27.5, 3.0, "ducto", "Puppet"),
    ("V08", "Boca de la ventilación derecha", 23.7, 3.0, "ducto", "Puppet (penúltimo punto)"),
]
POS = {n[0]: (n[2], n[3]) for n in NODOS}

RUTAS = {
    "Freddy": (FREDDY, ["P01", "P03", "P04", "P05", "P06", "P07", "J1"]),
    "Vixy": (VIXY, ["P02", "P10", "P11", "P12", "V01", "V02", "V03", "V04", "J1"]),
    "Puppet": (PUPPET, ["P13", "P14", "P15", "P16", "V05", "V06", "V07", "V08", "J1"]),
}
# Cruces entre rutas (desvíos posibles): atraviesan las puertas del comedor
DESVIOS = [("P04", "P08"), ("P08", "P11"), ("P04", "P09"), ("P09", "P15")]

COLOR_TIPO = {"jugador": TINTA, "sala": "#ffffff", "pasillo": "#ffffff", "caja": "#ffffff", "ducto": "#dfe6ee"}


# ------------------------------------------------------------------ dibujo
def dibujar_plano(ax):
    ax.set_facecolor(PAPEL)
    ax.set_xlim(2.5, 41.5)
    ax.set_ylim(-4.2, 31.2)
    ax.set_aspect("equal")
    ax.set_xticks(range(5, 41, 5))
    ax.set_yticks(range(0, 31, 5))
    ax.grid(True, color="#d9d2bf", lw=0.6, zorder=0)
    ax.tick_params(colors=SUAVE, labelsize=8)
    for lado in ax.spines.values():
        lado.set_color(SUAVE)
    ax.set_xlabel("x (metros, este →)", color=SUAVE, fontsize=9)
    ax.set_ylabel("z (metros, norte ↑)", color=SUAVE, fontsize=9)

    # zona técnica bajo los pasillos laterales (solo ductos)
    for x0, x1 in ((5, 17), (23, 35)):
        ax.add_patch(Rectangle((x0, 0), x1 - x0, 6, facecolor="none", edgecolor=SUAVE,
                               hatch="////", lw=0.0, alpha=0.22, zorder=1))
    ax.text(11, 0.9, "zona técnica (solo ductos)", ha="center", fontsize=7, color=SUAVE, style="italic")
    ax.text(29, 0.9, "zona técnica (solo ductos)", ha="center", fontsize=7, color=SUAVE, style="italic")

    # ductos (2 m de ancho)
    for x0, z0, x1, z1 in DUCTOS:
        ax.add_patch(Rectangle((x0, z0), x1 - x0, z1 - z0, facecolor=DUCTO, edgecolor=SUAVE,
                               lw=1.0, ls=(0, (4, 3)), zorder=2))
    ax.text(12.5, 4.45, "VENTILACIÓN IZQ. · 2 m de ancho", ha="center", fontsize=6.4, color=SUAVE, zorder=4)
    ax.text(28, 4.45, "VENTILACIÓN DER. · 2 m de ancho", ha="center", fontsize=6.4, color=SUAVE, zorder=4)

    # espacios
    for nombre, x0, z0, x1, z1, relleno in ESPACIOS:
        ax.add_patch(Rectangle((x0, z0), x1 - x0, z1 - z0, facecolor=relleno, edgecolor=PARED, lw=2.2, zorder=2))

    # comedor: mesas con sillas
    for x, z in MESAS:
        ax.add_patch(Circle((x, z), 1.05, facecolor="#d8c9a3", edgecolor=PARED, lw=1.0, zorder=3))
        for dx, dz in ((1.7, 0), (-1.7, 0), (0, 1.7), (0, -1.7)):
            ax.add_patch(Circle((x + dx, z + dz), 0.32, facecolor="#b79f6c", edgecolor="none", zorder=3))
    ax.text(20, 21.7, "COMEDOR", ha="center", va="top", fontsize=8.5, weight="bold", color=TINTA, zorder=6)
    ax.text(20, 20.75, "mesas para ver y oír a los animatrónicos", ha="center", va="top",
            fontsize=6.3, color=SUAVE, style="italic", zorder=6)

    # escenario
    ax.add_patch(Rectangle((12, 23.2), 16, 4.3, facecolor="#cfc9e6", edgecolor=PARED, lw=1.2, zorder=3))
    ax.text(20, 28.6, "ESCENARIO", ha="center", va="top", fontsize=8.5, weight="bold", color=TINTA, zorder=6)
    ax.text(20, 23.45, "tarima", ha="center", va="bottom", fontsize=6.3, color=SUAVE, zorder=5)

    # rincón de premios: caja de música oculta
    ax.text(34, 28.6, "RINCÓN DE PREMIOS", ha="center", va="top", fontsize=8, weight="bold", color=TINTA, zorder=6)
    ax.add_patch(Rectangle((32.9, 24.8), 2.2, 1.4, facecolor="#e8c98a", edgecolor=PARED, lw=1.2, zorder=3))
    ax.text(34, 23.2, "♪ caja de música\n(oculta: no se ve desde el comedor)", ha="center", va="center",
            fontsize=5.9, color=PUPPET, weight="bold", zorder=9, linespacing=1.15)

    # etiquetas de pasillos
    ax.text(5.75, 17.5, "PASILLO IZQUIERDO", rotation=90, ha="center", va="center", fontsize=7.5,
            weight="bold", color=TINTA, zorder=6)
    ax.text(30.75, 12.5, "PASILLO DERECHO", rotation=90, ha="center", va="center", fontsize=7.5,
            weight="bold", color=TINTA, zorder=6)
    ax.text(18.75, 8, "PASILLO\nPRINCIPAL", rotation=90, ha="center", va="center", fontsize=6.2,
            weight="bold", color=TINTA, zorder=6, linespacing=1.0)
    ax.text(17.35, 5.55, "OFICINA", ha="left", va="center", fontsize=8, weight="bold", color=TINTA, zorder=6)

    # puertas
    for x0, z0, x1, z1 in PUERTAS:
        ax.add_patch(Rectangle((x0, z0), x1 - x0, z1 - z0, facecolor=PAPEL, edgecolor="none", zorder=4))

    # escritorio y jugador
    ax.add_patch(Rectangle((18.2, 0.4), 3.6, 1.1, facecolor="#e4cf93", edgecolor=PARED, lw=1.0, zorder=3))
    ax.text(20, 0.95, "escritorio", ha="center", va="center", fontsize=6.5, color=TINTA, zorder=5)

    # desvíos (cruces entre rutas)
    for a, b in DESVIOS:
        (xa, za), (xb, zb) = POS[a], POS[b]
        ax.plot([xa, xb], [za, zb], color=SUAVE, lw=1.3, ls=(0, (1.5, 3)), zorder=6)

    # rutas
    for nombre, (color, ids) in RUTAS.items():
        puntos = [POS[i] for i in ids]
        for a, b in zip(puntos, puntos[1:]):
            ax.add_patch(FancyArrowPatch(a, b, arrowstyle="-|>", mutation_scale=11, lw=2.0, color=color,
                                         shrinkA=6, shrinkB=6, zorder=7, alpha=0.9))

    # nodos
    for nid, nombre, x, z, tipo, para in NODOS:
        if nid == "J1":
            continue
        radio = 0.62 if tipo != "ducto" else 0.55
        borde = ALERTA if "penúltimo" in para else TINTA
        ax.add_patch(Circle((x, z), radio, facecolor=COLOR_TIPO[tipo], edgecolor=borde,
                            lw=2.4 if borde == ALERTA else 1.2, zorder=8))
        ax.text(x, z, nid, ha="center", va="center", fontsize=5.4 if tipo != "ducto" else 5.2,
                weight="bold", color=TINTA, zorder=9)

    # Freddy y Vixy visibles en el escenario (marcas junto a sus nodos)
    ax.text(16.5, 22.9, "Freddy", ha="center", va="center", fontsize=6.5, weight="bold", color=FREDDY, zorder=9)
    ax.text(23.5, 25.2, "Vixy", ha="center", va="center", fontsize=6.5, weight="bold", color=VIXY, zorder=9)
    ax.text(34, 26.5, "Puppet", ha="center", va="center", fontsize=6.3, weight="bold", color=PUPPET, zorder=9)

    # jugador
    ax.add_patch(Circle((20, 2.5), 0.8, facecolor=TINTA, edgecolor="white", lw=1.5, zorder=9))
    ax.text(20, 2.5, "J1", ha="center", va="center", fontsize=7, weight="bold", color="white", zorder=10)
    ax.add_patch(FancyArrowPatch((20, 3.5), (20, 4.9), arrowstyle="-|>", mutation_scale=10, lw=1.4,
                                 color=TINTA, zorder=9))
    ax.text(20.35, 4.3, "mira al norte", fontsize=6, color=TINTA, ha="left", va="center", zorder=9)

    # círculos de pérdida de señal
    for r in (2, 4):
        ax.add_patch(Circle((20, 2.5), r, facecolor="none", edgecolor=ALERTA, lw=0.9, ls=(0, (3, 3)), zorder=6))
    ax.text(20, -1.6, "círculos punteados: 2 m = sin señal · 4 m = señal inestable (minimapa del jugador 2)",
            ha="center", fontsize=6.8, color=SUAVE)

    # escala y norte
    ax.plot([33, 38], [-3.0, -3.0], color=TINTA, lw=3, solid_capstyle="butt")
    ax.text(35.5, -2.4, "5 m", ha="center", fontsize=7, color=TINTA)
    ax.add_patch(FancyArrowPatch((40.3, 26.8), (40.3, 29.6), arrowstyle="-|>", mutation_scale=12, lw=1.6, color=TINTA))
    ax.text(40.3, 30.1, "N", ha="center", fontsize=9, weight="bold", color=TINTA)


def leyenda(ax):
    ax.axis("off")
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 31)
    y = 30.2
    ax.text(0, y, "Cómo leer el plano", fontsize=10, weight="bold", color=TINTA)
    y -= 1.4
    for color, texto in ((FREDDY, "Freddy: comedor y pasillo principal"),
                         (VIXY, "Vixy: pasillo y ventilación izquierda"),
                         (PUPPET, "Puppet: pasillo y ventilación derecha")):
        ax.add_patch(FancyArrowPatch((0.1, y), (1.5, y), arrowstyle="-|>", mutation_scale=10, lw=2.0, color=color))
        ax.text(1.8, y, texto, fontsize=7.6, va="center", color=TINTA)
        y -= 1.2
    ax.plot([0.1, 1.5], [y, y], color=SUAVE, lw=1.3, ls=(0, (1.5, 3)))
    ax.text(1.8, y, "cruces posibles entre rutas", fontsize=7.6, va="center", color=TINTA)
    y -= 1.5
    ax.add_patch(Circle((0.5, y), 0.35, facecolor="white", edgecolor=TINTA, lw=1.2))
    ax.text(1.8, y, "punto de ruta (Pxx)", fontsize=7.6, va="center", color=TINTA)
    y -= 1.1
    ax.add_patch(Circle((0.5, y), 0.3, facecolor="#dfe6ee", edgecolor=TINTA, lw=1.2))
    ax.text(1.8, y, "punto dentro de la ventilación (Vxx)", fontsize=7.6, va="center", color=TINTA)
    y -= 1.2
    ax.add_patch(Circle((0.5, y), 0.35, facecolor="white", edgecolor=ALERTA, lw=2.4))
    ax.text(1.8, y, "penúltimo punto: desde ahí\nataca si nadie lo mira", fontsize=7.6, va="center", color=TINTA)
    y -= 1.9
    ax.add_patch(Rectangle((0.1, y - 0.35), 0.8, 0.7, facecolor=PAPEL, edgecolor=PARED, lw=1))
    ax.text(1.8, y, "puertas: huecos en la pared", fontsize=7.6, va="center", color=TINTA)
    y -= 1.5
    ax.add_patch(Rectangle((0.1, y - 0.45), 1.4, 0.9, facecolor=DUCTO, edgecolor=SUAVE, lw=1.0, ls=(0, (4, 3))))
    ax.text(1.8, y, "ventilación: 2 m de ancho", fontsize=7.6, va="center", color=TINTA)

    y -= 2.3
    ax.text(0, y, "Qué se busca", fontsize=10, weight="bold", color=TINTA)
    notas = [
        "• Pocas salas: escenario, comedor, rincón\n  de premios y tres pasillos hasta la oficina.",
        "• Freddy y Vixy salen del escenario, a la\n  vista. Puppet sale de su rincón, oculto.",
        "• 3 entradas a la oficina (pasillo principal y\n  dos ventilaciones): una por animatrónico.",
        "• Las ventilaciones son anchas porque los\n  animatrónicos pasan por ahí.",
        "• Pasillos laterales: recorrido que baja del\n  escenario hasta cada ventilación.",
        "• Todo en metros: 33 × 29 m aprox.",
    ]
    y -= 1.5
    for nota in notas:
        ax.text(0, y, nota, fontsize=7.2, va="top", color=TINTA, linespacing=1.25)
        y -= 2.15


def pagina_mapa():
    fig = plt.figure(figsize=(16.5, 10.2), facecolor=PAPEL)
    fig.suptitle(f"Five Nights of Terror · Bosquejo del mapa nuevo ({VERSION})",
                 x=0.03, y=0.975, ha="left", fontsize=15, weight="bold", color=TINTA)
    fig.text(0.03, 0.945, "Vista desde arriba · inspirado en FNAF 2 (oficina con pasillo y dos ventilaciones; la caja de música de "
             "Puppet en el Rincón de premios) · metros: x = este, z = norte", fontsize=9.3, color=SUAVE)
    ax = fig.add_axes([0.03, 0.04, 0.70, 0.88])
    dibujar_plano(ax)
    lg = fig.add_axes([0.745, 0.06, 0.235, 0.86])
    leyenda(lg)
    return fig


def bloque(ax, x, y, texto, tam=8.0, ancho=185, color=TINTA, separacion=1.0):
    """Escribe `texto` con saltos de línea y devuelve la y donde sigue lo siguiente."""
    lineas = []
    for parrafo in texto.split("\n"):
        lineas += textwrap.wrap(parrafo, ancho) or [""]
    ax.text(x, y, "\n".join(lineas), fontsize=tam, color=color, va="top", linespacing=1.35)
    return y - len(lineas) * tam * 0.2 - separacion


def pagina_datos():
    fig = plt.figure(figsize=(16.5, 10.2), facecolor=PAPEL)
    fig.suptitle("Puntos de ruta, movimiento de los animatrónicos y lo que hay que decidir", x=0.03, y=0.975,
                 ha="left", fontsize=15, weight="bold", color=TINTA)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.axis("off")
    ax.set_xlim(0, 100)
    ax.set_ylim(0, 100)

    ax.text(3, 92, "Puntos de ruta (cada uno es un objeto vacío en Unity con ese nombre y posición X, Z)",
            fontsize=10, weight="bold", color=TINTA)
    mitad = 13
    fila = 2.05
    y_fin = 0
    for col, tramo in enumerate((NODOS[:mitad], NODOS[mitad:])):
        x0 = 3 + col * 48
        y = 89.2
        ax.text(x0, y, "ID", fontsize=8, weight="bold", color=SUAVE, family="monospace")
        ax.text(x0 + 4.2, y, "Lugar", fontsize=8, weight="bold", color=SUAVE)
        ax.text(x0 + 27, y, "x", fontsize=8, weight="bold", color=SUAVE, family="monospace")
        ax.text(x0 + 31, y, "z", fontsize=8, weight="bold", color=SUAVE, family="monospace")
        ax.text(x0 + 35.5, y, "Uso", fontsize=8, weight="bold", color=SUAVE)
        y -= fila
        for nid, nombre, x, z, tipo, para in tramo:
            ax.text(x0, y, nid, fontsize=8, family="monospace", weight="bold", color=TINTA)
            ax.text(x0 + 4.2, y, nombre, fontsize=8, color=TINTA)
            ax.text(x0 + 27, y, f"{x:g}", fontsize=8, family="monospace", color=TINTA)
            ax.text(x0 + 31, y, f"{z:g}", fontsize=8, family="monospace", color=TINTA)
            ax.text(x0 + 35.5, y, para, fontsize=7.4, color=SUAVE)
            y -= fila
        y_fin = min(y_fin, y) if y_fin else y

    # rutas
    y = y_fin - 1.2
    ax.text(3, y, "Rutas propuestas (recorrido estándar, en orden)", fontsize=10, weight="bold", color=TINTA)
    y -= 3.0
    for nombre, (color, ids) in RUTAS.items():
        ax.add_patch(Rectangle((3, y - 0.7), 1.2, 1.6, facecolor=color, edgecolor="none"))
        ax.text(5, y + 0.1, f"{nombre}:", fontsize=8.5, weight="bold", color=TINTA, va="center")
        ax.text(11, y + 0.1, "  →  ".join(ids), fontsize=8.5, family="monospace", color=TINTA, va="center")
        y -= 2.5
    ax.text(3, y, "Cruces (desvíos): P04 ↔ P08 ↔ P11 (comedor ↔ pasillo izquierdo)   ·   "
            "P04 ↔ P09 ↔ P15 (comedor ↔ pasillo derecho)", fontsize=8, color=SUAVE)
    y -= 3.2

    ax.text(3, y, "Cómo se mueven (hoy y propuesta)", fontsize=10, weight="bold", color=TINTA)
    y -= 2.6
    y = bloque(ax, 3.5, y,
               "HOY: en cada salto el animatrónico elige entre los 3 puntos sin visitar más cercanos que lo acerquen al jugador "
               "(con más peso al más cercano). Cuando está a menos de 9 m va directo al penúltimo punto y ataca. "
               "Nunca retrocede, no repite puntos y no da vueltas: por eso se ve que “solo avanza”.", separacion=1.3)
    y = bloque(ax, 3.5, y,
               "PROPUESTA (para decidir): A) dejarlo como está (avance directo).   B) Recorrido estándar: sigue su ruta en orden y en "
               "cada punto puede avanzar, quedarse o retroceder un punto.   C) Mixto (mi recomendación): recorrido estándar con "
               "desvíos por los cruces y poca probabilidad de retroceder; cada uno conserva su regla propia (Freddy se detiene si "
               "lo miran, Vixy avanza con tareas sin resolver, Puppet sale cuando su caja llega a 0).", separacion=2.0)

    ax.text(3, y, "Lo que el juego necesita del mapa", fontsize=10, weight="bold", color=TINTA)
    y -= 2.6
    for r in (
        "• Los animatrónicos caminan con NavMesh: hay que hornearlo también dentro de las ventilaciones. Se proponen 2 m de ancho "
        "(en FNAF 2 son más angostas; aquí ellos pasan por ahí) y ~2,5 m de alto.",
        "• Cada punto de ruta es un Transform con el nombre de la tabla (P01, V04…): el jugador 2 los ve en su minimapa y pone las trampas ahí.",
        "• Freddy y Vixy usan: puntos candidatos + penúltimo punto + posición del jugador al final. Puppet usa una ruta en orden. "
        "El jugador (J1) queda fijo en la oficina, (20, 2.5).",
        "• La caja de música de Puppet va en el Rincón de premios (sin abertura hacia el comedor) para que no se vea desde el escenario.",
    ):
        y = bloque(ax, 3.5, y, r, separacion=0.6)
    y -= 1.2

    ax.text(3, y, "Preguntas para decidir con Marcelo", fontsize=10, weight="bold", color=TINTA)
    y -= 2.6
    for q in (
        "1) ¿La cámara de J1 gira para ver las ventilaciones, o queda fija mirando al pasillo?",
        "2) ¿Se usan los pasillos laterales y las ventilaciones a la vez, o solo la ventilación?",
        "3) ¿Dos entradas por animatrónico? Hoy el código admite un solo penúltimo punto; ampliarlo lo hago yo.",
        "4) ¿Tamaño final? 33 × 29 m es una propuesta; se puede escalar sin cambiar la forma.",
    ):
        y = bloque(ax, 3.5, y, q, separacion=0.4)
    return fig


def csv_de_nodos():
    with open(CARPETA / f"nodos_mapa_{VERSION}.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["id", "nombre", "x", "z", "tipo", "uso"])
        for nid, nombre, x, z, tipo, para in NODOS:
            w.writerow([nid, nombre, x, z, tipo, para])


if __name__ == "__main__":
    plt.rcParams["font.family"] = "DejaVu Sans"
    p1 = pagina_mapa()
    p2 = pagina_datos()
    p1.savefig(CARPETA / f"bosquejo_mapa_{VERSION}.png", dpi=150, facecolor=PAPEL)
    with PdfPages(CARPETA / f"bosquejo_mapa_{VERSION}.pdf") as pdf:
        pdf.savefig(p1, facecolor=PAPEL)
        pdf.savefig(p2, facecolor=PAPEL)
    p2.savefig(CARPETA / f"bosquejo_mapa_{VERSION}_datos.png", dpi=150, facecolor=PAPEL)
    csv_de_nodos()
    print("listo:", *sorted(x.name for x in CARPETA.iterdir() if x.name.startswith(("bosquejo", "nodos"))))
