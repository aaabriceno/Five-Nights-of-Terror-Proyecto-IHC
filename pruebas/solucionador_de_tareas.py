"""Resuelve por código las tareas de la terminal del jugador 2 (mensaje `tarea` de Unity).

Lo usan las pruebas con el juego real para hacer de jugador 2 sin una persona. Es independiente
del generador de Unity (TareasDeOperador.cs): si Unity acepta lo que este solucionador responde,
la respuesta que guarda Unity es la que el enunciado pide.
"""
from itertools import product


def _minutos(hora):
    """"2:45" → minutos pasada la medianoche ("12:05" son 5)."""
    h, m = hora.split(":")
    return int(h) % 12 * 60 + int(m)


def resolver_calculo(tarea):
    """El número que pide un cálculo: `clase` y `datos` dicen qué es."""
    clase, datos = tarea["clase"], tarea["datos"]
    if clase == "aritmetica":                       # a + b  |  a × b  |  a × b + c  |  a × b − c
        a, operacion, b = int(datos[0]), datos[1], int(datos[2])
        valor = a + b if operacion == "+" else a * b
        if len(datos) == 5:
            valor += int(datos[4]) if datos[3] == "+" else -int(datos[4])
        return valor
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
    if clase == "problema":
        if datos[0] == "reloj":                     # minutos entre dos horas
            return _minutos(datos[2]) - _minutos(datos[1])
        if datos[0] == "vuelto":                    # pago − cantidad × precio
            return int(datos[3]) - int(datos[1]) * int(datos[2])
    raise ValueError(f"cálculo desconocido: {clase} {datos}")


def resolver_ecuacion(tarea):
    """La x de una ecuación. datos: forma, a, op, b, c con a·x op b = c, o a(x op b) = c."""
    forma, a, op, b, c = tarea["datos"]
    a, b, c = int(a), int(b), int(c)
    signo = 1 if op == "+" else -1
    if forma == "parentesis":
        return c // a - signo * b
    return (c - signo * b) // a


def respuesta_para(tarea):
    """El mensaje `tarea_respuesta` correcto para una `tarea` de Unity."""
    valor = resolver_ecuacion(tarea) if tarea["tipo"] == "ecuacion" else resolver_calculo(tarea)
    return {"type": "tarea_respuesta", "id": tarea["id"], "valor": valor}
