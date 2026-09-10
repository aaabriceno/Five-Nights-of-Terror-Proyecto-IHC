"""Calibracion del vector de mirada (gx, gy) contra la pantalla y la tablet reales
del jugador. Guarda/carga backend/calibracion.json.

Flujo de calibracion:
1. El jugador mira 5 puntos de la pantalla (centro + 4 esquinas) -> define un
   rectangulo de (gx, gy) que se considera "mirando la pantalla".
2. El jugador mira hacia donde sostiene la tablet -> un punto de referencia (gx, gy).

En uso normal, un vector de mirada dentro del rectangulo = PANTALLA; fuera del
rectangulo pero cerca del punto tablet = TABLET; ninguno de los dos = NADA.
"""

import json
import time
from pathlib import Path

import cv2

from gaze.estimacion import calcular_vector_mirada, leer_ojos_y_cabeza

ARCHIVO_CALIBRACION = Path(__file__).parent.parent / "calibracion.json"

DURACION_MUESTREO_SEGUNDOS = 2
MARGEN_RECTANGULO_PANTALLA = 1.25  # agranda un poco el rectangulo medido, para tolerar variacion natural
RADIO_TABLET_DEFECTO = 15.0  # grados: distancia maxima al punto tablet calibrado, si no se guardo un radio propio
DISTANCIA_MINIMA_TABLET_PANTALLA = 12.0  # grados: por debajo de esto se rechaza y se repite el paso de tablet

PUNTOS_PANTALLA = (
    "CENTRO de la pantalla",
    "ESQUINA SUPERIOR IZQUIERDA de la pantalla",
    "ESQUINA SUPERIOR DERECHA de la pantalla",
    "ESQUINA INFERIOR IZQUIERDA de la pantalla",
    "ESQUINA INFERIOR DERECHA de la pantalla",
)


class Calibracion:
    """Resultado de calibracion, listo para clasificar vectores de mirada."""

    def __init__(self, rect_min, rect_max, punto_tablet, radio_tablet):
        self.rect_min = rect_min  # (gx_min, gy_min)
        self.rect_max = rect_max  # (gx_max, gy_max)
        self.punto_tablet = punto_tablet  # (gx, gy)
        self.radio_tablet = radio_tablet

    def en_pantalla(self, gx, gy):
        return self.rect_min[0] <= gx <= self.rect_max[0] and self.rect_min[1] <= gy <= self.rect_max[1]

    def en_tablet(self, gx, gy):
        distancia = ((gx - self.punto_tablet[0]) ** 2 + (gy - self.punto_tablet[1]) ** 2) ** 0.5
        return distancia <= self.radio_tablet

    def a_dict(self):
        return {
            "rect_min": list(self.rect_min),
            "rect_max": list(self.rect_max),
            "punto_tablet": list(self.punto_tablet),
            "radio_tablet": self.radio_tablet,
        }

    @staticmethod
    def desde_dict(datos):
        return Calibracion(
            rect_min=tuple(datos["rect_min"]),
            rect_max=tuple(datos["rect_max"]),
            punto_tablet=tuple(datos["punto_tablet"]),
            radio_tablet=datos["radio_tablet"],
        )


def existe_calibracion():
    return ARCHIVO_CALIBRACION.exists()


def cargar_calibracion():
    """Devuelve una Calibracion, o None si no hay una guardada todavia."""
    if not existe_calibracion():
        return None
    datos = json.loads(ARCHIVO_CALIBRACION.read_text())
    return Calibracion.desde_dict(datos)


def guardar_calibracion(calibracion: Calibracion):
    ARCHIVO_CALIBRACION.write_text(json.dumps(calibracion.a_dict(), indent=2))


