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

sys.path.insert(0, str(Path(__file__).resolve().parent / "logicaJuego"))

from juego import Juego

PUERTO = 8000
NUMERO_NOCHE_INICIAL = 1

INTERVALO_PING_SEGUNDOS = 20
TIMEOUT_PING_SEGUNDOS = 20

TIPOS_DE_TAREA = ["cables", "dials", "sequence", "rhythm"]
DURACION_POR_TIPO = {"cables": 30, "dials": 20, "sequence": 25, "rhythm": 15}
DESCRIPCION_POR_TIPO = {
    "cables": "Conectar los cables del color correcto",
    "dials": "Girar perillas a posición correcta",
    "sequence": "Resolver la secuencia mostrada",
    "rhythm": "Seguir el ritmo crítico",
}

_contador_task_id = itertools.count(1)


class ServidorJuego:
    def __init__(self):
        self.clienteTablet = None
        self.loopAsyncio = None
        self.juego = None
        self.playerIdActivo = None
        self.ultimaTareaEnviada = None

    def alEventoDeJuego(self, tipo, datos):
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

    def crearNuevaTarea(self):
        taskId = next(_contador_task_id)
        tipo = TIPOS_DE_TAREA[taskId % len(TIPOS_DE_TAREA)]
        numeroNoche = self.juego.numeroNoche if self.juego else 1
        mensaje = {
            "type": "new_task",
            "task_id": taskId,
            "task_type": tipo,
            "duration": DURACION_POR_TIPO[tipo],
            "description": DESCRIPCION_POR_TIPO[tipo],
            "difficulty": numeroNoche,
            "timestamp": int(time.time() * 1000),
            "task_params": self.crearTaskParams(tipo, numeroNoche),
        }
        self.ultimaTareaEnviada = mensaje
        self._enviarAlCliente(mensaje)

    def manejarMensajeDeTablet(self, mensaje):
        tipo = mensaje.get("type")

        if tipo == "connect":
            playerId = mensaje.get("player_id")
            print(f"Tablet conectada: player_id={playerId}")

            if self.juego is not None and self.playerIdActivo == playerId:
                print("Mismo jugador reconectando, restaurando sesión.")
                self._enviarAlCliente({
                    "type": "reconnect_success",
                    "session_data": {
                        "score": self.juego.tareasCompletadas * 100,
                        "time_elapsed": self.juego.segundosTranscurridosTotal,
                        "tasks_completed": self.juego.tareasCompletadas,
                        "tasks_failed": self.juego.tareasFallidas,
                        "current_task": self.ultimaTareaEnviada,
                    },
                    "timestamp": int(time.time() * 1000),
                })
            else:
                print("Jugador nuevo o partida anterior distinta, empezando de cero.")
                if self.juego is not None:
                    self.juego.juego = False
                self.playerIdActivo = playerId
                self.juego = None
                self.ultimaTareaEnviada = None
                self.iniciarJuego()

        elif tipo == "task_completed":
            if self.juego is not None:
                self.juego.registrarTareaCompletada()
            print(f"Tarea completada: task_id={mensaje.get('task_id')}")
            self.crearNuevaTarea()

        elif tipo == "task_failed":
            if self.juego is not None:
                self.juego.registrarTareaFallida()
            print(f"Tarea fallida: task_id={mensaje.get('task_id')} razon={mensaje.get('reason')}")
            self.crearNuevaTarea()

        elif tipo == "disconnect":
            print(f"Tablet se desconectó: razon={mensaje.get('reason')}")

    def iniciarJuego(self):
        if self.juego is not None:
            return
        self.juego = Juego(numeroNoche=NUMERO_NOCHE_INICIAL, alEventoDeJuego=self.alEventoDeJuego)
        hiloJuego = threading.Thread(target=self.juego.iniciar, daemon=True)
        hiloJuego.start()
        self.crearNuevaTarea()


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
