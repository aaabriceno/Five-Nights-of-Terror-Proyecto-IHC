import random

PORCENTAJE_ATAQUE_POR_SEGUNDO_TAREA_PENDIENTE = 2

class Animatronico:
    def __init__ (self,nombre, imagen, sonido, habilidades,tiempoMirarJugador,nivelDeIA, ruta, tareasAsignadas=None, dependenciasDeTarea=None, requiereCompletadaAntes=None, horaMinimaPorTarea=None):
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
        # tareasAsignadas: lista de task_type que este animatronico puede
        # generar (ej. Freddy: temperatura/ventiladores/cables/dials).
        self.tareasAsignadas = tareasAsignadas or []
        # tareasPendientes: dict {task_type: segundosSinResolver} de las
        # tareas de este animatronico actualmente sin resolver. Mientras
        # más tiempo pasa sin resolverse, más sube su propia probabilidad
        # de ataque.
        self.tareasPendientes = {}
        # tareasCompletadasAlgunaVez: set de task_type que el jugador ya
        # resolvió al menos una vez en esta noche — para dependencias tipo
        # "necesita haberse hecho antes" (ej. subir_datos necesita que
        # procesar_datos ya se haya completado), distinto de "no debe
        # estar roto ahora mismo" (ver dependenciasDeTarea).
        self.tareasCompletadasAlgunaVez = set()
        # dependenciasDeTarea: dict {tarea: tarea_de_la_que_depende} —
        # una tarea dependiente no se puede generar ni queda vigente
        # mientras su dependencia esté pendiente (ej. "subir_datos"
        # depende de "wifi": sin wifi resuelto, subir_datos ni aparece).
        self.dependenciasDeTarea = dependenciasDeTarea or {}
        # requiereCompletadaAntes: dict {tarea: tarea_previa_requerida} —
        # una tarea no puede generarse hasta que la tarea previa se haya
        # completado (task_completed) al menos una vez en la noche (ej.
        # "subir_datos" requiere que "procesar_datos" ya se haya hecho).
        self.requiereCompletadaAntes = requiereCompletadaAntes or {}
        # horaMinimaPorTarea: dict {tarea: horaJuego_minima} — una tarea
        # no puede generarse antes de esa hora in-game (ej. "procesar_datos"
        # y "subir_datos" no tienen sentido narrativo antes de la hora 4).
        self.horaMinimaPorTarea = horaMinimaPorTarea or {}

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

    def tareasDisponiblesParaGenerar(self, horaJuego):
        disponibles = []
        for tarea in self.tareasAsignadas:
            if tarea in self.tareasPendientes:
                continue

            dependencia = self.dependenciasDeTarea.get(tarea)
            if dependencia is not None and dependencia in self.tareasPendientes:
                continue

            tareaPrevia = self.requiereCompletadaAntes.get(tarea)
            if tareaPrevia is not None and tareaPrevia not in self.tareasCompletadasAlgunaVez:
                continue

            horaMinima = self.horaMinimaPorTarea.get(tarea)
            if horaMinima is not None and horaJuego < horaMinima:
                continue

            disponibles.append(tarea)
        return disponibles

    def generarTareaPendiente(self, horaJuego):
        disponibles = self.tareasDisponiblesParaGenerar(horaJuego)
        if not disponibles:
            return None, []
        tareaElegida = random.choice(disponibles)
        self.tareasPendientes[tareaElegida] = 0
        print(f"{self.nombre} genera tarea pendiente: {tareaElegida}")

        # Si otras tareas dependían de esta (ej. "subir_datos" depende de
        # "wifi"), y "wifi" se acaba de romper, esas tareas dependientes
        # que ya estaban pendientes quedan imposibles de resolver — se
        # cancelan en vez de dejarlas ahí bloqueadas sin explicación.
        tareasCanceladas = [
            tarea for tarea, dependencia in self.dependenciasDeTarea.items()
            if dependencia == tareaElegida and tarea in self.tareasPendientes
        ]
        for tareaCancelada in tareasCanceladas:
            del self.tareasPendientes[tareaCancelada]
            print(f"{tareaCancelada} cancelada: dependía de {tareaElegida}, que se acaba de romper")

        return tareaElegida, tareasCanceladas

    def resolverTarea(self, tipoTarea):
        if tipoTarea in self.tareasPendientes:
            del self.tareasPendientes[tipoTarea]
            self.tareasCompletadasAlgunaVez.add(tipoTarea)
            print(f"{tipoTarea} resuelta, ya no cuenta para el ataque de {self.nombre}")

    def acumularTiempoDeEspera(self, segundosTranscurridos):
        for tipoTarea in self.tareasPendientes:
            self.tareasPendientes[tipoTarea] += segundosTranscurridos

    def probabilidadDeAtaquePorTareas(self):
        if not self.tareasPendientes:
            return 0
        segundosMaximos = max(self.tareasPendientes.values())
        return min(100, segundosMaximos * PORCENTAJE_ATAQUE_POR_SEGUNDO_TAREA_PENDIENTE)

    def intentarAtacarPorTareasPendientes(self, segundosTranscurridos):
        if not self.tareasPendientes:
            return False

        self.acumularTiempoDeEspera(segundosTranscurridos)
        probabilidad = self.probabilidadDeAtaquePorTareas()
        numero_aleatorio = random.randint(1, 100)
        return numero_aleatorio <= probabilidad

    def atacar(self,ataque):
        pass