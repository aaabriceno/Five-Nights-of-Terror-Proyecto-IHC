"""Resuelve por código las tareas de la terminal del jugador 2 (mensaje `tarea` de Unity).

Lo usan las pruebas con el juego real para hacer de jugador 2 sin una persona. Es independiente
del generador de Unity (TareasDeOperador.cs): si Unity acepta lo que este solucionador responde,
la respuesta que guarda Unity es la que el enunciado pide.
"""
from itertools import product


def resolver_calculo(tarea):
    """El número que pide un cálculo: `clase` y `datos` dicen qué es."""
    clase, datos = tarea["clase"], tarea["datos"]
    if clase == "binario":
        return int(datos[0], 2)
    if clase == "hexadecimal":
        return int(datos[0], 16)
    if clase == "aritmetica":                       # a + b  |  a × b  |  a × b + c  |  a × b − c
        a, operacion, b = int(datos[0]), datos[1], int(datos[2])
        valor = a + b if operacion == "+" else a * b
        if len(datos) == 5:
            valor += int(datos[4]) if datos[3] == "+" else -int(datos[4])
        return valor
    if clase == "memoria":
        n = int(datos[1])
        return 2 ** n if datos[0] == "valores" else 8 * n
    if clase == "serie":
        s = [int(x) for x in datos]
        diferencias = [y - x for x, y in zip(s, s[1:])]
        if len(set(diferencias)) == 1:                                   # aritmética
            return s[-1] + diferencias[0]
        razones = [y // x if x and y % x == 0 else None for x, y in zip(s, s[1:])]
        if None not in razones and len(set(razones)) == 1:               # geométrica
            return s[-1] * razones[0]
        if all(valor == (i + 1) ** 2 for i, valor in enumerate(s)):      # cuadrados
            return (len(s) + 1) ** 2
        return s[-1] + s[-2]                                             # tipo Fibonacci
    raise ValueError(f"clase de cálculo desconocida: {clase}")


def salida_del_circuito(tarea, interruptores):
    valores = {nombre: bool(v) for nombre, v in zip(tarea["entradas"], interruptores)}
    for puerta in tarea["puertas"]:
        a = valores[puerta["a"]]
        operacion = puerta["op"]
        if operacion == "NOT":
            valores[puerta["id"]] = not a
        elif operacion == "AND":
            valores[puerta["id"]] = a and valores[puerta["b"]]
        elif operacion == "OR":
            valores[puerta["id"]] = a or valores[puerta["b"]]
        else:                                                            # XOR
            valores[puerta["id"]] = a != valores[puerta["b"]]
    return valores[tarea["salida"]]


def resolver_circuito(tarea):
    """Unos interruptores (0 o 1 por entrada) con los que la salida es la pedida; None si no hay."""
    for combinacion in product((0, 1), repeat=len(tarea["entradas"])):
        if salida_del_circuito(tarea, combinacion) == bool(tarea["objetivo"]):
            return list(combinacion)
    return None


def respuesta_para(tarea):
    """El mensaje `tarea_respuesta` correcto para una `tarea` de Unity."""
    if tarea["tipo"] == "circuito":
        return {"type": "tarea_respuesta", "id": tarea["id"], "interruptores": resolver_circuito(tarea)}
    return {"type": "tarea_respuesta", "id": tarea["id"], "valor": resolver_calculo(tarea)}
