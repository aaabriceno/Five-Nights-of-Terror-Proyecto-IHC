**OpenCV es una biblioteca de visión por computadora de propósito general y código abierto, mientras que MediaPipe es un framework de Google diseñado específicamente para construir pipelines de aprendizaje automático en tiempo real para medios multimodales.**

La diferencia principal radica en su enfoque y facilidad de uso:

*   **OpenCV** ofrece un control granular y una amplia gama de algoritmos para procesamiento de imágenes y video, siendo ideal para tareas personalizadas, investigación y aplicaciones industriales complejas que requieren flexibilidad total.
*   **MediaPipe** prioriza la facilidad de uso y la implementación rápida en dispositivos móviles y web, proporcionando soluciones preentrenadas listas para usar para tareas específicas como detección de manos, rostro y pose humana.

**Comparativa de características clave:**

| Característica | OpenCV | MediaPipe |
| :--- | :--- | :--- |
| **Enfoque Principal** | Visión por computadora general y procesamiento de imágenes. | Pipelines de ML en tiempo real para medios (video/audio). |
| **Desarrollador** | Comunidad (originalmente Intel). | Google. |
| **Facilidad de Uso** | Requiere conocimientos profundos de programación para tareas avanzadas. | APIs simples y modelos preentrenados para despliegue rápido. |
| **Optimización** | Optimizado para múltiples núcleos y aceleración por hardware general. | Altamente optimizado para despliegue en dispositivos móviles y edge computing. |
| **Personalización** | Máxima flexibilidad para modificar algoritmos individuales. | Limitada a ajustar modelos preexistentes o construir grafos modulares. |

En resumen, **OpenCV** es la herramienta preferida para construir sistemas de visión desde cero o con necesidades especializadas, mientras que **MediaPipe** es la opción ideal para integrar rápidamente capacidades de percepción (como reconocimiento facial o de gestos) en aplicaciones móviles o web con alto rendimiento en tiempo real.

## Detección de mirada para un juego tipo FNAF

Para tu caso, **MediaPipe Face Mesh** es la herramienta ideal. La mecánica sería: si el jugador **mira la pantalla (webcam)** → los animatrónicos se congelan; si **desvía la mirada** → pueden atacar.

### Cómo funciona

MediaPipe Face Mesh con `refine_landmarks=True` detecta los **468 puntos faciales + iris**. Con la posición del iris relativa a las esquinas del ojo, puedes clasificar si la persona mira al frente (hacia la webcam) o a otro lado.

### Landmarks clave

| Punto | Índice |
|:---|:---:|
| Esquina exterior ojo izquierdo | 33 |
| Esquina interior ojo izquierdo | 133 |
| Centro iris izquierdo | 468 |
| Esquina interior ojo derecho | 362 |
| Esquina exterior ojo derecho | 263 |
| Centro iris derecho | 473 |

### Ejemplo básico (Python)

```python
import cv2
import mediapipe as mp
import numpy as np

mp_face_mesh = mp.solutions.face_mesh
face_mesh = mp_face_mesh.FaceMesh(
    max_num_faces=1,
    refine_landmarks=True,  # ← activa el tracking del iris
    static_image_mode=False,
    min_detection_confidence=0.5,
)

cap = cv2.VideoCapture(0)

def normalized_position(outer, inner, iris):
    """Posición normalizada del iris entre esquina exterior e interior (0 a 1)."""
    x_range = abs(inner[0] - outer[0])
    if x_range == 0:
        return 0.5
    return (iris[0] - min(outer[0], inner[0])) / x_range

while cap.isOpened():
    success, frame = cap.read()
    if not success:
        break

    frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = face_mesh.process(frame_rgb)

    if results.multi_face_landmarks:
        landmarks = results.multi_face_landmarks[0].landmark
        h, w, _ = frame.shape

        # Extraer puntos
        l_outer = (landmarks[33].x * w,  landmarks[33].y * h)
        l_inner = (landmarks[133].x * w, landmarks[133].y * h)
        l_iris  = (landmarks[468].x * w, landmarks[468].y * h)

        r_outer = (landmarks[263].x * w, landmarks[263].y * h)
        r_inner = (landmarks[362].x * w, landmarks[362].y * h)
        r_iris  = (landmarks[473].x * w, landmarks[473].y * h)

        # Posición promedio del iris
        avg = (normalized_position(l_outer, l_inner, l_iris) +
               normalized_position(r_outer, r_inner, r_iris)) / 2

        # Umbral: entre 0.35 y 0.65 → mirando al frente
        looking_at_screen = 0.35 < avg < 0.65

        status = "MIRANDO ✅" if looking_at_screen else "¡NO MIRA! ⚠️"
        color  = (0, 255, 0) if looking_at_screen else (0, 0, 255)
        cv2.putText(frame, status, (30, 50),
                    cv2.FONT_HERSHEY_SIMPLEX, 1.2, color, 2)

        # Aquí integras la lógica del juego:
        # if not looking_at_screen:
        #     animatronics_advance()
        # else:
        #     animatronics_freeze()

    cv2.imshow("FNAF Gaze", frame)
    if cv2.waitKey(5) & 0xFF == 27:
        break

cap.release()
```

