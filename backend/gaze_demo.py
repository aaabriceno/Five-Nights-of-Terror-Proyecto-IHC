"""Prototipo (spike): deteccion de mirada con MediaPipe Face Mesh + OpenCV.

Abre la camara y clasifica si el jugador mira a la pantalla, a la tablet o a
ninguna de las dos, combinando orientacion de cabeza (pitch/yaw) con la
posicion del iris en un vector de mirada 2D, calibrado contra la pantalla y
la tablet reales del jugador (ver gaze/calibracion.py). Presiona ESC para salir.

Uso:
    backend/venv/bin/python backend/gaze_demo.py [indice_camara]
    backend/venv/bin/python backend/gaze_demo.py --calibrar [indice_camara]
"""

import sys

import cv2
import mediapipe as mp

from gaze.calibracion import cargar_calibracion, ejecutar_calibracion
from gaze.deteccion import NADA, PANTALLA, TABLET, RastreadorDeZona, clasificar_zona
from gaze.estimacion import calcular_vector_mirada, leer_ojos_y_cabeza, punto_2d
from gaze.landmarks import IRIS_DER, IRIS_IZQ

MODO_CALIBRACION = "--calibrar" in sys.argv
_args_posicionales = [a for a in sys.argv[1:] if a != "--calibrar"]
INDICE_CAMARA = int(_args_posicionales[0]) if _args_posicionales else 0

TEXTOS_ESTADO = {
    PANTALLA: ("MIRANDO PANTALLA", (0, 200, 0)),
    TABLET: ("MIRANDO TABLET", (255, 200, 0)),
    NADA: ("IGNORANDO AMBAS (ATAQUE)", (0, 0, 255)),
}


def crear_malla_facial():
    return mp.solutions.face_mesh.FaceMesh(
        max_num_faces=1,
        refine_landmarks=True,
        static_image_mode=False,
        min_detection_confidence=0.5,
        min_tracking_confidence=0.5,
    )


def abrir_camara(indice):
    camara = cv2.VideoCapture(indice)
    if not camara.isOpened():
        print(f"No se pudo abrir la camara con indice {indice}")
        sys.exit(1)
    return camara


def dibujar_iris(cuadro, landmarks, ancho, alto):
    for indice in (IRIS_IZQ, IRIS_DER):
        x, y = punto_2d(landmarks, indice, ancho, alto)
        cv2.circle(cuadro, (int(x), int(y)), 3, (255, 255, 0), -1)


def ejecutar_loop_principal(camara, malla_facial, calibracion):
    rastreador = RastreadorDeZona()

    while camara.isOpened():
        ok, cuadro = camara.read()
        if not ok:
            break

        cuadro = cv2.flip(cuadro, 1)
        resultado = malla_facial.process(cv2.cvtColor(cuadro, cv2.COLOR_BGR2RGB))
        rostro_detectado = bool(resultado.multi_face_landmarks)

        if rostro_detectado:
            landmarks = resultado.multi_face_landmarks[0].landmark
            alto, ancho = cuadro.shape[:2]

            lectura = leer_ojos_y_cabeza(landmarks, ancho, alto)
            gx, gy = calcular_vector_mirada(lectura)
            zona_detectada = clasificar_zona(gx, gy, calibracion)

            dibujar_iris(cuadro, landmarks, ancho, alto)
            texto = f"gx={gx:.1f}  gy={gy:.1f}  iris=({lectura.iris_x:.2f},{lectura.iris_y:.2f})"
            cv2.putText(cuadro, texto, (30, 90), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (200, 200, 200), 2)
        else:
            zona_detectada = NADA

        zona_atencion = rastreador.actualizar(zona_detectada)

        estado, color = TEXTOS_ESTADO[zona_atencion]
        cv2.putText(cuadro, estado, (30, 50), cv2.FONT_HERSHEY_SIMPLEX, 1.0, color, 2)
        if not rostro_detectado:
            cv2.putText(cuadro, "rostro no detectado", (30, 130), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 165, 255), 2)

        cv2.imshow("Prototipo Gaze - Attention Defense (ESC para salir)", cuadro)
        if cv2.waitKey(5) & 0xFF == 27:
            break


def main():
    malla_facial = crear_malla_facial()
    camara = abrir_camara(INDICE_CAMARA)

    if MODO_CALIBRACION:
        ejecutar_calibracion(camara, malla_facial)
        camara.release()
        return

    calibracion = cargar_calibracion()
    if calibracion is None:
        print("No hay calibracion guardada. Corre primero: gaze_demo.py --calibrar")
        camara.release()
        sys.exit(1)

    print("Calibracion cargada.")
    ejecutar_loop_principal(camara, malla_facial, calibracion)

    camara.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
