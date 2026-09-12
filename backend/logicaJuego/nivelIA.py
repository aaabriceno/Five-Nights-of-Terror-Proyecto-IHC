import random

nivel_IA_Animatronico1 = {
    1: {"inicial": 0, "subidaIA": {2:0, 3:0, 4:0}},
    2: {"inicial": 0, "subidaIA": {2:0, 3:0, 4:0}},
    3: {"inicial": 1, "subidaIA": {2:0, 3:0, 4:0}},
    4: {"inicial": 0, "subidaIA": {2:0, 3:0, 4:0}},
    5: {"inicial": 3, "subidaIA": {2:0, 3:0, 4:0}},
    6: {"inicial": 4, "subidaIA": {2:0, 3:0, 4:0}},
}


nivel_IA_Animatronico2 = {
    1: {"inicial": 0, "subidaIA": {2:1, 3:1, 4:1}},
    2: {"inicial": 3, "subidaIA": {2:1, 3:1, 4:1}},
    3: {"inicial": 0, "subidaIA": {2:1, 3:1, 4:1}},
    4: {"inicial": 2, "subidaIA": {2:1, 3:1, 4:1}},
    5: {"inicial": 5, "subidaIA": {2:1, 3:1, 4:1}},
    6: {"inicial": 10, "subidaIA": {2:1, 3:1, 4:1}},
}

nivel_IA_Animatronico3 = {
    1: {"inicial": 0, "subidaIA": {2:1, 3:1, 4:1}},
    2: {"inicial": 1, "subidaIA": {2:1, 3:1, 4:1}},
    3: {"inicial": 5, "subidaIA": {2:1, 3:1, 4:1}},
    4: {"inicial": 4, "subidaIA": {2:1, 3:1, 4:1}},
    5: {"inicial": 7, "subidaIA": {2:1, 3:1, 4:1}},
    6: {"inicial": 12, "subidaIA": {2:1, 3:1, 4:1}},
}

drenaje_caja_Puppet = {
    1: 2,
    2: 3,
    3: 4,
    4: 4,
    5: 5,
    6: 6,
}

def actualizar_nivel_IA(tabla_nivel_IA, numeroNoche, horaActual):
    datosNoche = tabla_nivel_IA[numeroNoche]
    nivel = datosNoche["inicial"]
    for hora, aumento in datosNoche["subidaIA"].items():
        if hora <= horaActual:
            nivel += aumento
    return nivel