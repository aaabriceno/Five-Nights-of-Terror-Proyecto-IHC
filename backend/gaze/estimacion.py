"""Funciones puras de estimacion a partir de landmarks de MediaPipe Face Mesh.

Sin dependencias de camara ni UI: reciben landmarks + dimensiones de imagen,
devuelven numeros. Facil de probar con landmarks simulados.

La orientacion de cabeza se estima con proporciones geometricas directas entre
landmarks (no con solvePnP): con la calidad de camara disponible, solvePnP con
pocos puntos convergia a poses ambiguas (saltos de angulo de cientos de grados
sin sentido fisico). Las proporciones no dan un angulo exacto en grados, pero
son monotonas y estables -- suficiente para clasificar zonas de atencion.
"""

from gaze.landmarks import (
    FRENTE,
    IRIS_DER,
    IRIS_IZQ,
    MENTON,
    OJO_DER_EXTERIOR,
    OJO_DER_INTERIOR,
    OJO_DER_PARPADO_INF,
    OJO_DER_PARPADO_SUP,
    OJO_IZQ_EXTERIOR,
    OJO_IZQ_INTERIOR,
    OJO_IZQ_PARPADO_INF,
    OJO_IZQ_PARPADO_SUP,
    POMULO_DER,
    POMULO_IZQ,
    PUNTA_NARIZ,
)

# proporcion vertical frente-nariz-menton en reposo (cabeza recta, de frente a
# la camara). Medido empiricamente: NO es 0.5 -- la nariz esta naturalmente mas
# cerca de la frente que del menton en la proyeccion 2D tipica de una webcam.
PROPORCION_VERTICAL_REPOSO = 0.62

# escalas para convertir las proporciones geometricas (adimensionales) a la misma
# unidad "equivalente a grados" que usa el vector de mirada, de forma que sean
# comparables en magnitud con la correccion del iris.
ESCALA_PITCH = 250.0
ESCALA_YAW = 120.0

# peso con el que la desviacion del iris (respecto al centro del ojo) corrige
# el angulo puro de cabeza al construir el vector de mirada combinado. Un iris
# muy desviado (cerca de 0 o 1) puede compensar hasta ~este tanto en grados.
PESO_IRIS_EN_VECTOR = 15.0


def punto_2d(landmarks, indice, ancho, alto):
    lm = landmarks[indice]
    return (lm.x * ancho, lm.y * alto)


def posicion_normalizada(exterior, interior, iris):
    """Posicion horizontal del iris entre esquina exterior (0) e interior (1) del ojo."""
    rango_x = interior[0] - exterior[0]
    if rango_x == 0:
        return 0.5
    return (iris[0] - exterior[0]) / rango_x


def posicion_vertical_iris(parpado_sup, parpado_inf, iris):
    """Posicion vertical del iris entre parpado superior (0) e inferior (1).

    Valores bajos = iris pegado arriba del ojo = el ojo esta rotando hacia arriba,
    tipicamente para compensar una cabeza agachada y seguir mirando la pantalla.
    """
    rango_y = parpado_inf[1] - parpado_sup[1]
    if rango_y == 0:
        return 0.5
    return (iris[1] - parpado_sup[1]) / rango_y


def estimar_orientacion_cabeza(landmarks, ancho, alto):
    """Senal geometrica de orientacion de cabeza: (pitch, yaw), "equivalentes a grados".

    Pitch positivo = cabeza inclinada hacia abajo. Se mide comparando la distancia
    vertical frente-nariz contra nariz-menton: al agachar la cabeza, la nariz se
    proyecta relativamente mas cerca de la frente que del menton.

    Yaw positivo = cabeza girada hacia la derecha (desde la camara). Se mide
    comparando la distancia horizontal nariz-pomulo izquierdo vs nariz-pomulo
    derecho: de frente son iguales, girando la cabeza uno se acorta por perspectiva.
    """
    frente = punto_2d(landmarks, FRENTE, ancho, alto)
    nariz = punto_2d(landmarks, PUNTA_NARIZ, ancho, alto)
    menton = punto_2d(landmarks, MENTON, ancho, alto)
    pomulo_izq = punto_2d(landmarks, POMULO_IZQ, ancho, alto)
    pomulo_der = punto_2d(landmarks, POMULO_DER, ancho, alto)

    dist_frente_nariz = nariz[1] - frente[1]
    dist_nariz_menton = menton[1] - nariz[1]
    total_vertical = dist_frente_nariz + dist_nariz_menton
    if total_vertical == 0:
        pitch = 0.0
    else:
        # se aleja de PROPORCION_VERTICAL_REPOSO hacia arriba al agachar la cabeza
        # (la nariz se acerca relativamente a la frente).
        proporcion_vertical = dist_frente_nariz / total_vertical
        pitch = (proporcion_vertical - PROPORCION_VERTICAL_REPOSO) * ESCALA_PITCH

    dist_nariz_pomulo_izq = abs(nariz[0] - pomulo_izq[0])
    dist_nariz_pomulo_der = abs(pomulo_der[0] - nariz[0])
    total_horizontal = dist_nariz_pomulo_izq + dist_nariz_pomulo_der
    if total_horizontal == 0:
        yaw = 0.0
    else:
        proporcion_horizontal = dist_nariz_pomulo_der / total_horizontal
        yaw = (proporcion_horizontal - 0.5) * ESCALA_YAW

    return pitch, yaw


class LecturaOjos:
    """Snapshot de las medidas de iris/cabeza de un frame."""

    __slots__ = ("iris_x", "iris_y", "pitch", "yaw")

    def __init__(self, iris_x, iris_y, pitch, yaw):
        self.iris_x = iris_x
        self.iris_y = iris_y
        self.pitch = pitch
        self.yaw = yaw


def leer_ojos_y_cabeza(landmarks, ancho, alto):
    """Extrae iris (horizontal/vertical, promedio de ambos ojos) y orientacion de cabeza."""

    def p(indice):
        return punto_2d(landmarks, indice, ancho, alto)

    pos_izq = posicion_normalizada(p(OJO_IZQ_EXTERIOR), p(OJO_IZQ_INTERIOR), p(IRIS_IZQ))
    pos_der = posicion_normalizada(p(OJO_DER_EXTERIOR), p(OJO_DER_INTERIOR), p(IRIS_DER))
    iris_x = (pos_izq + pos_der) / 2

    vert_izq = posicion_vertical_iris(p(OJO_IZQ_PARPADO_SUP), p(OJO_IZQ_PARPADO_INF), p(IRIS_IZQ))
    vert_der = posicion_vertical_iris(p(OJO_DER_PARPADO_SUP), p(OJO_DER_PARPADO_INF), p(IRIS_DER))
    iris_y = (vert_izq + vert_der) / 2

    pitch, yaw = estimar_orientacion_cabeza(landmarks, ancho, alto)

    return LecturaOjos(iris_x=iris_x, iris_y=iris_y, pitch=pitch, yaw=yaw)


def calcular_vector_mirada(lectura: LecturaOjos):
    """Combina orientacion de cabeza + desviacion del iris en un vector de mirada 2D (gx, gy).

    Esto es lo que permite que "cabeza agachada + ojo compensando hacia arriba"
    siga dando un gy cercano al de mirar de frente, en vez de que ambas senales
    se contradigan.
    """
    gx = lectura.yaw + (lectura.iris_x - 0.5) * PESO_IRIS_EN_VECTOR
    gy = lectura.pitch + (lectura.iris_y - 0.5) * PESO_IRIS_EN_VECTOR
    return gx, gy
