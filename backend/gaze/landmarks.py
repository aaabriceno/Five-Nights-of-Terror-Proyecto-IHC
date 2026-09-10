"""Indices de landmarks de MediaPipe Face Mesh (con refine_landmarks=True).
Datos puros, sin logica.
"""

# esquinas de los ojos e iris (los ultimos 10 landmarks, 468-477, existen solo con refine_landmarks=True)
OJO_IZQ_EXTERIOR, OJO_IZQ_INTERIOR, IRIS_IZQ = 33, 133, 468
OJO_DER_INTERIOR, OJO_DER_EXTERIOR, IRIS_DER = 362, 263, 473

# parpados superior/inferior, para medir la posicion vertical del iris dentro del ojo
OJO_IZQ_PARPADO_SUP, OJO_IZQ_PARPADO_INF = 159, 145
OJO_DER_PARPADO_SUP, OJO_DER_PARPADO_INF = 386, 374

# puntos usados para estimar orientacion de cabeza con proporciones geometricas
# (ver gaze/estimacion.py) en vez de solvePnP: con la calidad de camara disponible,
# solvePnP con pocos puntos convergia a poses ambiguas (angulos que saltaban ±180
# grados sin razon fisica). Proporciones de distancias entre landmarks fijos son
# mas robustas para esta necesidad puntual (no requerimos angulos exactos en grados,
# solo una senal monotona de "cuanto se inclino/giro la cabeza").
PUNTA_NARIZ = 1
MENTON = 152
FRENTE = 10
POMULO_IZQ = 234
POMULO_DER = 454