### Consejos para el juego

- **Umbral ajustable**: los valores `0.35 / 0.65` son un starting point. Calibra con tu webcam y distancia al jugador.
- **Filtro temporal**: no reacciones a un solo frame. Usa un contador (ej. si mira a otro lado durante >15 frames consecutivos → activar ataque) para evitar falsos positivos por parpadeo.
- **Integración con Pygame**: corre la detección en un thread separado y pasa el booleano `looking_at_screen` al loop del juego.
- **Precisión**: para un prototipo esto es suficiente. Si necesitas mayor fiabilidad, puedes añadir un filtro Kalman para suavizar o un modelo CNN de gaze estimation (como Gaze360), pero para un juego indie el método por landmarks es más que adecuado.

En resumen: **MediaPipe** es la herramienta correcta aquí (no OpenCV solo), porque ya viene con el modelo de iris preentrenado y corre en tiempo real con una webcam común.

**No, no necesitas clonar nada de GitHub.** Para usar MediaPipe en Python solo necesitas instalarlo con `pip`:

```bash
pip install mediapipe
```

Eso descarga el paquete precompilado desde **PyPI** (el registro oficial de Python) junto con todas sus dependencias. Inmediatamente después puedes hacer:

```python
import mediapipe as mp
```

### ¿Para qué sirve el repo de GitHub entonces?

