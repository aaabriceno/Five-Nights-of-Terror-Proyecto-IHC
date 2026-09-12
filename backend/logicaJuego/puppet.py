import random

from animatronicos import Animatronico

VALOR_MAXIMO_CAJA = 2000
ACIERTOS_NECESARIOS_PARA_SALIR = 3
PROBABILIDAD_ATAQUE_PORCENTAJE = 10


class Puppet(Animatronico):
    def __init__(self, nombre, imagen, sonido, habilidades, tiempoMirarJugador, nivelDeIA, ruta, drenajePorSegundo):
        super().__init__(nombre, imagen, sonido, habilidades, tiempoMirarJugador, nivelDeIA, ruta)
        self.drenajePorSegundo = drenajePorSegundo
        self.valorCaja = VALOR_MAXIMO_CAJA
        self.enCaja = True
        self.aciertosParaSalir = 0

    def drenarCaja(self, segundosTranscurridos):
        if not self.enCaja:
            return
        self.valorCaja = max(0, self.valorCaja - self.drenajePorSegundo * segundosTranscurridos)

    def intentarSalirDeCaja(self):
        if not self.enCaja or self.valorCaja > 0:
            return

        numero_aleatorio = random.randint(0, 20)
        if numero_aleatorio <= self.nivelDeIA:
            self.aciertosParaSalir += 1
            print(f"{self.nombre} acierta ODM para salir ({self.aciertosParaSalir}/{ACIERTOS_NECESARIOS_PARA_SALIR})")
            if self.aciertosParaSalir >= ACIERTOS_NECESARIOS_PARA_SALIR:
                self.enCaja = False
                print(f"{self.nombre} ha salido de su caja musical")

    def intentarAtacar(self):
        if self.enCaja or self.posicion != "jugador":
            return False
        numero_aleatorio = random.randint(1, 100)
        return numero_aleatorio <= PROBABILIDAD_ATAQUE_PORCENTAJE
