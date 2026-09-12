"""Hilo en segundo plano que corre la deteccion de mirada (camara + MediaPipe)
y expone la zona de atencion actual (PANTALLA / TABLET / NADA) para que
Juego la consulte en su propio loop, sin bloquearse entre si.
"""

import threading

import cv2
import mediapipe as mp

from gaze.calibracion import cargar_calibracion
from gaze.deteccion import NADA, RastreadorDeZona, clasificar_zona
from gaze.estimacion import calcular_vector_mirada, leer_ojos_y_cabeza


class HiloGaze(threading.Thread):
    def __init__(self, indiceCamara=0):
        super().__init__(daemon=True)
        self.indiceCamara = indiceCamara
        self.lock = threading.Lock()
        self.zonaAtencion = NADA
        self.corriendo = True

    def obtenerZona(self):
        with self.lock:
            return self.zonaAtencion

    def detener(self):
        self.corriendo = False

    def run(self):
        calibracion = cargar_calibracion()
        if calibracion is None:
            print("HiloGaze: no hay calibracion guardada, el hilo no puede iniciar.")
            return

        mallaFacial = mp.solutions.face_mesh.FaceMesh(
            max_num_faces=1,
            refine_landmarks=True,
            static_image_mode=False,
            min_detection_confidence=0.5,
            min_tracking_confidence=0.5,
        )
        camara = cv2.VideoCapture(self.indiceCamara)
        if not camara.isOpened():
            print(f"HiloGaze: no se pudo abrir la camara {self.indiceCamara}.")
            return

        rastreador = RastreadorDeZona()

        while self.corriendo and camara.isOpened():
            ok, cuadro = camara.read()
            if not ok:
                break

            cuadro = cv2.flip(cuadro, 1)
            resultado = mallaFacial.process(cv2.cvtColor(cuadro, cv2.COLOR_BGR2RGB))

            if resultado.multi_face_landmarks:
                landmarks = resultado.multi_face_landmarks[0].landmark
                alto, ancho = cuadro.shape[:2]
                lectura = leer_ojos_y_cabeza(landmarks, ancho, alto)
                gx, gy = calcular_vector_mirada(lectura)
                zonaDetectada = clasificar_zona(gx, gy, calibracion)
            else:
                zonaDetectada = NADA

            zonaConfirmada = rastreador.actualizar(zonaDetectada)
            with self.lock:
                self.zonaAtencion = zonaConfirmada

        camara.release()
