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

# Valores reales de FNAF2: 20 x numeroNoche por segundo (noches 2-6).
# La noche 1 no drena hasta la hora 2 in-game, y a partir de ahí drena al
# mismo ritmo que la noche 2 (40/seg) — ver Juego.iniciar(), que aplica
# ese freeze consultando self.horaJuego antes de llamar drenarCaja().
drenaje_caja_Puppet = {
    1: 40,
    2: 40,
    3: 60,
    4: 80,
    5: 100,
    6: 120,
}

# Cuanto sube self.valorCaja por segundo mientras el jugador sostiene el
# botón de "dar cuerda" en la tablet. La tasa exacta del juego original
# no está documentada públicamente — se define como el doble del
# drenaje de esa noche, para que sostener el botón sea siempre viable
# (repone más rápido de lo que se pierde) sin importar la dificultad.
subida_caja_Puppet_al_dar_cuerda = {
    noche: drenaje * 2 for noche, drenaje in drenaje_caja_Puppet.items()
}

def actualizar_nivel_IA(tabla_nivel_IA, numeroNoche, horaActual):
    datosNoche = tabla_nivel_IA[numeroNoche]
    nivel = datosNoche["inicial"]
    for hora, aumento in datosNoche["subidaIA"].items():
        if hora <= horaActual:
            nivel += aumento
    return nivel