| Uso | Método |
|:---|:---|
| **Python** (tu caso) | `pip install mediapipe` ← solo esto |
| C++, Android, iOS | Clonar el repo + Bazel + build manual |
| Ver ejemplos / contribuir | [github.com/google-ai-edge/mediapipe](https://github.com/google-ai-edge/mediapipe) |

El repo de GitHub es para el código fuente del framework completo (necesario si quieres compilarlo desde cero para otros lenguajes o plataformas). Para Python, el paquete de PyPI ya incluye todo lo que necesitas: los modelos preentrenados (Face Mesh, Iris, Hands, Pose, etc.) vienen embebidos dentro.

### Requisitos mínimos

- **Python 3.9 – 3.12** (no soporta 3.13 aún)
- **OpenCV** viene como dependencia automática al instalar mediapipe, así que no lo tienes que instalar aparte

Así que en resumen: una sola línea en tu terminal y ya tienes todo listo para el código del ejemplo anterior.

## La duda clave: ¿pantalla o cámara?

**MediaPipe detecta la dirección de la mirada relativa a la CÁMARA, no a la pantalla.** Pero en tu caso esto no es un problema si posicionas bien la webcam:

- **Webcam arriba del monitor** (o laptop) → mirar la pantalla ≈ mirar la cámara → MediaPipe detecta "mirando al frente" ✅
- **Webcam en otro lado** (ej. un web cam lateral) → hay un ángulo de desviación y el umbral se complica

**Conclusión**: Coloca la webcam centrada en la parte superior del monitor. Así "mirar la pantalla" y "mirar la cámara" son prácticamente lo mismo, y los landmarks del iris te dan la señal correcta.

---

## Arquitectura de tu proyecto

```
┌─────────────────────────────────────────────────────┐
│  MÓVIL / TABLET                                      │
│  (minijuegos)                                        │
│  Envía datos vía WiFi (WebSocket / HTTP)             │
└──────────────────────┬──────────────────────────────┘
                       │ WiFi
                       ▼
┌─────────────────────────────────────────────────────┐
│  PC (Server)                                         │
│                                                      │
│  ┌──────────────────────────────────────────────┐   │
│  │  Unity (juego principal tipo FNAF)           │   │
│  │  - Recibe datos del móvil (minijuegos)       │   │
│  │  - Recibe estado de mirada (true/false)      │   │
│  │  - Lógica: si no mira → animatronics avanzan │   │
│  └──────────────────────────────────────────────┘   │
│                                                      │
│  ┌──────────────────────────────────────────────┐   │
│  │  Script Python (MediaPipe)                   │   │
│  │  - Lee webcam                                │   │
│  │  - Detecta si mira al frente o no            │   │
│  │  - Envía booleano a Unity vía socket local   │   │
│  └──────────────────────────────────────────────┘   │
│                                                      │
│  ┌──────────────────────────────────────────────┐   │
│  │  Server WebSocket (Python / Node)             │   │
│  │  - Recibe datos del móvil                    │   │
│  │  - Reenvía a Unity                           │   │
│  └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

---

## Integración Unity ↔ MediaPipe

Tienes **dos opciones**:

| Opción | Cómo | Ventaja | Desventaja |
|:---|:---|:---|:---|
| **A. Python + Socket** (recomendada) | Script Python corre aparte, envía `true/false` a Unity por TCP/UDP local | Simple, no tocas el build de Unity | Un proceso extra |
| **B. MediaPipe Unity Plugin** | [homuler/MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin) → C# dentro de Unity | Todo en un solo proceso | Más complejo, requiere build nativo |

**Para un curso, la Opción A es lo más práctico.** Ejemplo minimalista:

**Python (envía estado de mirada):**
```python
import socket, cv2, mediapipe as mp, time

mp_face_mesh = mp.solutions.face_mesh
face_mesh = mp_face_mesh.FaceMesh(refine_landmarks=True)
cap = cv2.VideoCapture(0)
server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind(('127.0.0.1', 9999))
server.listen(1)
conn, _ = server.accept()

while True:
    _, frame = cap.read()
    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = face_mesh.process(rgb)
    
    looking = False
    if results.multi_face_landmarks:
        lm = results.multi_face_landmarks[0].landmark
        h, w = frame.shape[:2]
        # (misma lógica de iris que antes)
        l_pos = (lm[468].x - lm[33].x) / (lm[133].x - lm[33].x)
        r_pos = (lm[473].x - lm[263].x) / (lm[362].x - lm[263].x)
        avg = (l_pos + r_pos) / 2
        looking = 0.35 < avg < 0.65
    
    conn.sendall(("1" if looking else "0").encode())
    time.sleep(1/30)  # 30 Hz
```

**C# en Unity (recibe):**
```csharp
using UnityEngine;
using System.Net.Sockets;

public class GazeReceiver : MonoBehaviour
{
    TcpClient client;
    NetworkStream stream;
    public bool isLooking = true;

    void Start()
    {
        client = new TcpClient("127.0.0.1", 9999);
        stream = client.GetStream();
        InvokeRepeating("ReadGaze", 0f, 0.03f);
    }

    void ReadGaze()
    {
        byte[] buffer = new byte[1];
        if (stream.DataAvailable)
        {
            stream.Read(buffer, 0, 1);
            isLooking = buffer[0] == '1';
        }
    }

    void Update()
    {
        if (!isLooking)
            // Animatronics avanzan
            ;
        else
            // Se congelan
            ;
    }
}
```

---

## Para el móvil → Server

Lo más simple: un **WebSocket** (Python con `websockets` o Node.js). El móvil envía JSON:
```json
{"tipo": "minijuego", "id": 3, "resultado": "exitoso"}
```
El server lo reenvía a Unity (o Unity lo escucha directamente). Unity tiene soporte nativo para WebSocket con `WebSocketSharp` (Asset Store, gratis).

---

## Resumen para tu proyecto UCSP

| Componente | Tecnología |
|:---|:---|
| Detección de mirada | MediaPipe (Python) + webcam |
| Juego principal | Unity (C#) |
| Minijuegos | App móvil (Flutter / React Native / nativo) |
| Comunicación móvil→PC | WebSocket sobre WiFi |
| Comunicación MediaPipe→Unity | Socket TCP local |
| Server | Python (FastAPI / Flask + WebSocket) |

Todo esto es perfectamente viable para un curso de Interacción Humano-Computadora: demuestras **multi-modalidad** (webcam + móvil), **tiempo real**, y **inmersión** (el juego reacciona a tu atención visual).

