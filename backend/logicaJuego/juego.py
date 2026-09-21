from animatronicos import Animatronico
from puppet import Puppet
from gazeHilo import HiloGaze
import nivelIA
import random
import time

TICK_SEGUNDOS = 0.5
SEGUNDOS_ENTRE_ODM_NORMAL = 5
SEGUNDOS_ENTRE_ODM_PUPPET_FUERA_DE_CAJA = 1

PROBABILIDAD_TAREA_NUEVA_POR_NOCHE = {
    1: 10,
    2: 20,
    3: 30,
    4: 40,
    5: 50,
    6: 60,
}

class Juego:
    def __init__ (self,numeroNoche,juego = True, tiempoJuegoMinutos = 6, cantidadAnimatronicos = 3, alEventoDeJuego = None):
        self.numeroNoche = numeroNoche
        self.juego = juego
        self.tiempoJuegoMinutos = tiempoJuegoMinutos
        self.cantidadAnimatronicos = cantidadAnimatronicos
        self.horaJuego = 1
        self.segundosAcumulados = 0
        self.segundosTranscurridosTotal = 0
        self.tareasCompletadas = 0
        self.tareasFallidas = 0
        self.animatronicos = []
        self.tablasNivelIA = {}
        self.nodosDeObservacion = ["nodo3", "nodo6"]
        self.hiloGaze = HiloGaze()
        self.juegoTerminado = False
        self.jugadorMurio = False
        self.alEventoDeJuego = alEventoDeJuego
        self.grafoMapa = {
            "escenario": ["nodo1","nodo2","nodo3"],
            "nodo1": ["escenario","areaJuegos"],
            "nodo2": ["escenario","areaFiestas"],
            "areaJuegos": ["nodo1","nodo3","nodo4"],
            "nodo3": ["escenario","areaJuegos","areaFiestas","nodo6"],
            "areaFiestas": ["nodo2","nodo3","nodo5"],
            "nodo4":["areaJuegos","nodo6"],
            "nodo5":["areaFiestas","nodo6"],
            "nodo6":["nodo3","nodo4","nodo5","jugador"],
            "jugador":["nodo6"]
        }

        rutaFreddy = ["escenario","nodo1","areaJuegos","nodo4","nodo6","jugador"]
        rutaRex = ["escenario","nodo1","areaJuegos","nodo3","areaFiestas","nodo5","nodo6","jugador"]
        rutaVixy = ["escenario","nodo2","areaFiestas","nodo3","areaJuegos","nodo4","nodo6","jugador"]

        self.agregarAnimatronico("Freddy", "imgFreddy.png", "sonidoFreddy.mp3", ["habilidad1"], 5, nivelIA.nivel_IA_Animatronico1, rutaFreddy, ["temperatura", "ventiladores", "cables", "dials", "trazar_curso"])
        self.agregarPuppet("Puppet", "imgPuppet.png", "sonidoPuppet.mp3", ["habilidad1"], 5, nivelIA.nivel_IA_Animatronico2, rutaRex)
        self.agregarAnimatronico(
            "Vixy", "imgVixy.png", "sonidoVixy.mp3", ["habilidad1"], 5, nivelIA.nivel_IA_Animatronico3, rutaVixy,
            ["wifi", "sequence", "rhythm", "procesar_datos", "subir_datos"],
            dependenciasDeTarea={"subir_datos": "wifi"},
            requiereCompletadaAntes={"subir_datos": "procesar_datos"},
            horaMinimaPorTarea={"procesar_datos": 4, "subir_datos": 4},
        )

    def agregarAnimatronico(self, nombre, imagen, sonido, habilidades, tiempoMirarJugador, tablaNivelIA, ruta, tareasAsignadas=None, dependenciasDeTarea=None, requiereCompletadaAntes=None, horaMinimaPorTarea=None):
        nivelInicial = tablaNivelIA[self.numeroNoche]["inicial"]
        nuevoAnimatronico = Animatronico(nombre, imagen, sonido, habilidades, tiempoMirarJugador, nivelInicial, ruta, tareasAsignadas, dependenciasDeTarea, requiereCompletadaAntes, horaMinimaPorTarea)
        self.animatronicos.append(nuevoAnimatronico)
        self.tablasNivelIA[nombre] = tablaNivelIA

    def agregarPuppet(self, nombre, imagen, sonido, habilidades, tiempoMirarJugador, tablaNivelIA, ruta):
        nivelInicial = tablaNivelIA[self.numeroNoche]["inicial"]
        drenaje = nivelIA.drenaje_caja_Puppet[self.numeroNoche]
        subidaAlDarCuerda = nivelIA.subida_caja_Puppet_al_dar_cuerda[self.numeroNoche]
        nuevoPuppet = Puppet(nombre, imagen, sonido, habilidades, tiempoMirarJugador, nivelInicial, ruta, drenaje, subidaAlDarCuerda)
        self.animatronicos.append(nuevoPuppet)
        self.tablasNivelIA[nombre] = tablaNivelIA

    def obtenerPuppet(self):
        for animatronico in self.animatronicos:
            if isinstance(animatronico, Puppet):
                return animatronico
        return None

    def iniciarDarCuerda(self):
        puppet = self.obtenerPuppet()
        if puppet is not None:
            puppet.iniciarDarCuerda()

    def detenerDarCuerda(self):
        puppet = self.obtenerPuppet()
        if puppet is not None:
            puppet.detenerDarCuerda()

    def actualizarNivelesIA(self):
        for animatronico in self.animatronicos:
            tabla = self.tablasNivelIA[animatronico.nombre]
            animatronico.nivelDeIA = nivelIA.actualizar_nivel_IA(tabla, self.numeroNoche, self.horaJuego)

    def emitirEvento(self, tipo, datos):
        if self.alEventoDeJuego is not None:
            self.alEventoDeJuego(tipo, datos)

    def _formatearHoraEnJuego(self):
        # horaJuego va de 1 a 6, representando 12AM, 1AM, ..., 5AM (fiel
        # al reloj del juego original, que nunca llega a mostrar 6AM —
        # esa hora solo se usa como condición de fin de noche). Los
        # minutos se derivan de segundosAcumulados (0-59 in-game = un
        # minuto real, ver TICK_SEGUNDOS) para que el reloj no salte de
        # hora en hora sino que avance en vivo.
        horaVisible = 12 if self.horaJuego == 1 else self.horaJuego - 1
        minutos = int(self.segundosAcumulados)
        return f"{horaVisible}:{minutos:02d} AM"

    def _emitirEstadoDeNoche(self):
        self.emitirEvento("night_status", {
            "type": "night_status",
            "night": self.numeroNoche,
            "in_game_time": self._formatearHoraEnJuego(),
            "risk_percent": 0,
            "timestamp": int(time.time() * 1000),
        })

    def registrarTareaCompletada(self):
        self.tareasCompletadas += 1

    def registrarTareaFallida(self):
        self.tareasFallidas += 1

    def resolverTarea(self, tipoTarea):
        for animatronico in self.animatronicos:
            if tipoTarea in animatronico.tareasPendientes:
                animatronico.resolverTarea(tipoTarea)

    def intentarGenerarTareasNuevas(self):
        probabilidad = PROBABILIDAD_TAREA_NUEVA_POR_NOCHE.get(self.numeroNoche, 100)
        for animatronico in self.animatronicos:
            if not animatronico.tareasAsignadas:
                continue
            numero_aleatorio = random.randint(1, 100)
            if numero_aleatorio <= probabilidad:
                tareaGenerada, tareasCanceladas = animatronico.generarTareaPendiente(self.horaJuego)
                if tareaGenerada is not None:
                    self.emitirEvento("nueva_tarea_pendiente", {
                        "task_type": tareaGenerada,
                        "animatronic": animatronico.nombre,
                    })
                for tareaCancelada in tareasCanceladas:
                    self.emitirEvento("tarea_cancelada", {
                        "task_type": tareaCancelada,
                        "animatronic": animatronico.nombre,
                    })

    def _procesarAtaque(self, animatronico):
        animatronico.atacar(self)
        self.juegoTerminado = True
        self.jugadorMurio = True
        self.juego = False
        print(f"{animatronico.nombre} atacó al jugador! Game Over.")
        self.emitirEvento("attack", {
            "type": "attack",
            "attack_id": f"atk_{self.segundosTranscurridosTotal}",
            "damage": 100,
            "urgency": "critical",
            "animatronic": animatronico.nombre,
            "message": f"{animatronico.nombre} te encontró",
            "timestamp": int(time.time() * 1000),
        })

    def iniciar(self):
        self.hiloGaze.start()
        # Tick rápido (TICK_SEGUNDOS): la caja del Puppet, el reloj de la
        # noche y el tiempo de tareas pendientes avanzan en tiempo real,
        # como en el juego original — no en saltos de 3-5s.
        #
        # La Oportunidad De Movimiento (ODM) de Freddy y Vixy es fija en
        # SEGUNDOS_ENTRE_ODM_NORMAL (5s), sincronizada para ambos a la
        # vez — así funciona FNAF2 real (a diferencia de FNAF1, donde
        # cada animatronico tenía su propio timer aleatorio). Puppet, una
        # vez fuera de su caja, tiene ODM cada
        # SEGUNDOS_ENTRE_ODM_PUPPET_FUERA_DE_CAJA (1s) — mucho más
        # seguido que los demás, también fiel al original.
        proximaOdmNormal = SEGUNDOS_ENTRE_ODM_NORMAL
        proximaOdmPuppet = SEGUNDOS_ENTRE_ODM_PUPPET_FUERA_DE_CAJA

        while self.juego:
            time.sleep(TICK_SEGUNDOS)
            self.segundosAcumulados += TICK_SEGUNDOS
            self.segundosTranscurridosTotal += TICK_SEGUNDOS
            if self.segundosAcumulados >= 60 and self.horaJuego < 6:
                self.segundosAcumulados -= 60
                self.horaJuego += 1
                self.intentarGenerarTareasNuevas()
            self.actualizarNivelesIA()
            self._emitirEstadoDeNoche()

            proximaOdmNormal -= TICK_SEGUNDOS
            leTocaOdmNormal = proximaOdmNormal <= 0
            if leTocaOdmNormal:
                proximaOdmNormal = SEGUNDOS_ENTRE_ODM_NORMAL

            proximaOdmPuppet -= TICK_SEGUNDOS
            leTocaOdmPuppet = proximaOdmPuppet <= 0
            if leTocaOdmPuppet:
                proximaOdmPuppet = SEGUNDOS_ENTRE_ODM_PUPPET_FUERA_DE_CAJA

            zonaAtencion = self.hiloGaze.obtenerZona()
            for animatronico in self.animatronicos:
                atacoAlJugador = False

                if isinstance(animatronico, Puppet):
                    if animatronico.enCaja:
                        # En la noche 1 la caja no drena hasta la hora 2
                        # in-game (fiel al original) — antes de eso queda
                        # congelada en su valor máximo.
                        cajaCongelada = self.numeroNoche == 1 and self.horaJuego < 2
                        if not cajaCongelada:
                            animatronico.drenarCaja(TICK_SEGUNDOS)

                        # Se emite cada tick (no solo al cruzar el umbral
                        # de peligro) para que Flutter pueda animar una
                        # barra de progreso en tiempo real, fiel al
                        # medidor visible del juego original — acordado
                        # con el chat de Flutter el 2026-09-15.
                        self.emitirEvento("estado_puppet", {
                            "type": "estado_puppet",
                            "en_peligro": animatronico.estaEnPeligro(),
                            "valor_caja_porcentaje": animatronico.porcentajeCaja(),
                            "timestamp": int(time.time() * 1000),
                        })

                        if leTocaOdmPuppet:
                            animatronico.intentarSalirDeCaja()
                    else:
                        if leTocaOdmPuppet:
                            animatronico.moverse()
                        atacoAlJugador = animatronico.intentarAtacar()
                else:
                    if leTocaOdmNormal:
                        animatronico.moverse()
                    atacoAlJugador = animatronico.observar(zonaAtencion, TICK_SEGUNDOS, self.nodosDeObservacion)

                    if not atacoAlJugador:
                        atacoAlJugador = animatronico.intentarAtacarPorTareasPendientes(TICK_SEGUNDOS)

                if atacoAlJugador:
                    self._procesarAtaque(animatronico)
                    break

            if self.horaJuego >= 6 and self.segundosAcumulados >= 60:
                self.juego = False
                self.juegoTerminado = True

        self.emitirEvento("game_over", {
            "type": "game_over",
            "result": "loss" if self.jugadorMurio else "win",
            "final_stats": {
                "duration": self.segundosTranscurridosTotal,
                "tasks_completed": self.tareasCompletadas,
                "tasks_failed": self.tareasFallidas,
                "total_damage": 100 if self.jugadorMurio else 0,
                "score": self.tareasCompletadas * 100,
            },
            "timestamp": int(time.time() * 1000),
        })

        self.hiloGaze.detener()