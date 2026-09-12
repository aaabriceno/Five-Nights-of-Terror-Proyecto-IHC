"""Persistencia mínima de progreso: qué noche alcanzó cada jugador.

El servidor es la única fuente de verdad del progreso — la tablet no
guarda nada, solo pide "nuevo juego" o "continuar" al conectar. Usa
SQLite (backend/partida.db, gitignored) para sobrevivir reinicios del
servidor sin necesitar un motor de base de datos aparte.
"""

import sqlite3
from pathlib import Path

ARCHIVO_BASE_DATOS = Path(__file__).resolve().parent / "partida.db"


def _conectar():
    conexion = sqlite3.connect(ARCHIVO_BASE_DATOS)
    conexion.execute(
        """
        CREATE TABLE IF NOT EXISTS progreso (
            player_id TEXT PRIMARY KEY,
            ultima_noche_alcanzada INTEGER NOT NULL
        )
        """
    )
    return conexion


def obtenerUltimaNoche(playerId):
    """Devuelve la última noche alcanzada por el jugador, o None si nunca jugó."""
    with _conectar() as conexion:
        fila = conexion.execute(
            "SELECT ultima_noche_alcanzada FROM progreso WHERE player_id = ?",
            (playerId,),
        ).fetchone()
        return fila[0] if fila else None


def guardarUltimaNoche(playerId, numeroNoche):
    with _conectar() as conexion:
        conexion.execute(
            """
            INSERT INTO progreso (player_id, ultima_noche_alcanzada)
            VALUES (?, ?)
            ON CONFLICT(player_id) DO UPDATE SET ultima_noche_alcanzada = excluded.ultima_noche_alcanzada
            """,
            (playerId, numeroNoche),
        )


def borrarProgreso(playerId):
    with _conectar() as conexion:
        conexion.execute("DELETE FROM progreso WHERE player_id = ?", (playerId,))
