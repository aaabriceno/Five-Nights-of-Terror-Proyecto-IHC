"""Servidor WebSocket minimo que conecta la logica del juego (backend/logicaJuego/)
con la tablet Flutter, hablando el protocolo definido en
docs/FLUTTER_TABLET_ESPECIFICACION.md.

Uso:
    backend/venv/bin/python backend/servidor.py
"""

import asyncio
import itertools
import json
import random
import sys
import threading
import time
from pathlib import Path

import websockets

_DIR_BACKEND = Path(__file__).resolve().parent
sys.path.insert(0, str(_DIR_BACKEND / "logicaJuego"))
sys.path.insert(0, str(_DIR_BACKEND))

from juego import Juego
import partida

PUERTO = 8000
NUMERO_NOCHE_INICIAL = 1
NUMERO_NOCHE_FINAL = 6

INTERVALO_PING_SEGUNDOS = 20
TIMEOUT_PING_SEGUNDOS = 20

DURACION_POR_TIPO = {
    "cables": 30, "dials": 20, "sequence": 25, "rhythm": 15,
    "wifi": 15, "temperatura": 20, "ventiladores": 20,
    "procesar_datos": 15, "subir_datos": 12, "trazar_curso": 20,
}
DESCRIPCION_POR_TIPO = {
    "cables": "Conectar los cables del color correcto",
    "dials": "Girar perillas a posición correcta",
    "sequence": "Resolver la secuencia mostrada",
    "rhythm": "Seguir el ritmo crítico",
    "wifi": "Reiniciar WiFi",
    "temperatura": "Reparar temperatura",
    "ventiladores": "Reparar ventiladores",
    "procesar_datos": "Procesar datos",
    "subir_datos": "Subir datos",
    "trazar_curso": "Trazar curso",
}
# Con que animatronico esta emparejada cada tarea, para poder avisarle al
# Juego cuando se resuelve (Animatronico.resolverTarea) — debe coincidir
# con tareasAsignadas de cada Animatronico en juego.py.
TAREA_TIPO_GENERICO_INICIAL = "cables"

_contador_task_id = itertools.count(1)


