import random
class Animatronico:
    def __init__ (self,nombre, imagen, sonido, habilidades,tiempoMirarJugador,nivelDeIA, ruta):
        self.nombre = nombre
        self.imagen = imagen
        self.sonido = sonido
        self.habilidades = habilidades
        self.tiempoMirarJugador = tiempoMirarJugador
        self.nivelDeIA = nivelDeIA
        self.ruta = ruta
        self.indiceRuta = 0
        self.posicion = ruta[0]
        self.segundosSinObservar = 0
        self.observando = False

    def moverse(self):
        numero_aleatorio = random.randint(0,20)
        if numero_aleatorio <= self.nivelDeIA:
            if self.indiceRuta < len(self.ruta) - 1:
                self.indiceRuta += 1
                self.posicion = self.ruta[self.indiceRuta]
                print(f"{self.nombre} avanza a {self.posicion}")
            else:
                print(f"{self.nombre} ya esta en el final de su ruta")

    def retroceder(self, pasos):
        self.indiceRuta = max(0, self.indiceRuta - pasos)
        self.posicion = self.ruta[self.indiceRuta]
        print(f"{self.nombre} retrocede a {self.posicion}")

    def observar(self, zonaAtencion, segundosTranscurridos, nodosDeObservacion):
        if self.posicion not in nodosDeObservacion:
            self.observando = False
            self.segundosSinObservar = 0
            return False

        self.observando = True
        if zonaAtencion == "PANTALLA":
            self.segundosSinObservar = 0
        else:
            self.segundosSinObservar += segundosTranscurridos

        return self.segundosSinObservar >= self.tiempoMirarJugador

    def atacar(self,ataque):
        pass