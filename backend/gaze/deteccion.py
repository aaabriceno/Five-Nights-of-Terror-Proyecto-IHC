"""Clasificacion de zona de atencion (PANTALLA / TABLET / NADA) a partir del
vector de mirada, con filtro temporal para evitar parpadeos de estado.
"""

FRAMES_CONFIRMACION = 15  # cuadros seguidos en la misma zona antes de confirmar el cambio

PANTALLA = "PANTALLA"
TABLET = "TABLET"
NADA = "NADA"


def clasificar_zona(gx, gy, calibracion):
    """Devuelve PANTALLA, TABLET o NADA para un vector de mirada (gx, gy) dado."""
    if calibracion.en_pantalla(gx, gy):
        return PANTALLA
    if calibracion.en_tablet(gx, gy):
        return TABLET
    return NADA


class RastreadorDeZona:
    """Aplica un filtro temporal: la zona confirmada solo cambia tras N cuadros seguidos
    detectando la misma zona candidata. Evita que un solo frame ruidoso dispare un cambio.
    """

    def __init__(self, zona_inicial=PANTALLA, frames_confirmacion=FRAMES_CONFIRMACION):
        self.zona_confirmada = zona_inicial
        self.zona_candidata = zona_inicial
        self.frames_en_candidata = 0
        self.frames_confirmacion = frames_confirmacion

    def actualizar(self, zona_detectada):
        if zona_detectada == self.zona_candidata:
            self.frames_en_candidata += 1
        else:
            self.zona_candidata = zona_detectada
            self.frames_en_candidata = 1

        if self.frames_en_candidata >= self.frames_confirmacion:
            self.zona_confirmada = self.zona_candidata

        return self.zona_confirmada
