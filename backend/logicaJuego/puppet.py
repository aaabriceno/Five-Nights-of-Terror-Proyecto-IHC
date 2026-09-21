import random

from animatronicos import Animatronico

VALOR_MAXIMO_CAJA = 2000
UMBRAL_PELIGRO_CAJA = VALOR_MAXIMO_CAJA * 0.2


class Puppet(Animatronico):
    def __init__(self, nombre, imagen, sonido, habilidades, tiempoMirarJugador, nivelDeIA, ruta, drenajePorSegundo, subidaPorSegundoAlDarCuerda):
        super().__init__(nombre, imagen, sonido, habilidades, tiempoMirarJugador, nivelDeIA, ruta)
        self.drenajePorSegundo = drenajePorSegundo
        self.subidaPorSegundoAlDarCuerda = subidaPorSegundoAlDarCuerda
        self.valorCaja = VALOR_MAXIMO_CAJA
        self.enCaja = True
        # dandoCuerda: True mientras el jugador sostiene el botón de "dar
        # cuerda" en la tablet — mientras esté sostenido, la caja sube en
        # vez de drenarse. Lo controla el servidor vía
        # iniciarDarCuerda()/detenerDarCuerda(), según los mensajes
        # 'dar_cuerda_inicio'/'dar_cuerda_fin' que manda la tablet.
        self.dandoCuerda = False

    def iniciarDarCuerda(self):
        self.dandoCuerda = True

    def detenerDarCuerda(self):
        self.dandoCuerda = False

    def drenarCaja(self, segundosTranscurridos):
        if not self.enCaja:
            return
        if self.dandoCuerda:
            self.valorCaja = min(
                VALOR_MAXIMO_CAJA,
                self.valorCaja + self.subidaPorSegundoAlDarCuerda * segundosTranscurridos,
            )
        else:
            self.valorCaja = max(0, self.valorCaja - self.drenajePorSegundo * segundosTranscurridos)

    def intentarSalirDeCaja(self):
        # Fiel al original: una vez la caja llega a 0, Puppet tiene ODM
        # cada 1 segundo (mismo intervalo que su ODM fuera de la caja) —
        # el primer acierto ya lo saca, sin contador de intentos previos.
        if not self.enCaja or self.valorCaja > 0:
            return

        numero_aleatorio = random.randint(0, 20)
        if numero_aleatorio <= self.nivelDeIA:
            self.enCaja = False
            print(f"{self.nombre} ha salido de su caja musical")

    def intentarAtacar(self):
        # Fiel al original: una vez fuera de la caja, Puppet no falla —
        # avanza por su ruta (grafo) y mata con certeza al llegar al
        # jugador. No hay tirada de dado ni forma de detenerlo; la única
        # salvación es que la noche termine (hora 6) antes de que llegue.
        return not self.enCaja and self.posicion == "jugador"

    def estaEnPeligro(self):
        return self.enCaja and self.valorCaja < UMBRAL_PELIGRO_CAJA

    def porcentajeCaja(self):
        return round(self.valorCaja / VALOR_MAXIMO_CAJA * 100)