class ServidorJuego:
    def __init__(self):
        self.clienteTablet = None
        self.loopAsyncio = None
        self.juego = None
        self.playerIdActivo = None
        self.tareasActivas = {}

    def alEventoDeJuego(self, tipo, datos):
        if tipo == "nueva_tarea_pendiente":
            self.crearTarea(datos["task_type"])

        if tipo == "tarea_cancelada":
            self.cancelarTareaActivaPorTipo(datos["task_type"])

        if tipo == "game_over" and self.playerIdActivo is not None and self.juego is not None:
            if self.juego.jugadorMurio:
                noche = self.juego.numeroNoche
            elif self.juego.numeroNoche >= NUMERO_NOCHE_FINAL:
                noche = NUMERO_NOCHE_INICIAL
                datos["result"] = "final_victory"
                print("¡Juego completado! Reiniciando progreso desde la noche 1.")
            else:
                noche = self.juego.numeroNoche + 1

            partida.guardarUltimaNoche(self.playerIdActivo, noche)
            print(f"Progreso guardado: player_id={self.playerIdActivo} próxima_noche={noche}")

        self._enviarAlCliente(datos)

    def _enviarAlCliente(self, datos):
        if self.clienteTablet is None or self.loopAsyncio is None:
            return
        asyncio.run_coroutine_threadsafe(
            self.clienteTablet.send(json.dumps(datos)), self.loopAsyncio
        )

    def crearTaskParams(self, tipo, numeroNoche):
        if tipo != "dials":
            return {}

        numDiales = 3 if numeroNoche >= 4 else 2
        tolerancia = 10 if numeroNoche >= 4 else 15
        targets = [random.randint(0, 11) * 30 for _ in range(numDiales)]
        return {
            "num_dials": numDiales,
            "targets": targets,
            "tolerance": tolerancia,
        }

    def crearTarea(self, tipo):
        taskId = next(_contador_task_id)
        numeroNoche = self.juego.numeroNoche if self.juego else 1
        mensaje = {
            "task_id": taskId,
            "task_type": tipo,
            "duration": DURACION_POR_TIPO[tipo],
            "description": DESCRIPCION_POR_TIPO[tipo],
            "difficulty": numeroNoche,
            "timestamp": int(time.time() * 1000),
            "task_params": self.crearTaskParams(tipo, numeroNoche),
        }
        self.tareasActivas[taskId] = mensaje
        self.enviarListaDeTareas()

    def cancelarTareaActivaPorTipo(self, tipo):
        idsACancelar = [
            taskId for taskId, tarea in self.tareasActivas.items()
            if tarea["task_type"] == tipo
        ]
        for taskId in idsACancelar:
            del self.tareasActivas[taskId]
            print(f"Tarea cancelada por dependencia rota: task_id={taskId} tipo={tipo}")

        if idsACancelar:
            self.enviarListaDeTareas()

    def enviarListaDeTareas(self):
        self._enviarAlCliente({
            "type": "task_list",
            "tasks": list(self.tareasActivas.values()),
            "timestamp": int(time.time() * 1000),
        })

    def manejarMensajeDeTablet(self, mensaje):
        tipo = mensaje.get("type")

        if tipo == "connect":
            playerId = mensaje.get("player_id")
            print(f"Tablet conectada: player_id={playerId}")

            # Cualquier connect corta la partida anterior si quedaba alguna
            # corriendo (ej. el jugador cerró la app a mitad de noche) — no
            # existe "retomar exactamente donde quedó", solo "elegir de
            # nuevo desde el menú": Nuevo Juego (noche 1) o Continuar
            # (última noche guardada), igual que en el FNAF original.
            if self.juego is not None:
                self.juego.juego = False
            self.playerIdActivo = playerId
            self.juego = None
            self.tareasActivas = {}

            modo = mensaje.get("modo", "nuevo")
            if modo == "continuar":
                numeroNoche = partida.obtenerUltimaNoche(playerId) or NUMERO_NOCHE_INICIAL
                print(f"Continuar: retomando en noche {numeroNoche}.")
            else:
                numeroNoche = NUMERO_NOCHE_INICIAL
                partida.guardarUltimaNoche(playerId, numeroNoche)
                print("Nuevo juego: empezando desde la noche 1.")

            self.iniciarJuego(numeroNoche)

        elif tipo == "task_completed":
            self._finalizarTarea(mensaje, exito=True)

        elif tipo == "task_failed":
            self._finalizarTarea(mensaje, exito=False)

        elif tipo == "disconnect":
            print(f"Tablet se desconectó: razon={mensaje.get('reason')}")

    def _finalizarTarea(self, mensaje, exito):
        taskId = mensaje.get("task_id")
        tareaOriginal = self.tareasActivas.pop(taskId, None)

        if self.juego is not None:
            if exito:
                self.juego.registrarTareaCompletada()
            else:
                self.juego.registrarTareaFallida()

            if tareaOriginal is not None:
                self.juego.resolverTarea(tareaOriginal["task_type"])

        print(f"Tarea {'completada' if exito else 'fallida'}: task_id={taskId}")

        if not self.tareasActivas and self.juego is not None:
            self.crearTarea(TAREA_TIPO_GENERICO_INICIAL)
        else:
            self.enviarListaDeTareas()

    def iniciarJuego(self, numeroNoche):
        if self.juego is not None:
            return
        self.juego = Juego(numeroNoche=numeroNoche, alEventoDeJuego=self.alEventoDeJuego)
        hiloJuego = threading.Thread(target=self.juego.iniciar, daemon=True)
        hiloJuego.start()
        self.crearTarea(TAREA_TIPO_GENERICO_INICIAL)


servidorJuego = ServidorJuego()


async def manejarConexion(websocket):
    servidorJuego.clienteTablet = websocket
    servidorJuego.loopAsyncio = asyncio.get_running_loop()
    print("Cliente conectado.")

    try:
        async for mensajeCrudo in websocket:
            mensaje = json.loads(mensajeCrudo)
            servidorJuego.manejarMensajeDeTablet(mensaje)
    except websockets.exceptions.ConnectionClosed:
        print("Cliente desconectado.")
    finally:
        if servidorJuego.clienteTablet is websocket:
            servidorJuego.clienteTablet = None


async def main():
    async with websockets.serve(
        manejarConexion,
        "0.0.0.0",
        PUERTO,
        ping_interval=INTERVALO_PING_SEGUNDOS,
        ping_timeout=TIMEOUT_PING_SEGUNDOS,
    ):
        print(f"Servidor escuchando en ws://0.0.0.0:{PUERTO}")
        await asyncio.Future()


if __name__ == "__main__":
    asyncio.run(main())