def _muestrear_vector_promedio(camara, malla_facial, etiqueta, ventana_titulo):
    """Muestra countdown en ventana y promedia el vector de mirada durante el muestreo."""
    print(f"\nMira {etiqueta} y presiona ENTER cuando estes listo...")
    input()

    lecturas = []
    inicio = time.time()
    while time.time() - inicio < DURACION_MUESTREO_SEGUNDOS:
        ok, cuadro = camara.read()
        if not ok:
            continue
        cuadro = cv2.flip(cuadro, 1)
        resultado = malla_facial.process(cv2.cvtColor(cuadro, cv2.COLOR_BGR2RGB))
        if resultado.multi_face_landmarks:
            landmarks = resultado.multi_face_landmarks[0].landmark
            alto, ancho = cuadro.shape[:2]
            lectura = leer_ojos_y_cabeza(landmarks, ancho, alto)
            lecturas.append(calcular_vector_mirada(lectura))

        restante = DURACION_MUESTREO_SEGUNDOS - (time.time() - inicio)
        cv2.putText(cuadro, f"{etiqueta}: {restante:.1f}s", (30, 50),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 255, 255), 2)
        cv2.imshow(ventana_titulo, cuadro)
        cv2.waitKey(1)

    if not lecturas:
        print(f"No se detecto rostro durante '{etiqueta}'. Reintenta la calibracion.")
        raise SystemExit(1)

    gx_promedio = sum(v[0] for v in lecturas) / len(lecturas)
    gy_promedio = sum(v[1] for v in lecturas) / len(lecturas)
    print(f"  vector promedio: ({gx_promedio:.1f}, {gy_promedio:.1f}) grados ({len(lecturas)} muestras)")
    return gx_promedio, gy_promedio


def ejecutar_calibracion(camara, malla_facial):
    """Corre el flujo interactivo de calibracion y guarda el resultado en disco."""
    ventana_titulo = "Calibracion - Attention Defense"
    print("=== Calibracion del vector de mirada ===")
    print("Paso 1: 5 puntos de tu pantalla. Paso 2: donde sostenes la tablet.\n")

    vectores_pantalla = [
        _muestrear_vector_promedio(camara, malla_facial, punto, ventana_titulo)
        for punto in PUNTOS_PANTALLA
    ]

    gx_valores = [v[0] for v in vectores_pantalla]
    gy_valores = [v[1] for v in vectores_pantalla]
    gx_centro = sum(gx_valores) / len(gx_valores)
    gy_centro = sum(gy_valores) / len(gy_valores)
    gx_medio_rango = (max(gx_valores) - min(gx_valores)) / 2 * MARGEN_RECTANGULO_PANTALLA
    gy_medio_rango = (max(gy_valores) - min(gy_valores)) / 2 * MARGEN_RECTANGULO_PANTALLA

    rect_min = (gx_centro - gx_medio_rango, gy_centro - gy_medio_rango)
    rect_max = (gx_centro + gx_medio_rango, gy_centro + gy_medio_rango)

    while True:
        punto_tablet = _muestrear_vector_promedio(
            camara, malla_facial, "hacia la TABLET (como si la sostuvieras en la mano)", ventana_titulo
        )

        distancia_centro_tablet = ((punto_tablet[0] - gx_centro) ** 2 + (punto_tablet[1] - gy_centro) ** 2) ** 0.5
        cae_dentro_del_rectangulo = (
            rect_min[0] <= punto_tablet[0] <= rect_max[0] and rect_min[1] <= punto_tablet[1] <= rect_max[1]
        )

        if distancia_centro_tablet >= DISTANCIA_MINIMA_TABLET_PANTALLA and not cae_dentro_del_rectangulo:
            break

        print(f"\nEl punto de tablet quedo muy cerca (o dentro) del rectangulo de pantalla "
              f"({distancia_centro_tablet:.1f} grados de diferencia, minimo requerido {DISTANCIA_MINIMA_TABLET_PANTALLA}).")
        print("Repite este paso inclinando bien la cabeza hacia abajo (no solo los ojos),")
        print("como si realmente sostuvieras la tablet en la mano.")

    radio_tablet = max(RADIO_TABLET_DEFECTO, distancia_centro_tablet * 0.6)

    calibracion = Calibracion(rect_min, rect_max, punto_tablet, radio_tablet)
    guardar_calibracion(calibracion)

    print(f"\nRectangulo pantalla: {rect_min} a {rect_max}")
    print(f"Punto tablet: {punto_tablet}, radio {radio_tablet:.1f}")
    print(f"Guardado en {ARCHIVO_CALIBRACION.name}")
    cv2.destroyAllWindows()
