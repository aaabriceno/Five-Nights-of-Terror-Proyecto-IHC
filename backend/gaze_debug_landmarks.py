"""Herramienta de diagnostico (no parte del juego): imprime en consola las
coordenadas normalizadas (0-1) de los landmarks usados para orientacion de
cabeza, cada ~1 segundo, para poder elegir/ajustar la formula de pitch/yaw
con datos reales en vez de a ciegas.

Uso:
    backend/venv/bin/python backend/gaze_debug_landmarks.py [indice_camara]
"""

import sys
import time

import cv2
import mediapipe as mp

from gaze.landmarks import FRENTE, MENTON, POMULO_DER, POMULO_IZQ, PUNTA_NARIZ

INDICE_CAMARA = int(sys.argv[1]) if len(sys.argv) > 1 else 0


def main():
    malla_facial = mp.solutions.face_mesh.FaceMesh(max_num_faces=1, refine_landmarks=True)
    camara = cv2.VideoCapture(INDICE_CAMARA)
    if not camara.isOpened():
        print(f"No se pudo abrir la camara con indice {INDICE_CAMARA}")
        sys.exit(1)

    print("Moviendo la cabeza (pantalla normal / agachada / girada), ESC para salir.")
    print("frente_y  nariz_y  menton_y  |  pomulo_izq_x  nariz_x  pomulo_der_x")

    ultimo_print = 0.0
    while camara.isOpened():
        ok, cuadro = camara.read()
        if not ok:
            break
        cuadro = cv2.flip(cuadro, 1)
        resultado = malla_facial.process(cv2.cvtColor(cuadro, cv2.COLOR_BGR2RGB))

        if resultado.multi_face_landmarks and time.time() - ultimo_print > 0.5:
            lm = resultado.multi_face_landmarks[0].landmark
            print(
                f"{lm[FRENTE].y:.3f}    {lm[PUNTA_NARIZ].y:.3f}   {lm[MENTON].y:.3f}   |  "
                f"{lm[POMULO_IZQ].x:.3f}      {lm[PUNTA_NARIZ].x:.3f}   {lm[POMULO_DER].x:.3f}"
            )
            ultimo_print = time.time()

        cv2.imshow("Debug landmarks (ESC para salir)", cuadro)
        if cv2.waitKey(5) & 0xFF == 27:
            break

    camara.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
