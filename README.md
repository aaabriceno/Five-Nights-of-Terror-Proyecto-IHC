# 🎮 Five Nights at Freddy's - Attention Defense

> Un juego innovador que combina detección de atención visual con mini-juegos en tablet para crear una experiencia única de vigilancia y defensa.

**Curso:** CS2H1 - Interacción Humano Computador  
**Universidad:** Universidad Católica San Pablo (UCSP)  
**Año:** 2024  

---

## 📋 Tabla de Contenidos

- [Concepto del Juego](#concepto-del-juego)
- [Idea Principal](#idea-principal)
- [Cómo Funciona](#cómo-funciona)
- [Tecnologías](#tecnologías)
- [Arquitectura del Sistema](#arquitectura-del-sistema)
- [Componentes](#componentes)
- [Instalación y Setup](#instalación-y-setup)
- [Cronograma](#cronograma)
- [Equipo](#equipo)
- [Licencia](#licencia)

---

## 🎯 Concepto del Juego

**Five Nights at Freddy's - Attention Defense** es un juego que reimagina el concepto original de FNAF (Five Nights at Freddy's) con un elemento revolucionario: **detección de atención visual en tiempo real**.

### Idea Central

```
La seguridad del jugador depende de:
1. Mantener la atención en la pantalla (mirando el PC)
2. Completar tareas en la tablet (mirar la tablet)
3. Balancear ambas para no ser atacado

┌─────────────────────────────────────┐
│  JUGADOR                            │
│  ├─ Frente a PC (ve el juego)      │
│  ├─ Holding Tablet (hace tareas)    │
│  └─ Cámara USB (detecta dónde mira)│
└─────────────────────────────────────┘
```

### Mecánica Principal

El animatrónico es **inteligente**:

```
Si usuario IGNORA TABLET:
  └─ "Se da cuenta de que no trabajas"
  └─ ⚡ ATAQUE INMINENTE

Si usuario MIRA MUCHO LA TABLET:
  └─ "Se da cuenta de que no vigilas"
  └─ ⚡ ATAQUE CRÍTICO

Balance Correcto:
  └─ ✅ Estás seguro
  └─ ✅ Avanzas en el juego
```

---

## 💡 Idea Principal

### Diferenciador vs FNAF Original

| Aspecto | FNAF Original | Nuestro Juego |
|---------|---------------|---------------|
| Input | Teclado/Mouse | Tablet + Gaze |
| Actividades | Monitor cámaras | Mini-juegos + Vigilancia |
| Detección | Solo resultado | Detección de atención |
| Interacción | Pasiva | Activa + Adaptativa |
| Tecnología | Convencional | Reconocimiento facial + Sensores |

### Concepto de Gamificación

```
OBJETIVO: Sobrevivir la noche sin ser atrapado

MECÁNICA:
1. Completa tareas en tablet → Ganas tiempo
2. Vigila pantalla principal → Evitas ataques
3. Equilibra ambas → Avanzas de nivel

RETO:
No puedes ignorar ninguna actividad
El animatrónico monitorea TU ATENCIÓN
```

---

## 🎮 Cómo Funciona

### Flujo del Juego

```
INICIO
  ↓
[Cámara analiza tu rostro]
  ↓
[Te genera avatar personalizado]
  ↓
[GAME START - Noche 1]
  ├─ Vida: 100%
  ├─ Tareas completadas: 0
  └─ Tiempo: 00:00
  ↓
BUCLE PRINCIPAL (cada segundo):
  ├─ Gaze Tracking detecta dónde miras
  ├─ ¿Miras tablet? → Completa tarea
  ├─ ¿Miras pantalla? → Vigilas
  ├─ ¿Ignoras ambas? → ⚡ ATAQUE
  └─ ¿Vida = 0%? → GAME OVER
  ↓
VICTORIA: Sobrevivir 8 horas (8 minutos en juego)
```

### Interacción Usuario

```
TABLET (en tu mano):
├─ Mini-juego actual
├─ Cuenta regresiva
├─ Indicador de atención
└─ Feedback (vibración, sonido)

PANTALLA PC (frente a ti):
├─ Escena de vigilancia (oficina)
├─ Animatrónico moviéndose
├─ Barra de vida
├─ Estadísticas en tiempo real
└─ Eventos de ataque

CÁMARA USB (arriba de monitor):
└─ Detecta a dónde miras continuamente
```

---

## 🛠️ Tecnologías

### Stack Tecnológico

```
┌────────────────────────────────────────┐
│        TECNOLOGÍAS DEL PROYECTO        │
├────────────────────────────────────────┤
│                                        │
│  FRONTEND TABLET                       │
│  ├─ Framework: Flutter                │
│  ├─ Lenguaje: Dart 3.0+               │
│  ├─ Plataformas: Android + iOS        │
│  └─ Comunicación: WebSocket           │
│                                        │
│  BACKEND / LÓGICA DE JUEGO             │
│  ├─ Lenguaje: Python 3.10+            │
│  ├─ Gaze Tracking: MediaPipe          │
│  ├─ Visión: OpenCV                    │
│  ├─ Facial Detection: dlib + MediaPipe│
│  ├─ Framework: FastAPI/Flask          │
│  └─ Comunicación: WebSocket           │
│                                        │
│  MOTOR GRÁFICO / 3D                    │
│  ├─ Engine: Unity 2021 LTS            │
│  ├─ Lenguaje: C#                      │
│  ├─ Gráficos: 3D Renderizado          │
│  ├─ Audio: Audio 3D Espacializado     │
│  └─ Animaciones: Character Animation  │
│                                        │
│  COMUNICACIÓN                          │
│  ├─ Protocolo: WebSocket              │
│  ├─ Formato: JSON                     │
│  ├─ Conexión: WiFi/Bluetooth          │
│  └─ Latencia Objetivo: <50ms          │
│                                        │
└────────────────────────────────────────┘
```

### Componentes Tecnológicos Clave

**1. Detección de Atención Visual (Gaze Tracking)**
```
MediaPipe Face Mesh
├─ Detecta 468 puntos del rostro
├─ Identifica dirección de ojos
├─ Determina: ¿Mira tablet o pantalla?
└─ Frecuencia: 60 FPS (16ms)

Precisión: 80%+
Latencia: <100ms
```

**2. Mini-juegos en Tablet**
```
Flutter CustomPainter
├─ Conectar cables (drag & drop)
├─ Girar perillas (rotación táctil)
├─ Resolver secuencias (tap orden)
├─ Ritmo crítico (timing precision)
└─ Comunicación: JSON WebSocket
```

**3. Motor de Juego 3D**
```
Unity 3D
├─ Escena de vigilancia 3D
├─ Animatrónico con comportamiento IA
├─ Sistema de partículas para ataques
├─ Audio 3D espacializado
└─ Sincronización con backend
```

**4. Servidor Backend**
```
Python FastAPI
├─ Gaze tracking + análisis
├─ Lógica de ataque
├─ Sincronización de eventos
├─ Orquestación del juego
└─ WebSocket server
```

---

## 🏗️ Arquitectura del Sistema

### Diagrama General

```
┌──────────────────────────────────────────────────────────┐
│                    JUGADOR                               │
│    ┌─────────────────────────────────────────────────┐  │
│    │  Frente a Escritorio                            │  │
│    │  ├─ Cámara USB (detecta gaze)                  │  │
│    │  ├─ Monitor (ve juego 3D)                      │  │
│    │  └─ Tablet en mano (hace tareas)               │  │
│    └─────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
                          ↓
         ┌────────────────┼────────────────┐
         ↓                ↓                ↓
    ┌─────────┐      ┌─────────┐      ┌────────┐
    │ TABLET  │      │ SERVIDOR│      │ PANTALLA
    │ Flutter │←────→│ Python  │←────→│ Unity
    │         │ WiFi │         │ JSON │ 3D
    └─────────┘      └─────────┘      └────────┘
         │                │                │
         │ Envía tarea    │ Recibe gaze    │
         │ completada     │ + ataca        │ Muestra
         │                │                │ escena 3D
         └────────────────┼────────────────┘
                          │
                    Sincronización
                    en tiempo real
                    (<50ms latencia)
```

### Flujo de Datos

```
CICLO CADA 16ms (60 FPS):

1. Cámara USB
   └─ Captura rostro del jugador

2. Python Backend
   ├─ Procesa imagen (MediaPipe)
   ├─ Detecta: ¿Hacia dónde mira?
   ├─ Genera evento: "mirando_tablet" o "mirando_pantalla"
   └─ Envía estado a otros componentes

3. Flutter Tablet
   ├─ Recibe eventos del backend
   ├─ Renderiza mini-juego actual
   ├─ Usuario interactúa (toca, desliza)
   └─ Envía: "tarea_completada" o "tarea_fallida"

4. Unity PC
   ├─ Recibe: gaze status + eventos tarea
   ├─ Lógica: ¿Atacar ahora?
   ├─ Renderiza ataque (si aplica)
   ├─ Reproduce audio 3D
   └─ Actualiza UI (vida, puntuación)

5. Todo sincronizado
   └─ Jugador ve resultado en pantalla
   └─ Tablet vibra (feedback)
   └─ Sonido de ataque
```

---

## 📦 Componentes

### 1. Tablet (Flutter)

**Responsabilidades:**
- Mostrar mini-juegos interactivos
- Capturar entrada del usuario (toques, deslices)
- Comunicarse con servidor Python
- Proporcionar feedback (vibración, sonido, visual)

**Características:**
- 4 tipos de mini-juegos diferentes
- Interfaz responsive para tablets 7"-10"
- Reconexión automática
- Indicador de conexión en tiempo real

**Conectividad:**
- WebSocket para comunicación persistente
- JSON para formato de mensajes
- WiFi o Bluetooth

---

### 2. Backend Python

**Responsabilidades:**
- Ejecutar gaze tracking
- Analizar atención del jugador
- Decidir cuándo atacar
- Orquestar eventos del juego
- Gestionar comunicación con otros componentes

**Características:**
- Detección facial en tiempo real
- Estimación de dirección de ojos
- Lógica de decisión de ataques
- WebSocket server
- Logging y debugging

**Tecnologías:**
- MediaPipe para face detection
- OpenCV para procesamiento de imagen
- FastAPI para server
- Python 3.10+

---

### 3. Motor Unity 3D

**Responsabilidades:**
- Renderizar escena 3D de vigilancia
- Animar animatrónico
- Reproducir audio espacializado
- Visualizar efectos de ataque
- Gestionar interfaz de usuario (UI)

**Características:**
- Escena de oficina/sala de vigilancia
- Animatrónico 3D con comportamientos
- Efectos de partículas para ataques
- Audio 3D envolvente
- Barra de vida y estadísticas

**Tecnologías:**
- Unity 2021 LTS
- C# para scripting
- Sistema de animación
- Audio 3D Spatial

---

## 🚀 Instalación y Setup

### Requisitos Previos

**Hardware:**
- PC con GPU (NVIDIA recomendado para gaze tracking)
- Tablet Android o iPad
- Cámara USB 1080p mínimo
- Conexión WiFi 5GHz

**Software:**
- Python 3.10+
- Flutter 3.13+
- Unity 2021 LTS
- Git

### Estructura del Repositorio (real, actual)

```
ProyectoIHC-Five-Nights-at-Freddy-s-Attention-Defense/
├── backend/                  # Gaze tracking (Python)
│   ├── gaze/                 # módulo: detección, calibración, estimación, landmarks
│   ├── gaze_demo.py           # script de demo/prueba de la webcam
│   ├── gaze_debug_landmarks.py
│   ├── requirements.txt
│   └── venv/                 # entorno virtual (NO se sube a git, se crea local)
│
├── flutter/                  # App tablet (Flutter/Dart)
│   ├── lib/
│   │   ├── screens/ widgets/ services/ providers/ models/ utils/ config/
│   └── pubspec.yaml
│
├── docs/                      # Especificación técnica y protocolo JSON
│   ├── FLUTTER_TABLET_ESPECIFICACION.md
│   └── ayuda.md
│
├── README.md (este archivo)
└── .gitignore
```

> Nota: el motor Unity todavía no vive en este repositorio (lo maneja el compañero de Unity por separado). Se integrará más adelante.

### Backend Python — crear y usar el entorno virtual (venv)

El entorno virtual (`venv/`) **no se sube a git** (está en `.gitignore`) porque depende de cada máquina. Cada persona debe crearlo localmente la primera vez:

```bash
cd backend

# 1. Crear el entorno virtual (una sola vez)
python3 -m venv venv

# 2. Activar el entorno virtual
source venv/bin/activate        # Linux/Mac
# venv\Scripts\activate         # Windows (cmd/PowerShell)

# 3. Instalar dependencias
pip install -r requirements.txt

# 4. Probar que la cámara + gaze tracking funcionan
python gaze_demo.py

# 5. Al terminar, desactivar el entorno
deactivate
```

Cada vez que vuelvas a trabajar en el backend (nueva sesión de terminal), solo hace falta repetir el paso 2 (`source venv/bin/activate`) — no hace falta recrear el venv salvo que lo borres o cambies de máquina.

Si agregas una librería nueva con `pip install <paquete>`, actualiza `requirements.txt` con:
```bash
pip freeze > requirements.txt
```

### Tablet Flutter — comandos básicos

```bash
cd flutter

# 1. Descargar dependencias del pubspec.yaml
flutter pub get

# 2. Verificar que el entorno Flutter está bien configurado
flutter doctor

# 3. Correr la app (con un emulador/tablet conectado o Chrome)
flutter run
```

### Unity (referencia, cuando se integre)

```
Abrir Unity Hub → Open Project → Seleccionar la carpeta del proyecto Unity
(carpeta aún no incluida en este repo — ver nota arriba)
```

---

## 📅 Cronograma

### Semana 1-2: Setup Base + Flutter
```
├─ Instalar Flutter SDK
├─ Crear proyecto base
├─ Conectar WebSocket
├─ Mini-juego 1 (Cables)
└─ Mini-juego 2 (Perillas)
```

### Semana 3-4: Backend + Más Juegos
```
├─ Gaze tracking con MediaPipe
├─ Servidor WebSocket Python
├─ Mini-juego 3 (Secuencias)
├─ Mini-juego 4 (Ritmo)
└─ Testing integración Flutter ↔ Python
```

### Semana 5-7: Motor 3D
```
├─ Escena de vigilancia en Unity
├─ Animatrónico 3D
├─ Sistema de ataques
├─ Audio 3D
└─ Efectos visuales
```

### Semana 8-10: Integración Total
```
├─ Flutter ↔ Python sincronizado
├─ Python ↔ Unity comunicando
├─ Testing end-to-end
├─ Balanceo de dificultad
└─ Bug fixes
```

### Semana 11-12: Pulido + Presentación
```
├─ Optimización de performance
├─ Documentación final
├─ Demo funcional
└─ Presentación a profesor
```

**Duración Total:** 12 semanas

---

## 👥 Equipo

```
ESPECIALISTA 1: Frontend Mobile (Flutter/Dart)
├─ Aplicativo tablet
├─ Mini-juegos interactivos
├─ Comunicación WebSocket
└─ Feedback (vibración, sonido)

ESPECIALISTA 2: Backend (Python)
├─ Gaze tracking
├─ Lógica de ataque
├─ Servidor WebSocket
└─ Orquestación del juego

ESPECIALISTA 3: Motor 3D (Unity/C#)
├─ Escena de vigilancia 3D
├─ Animatrónico
├─ Audio y efectos visuales
└─ Interfaz de usuario
```

---

## 📊 Mapeo a Syllabus IHC

Este proyecto cubre todas las unidades del curso CS2H1:

```
✅ UNIDAD 1: Fundamentos
   └─ Interfaz centrada en usuario

✅ UNIDAD 2: Factores Humanos
   └─ Detección de atención visual

✅ UNIDAD 3: Diseño y Testing
   └─ Diseño iterativo centrado usuario

✅ UNIDAD 4: Diseño de Interacción
   └─ Múltiples modalidades de entrada

✅ UNIDAD 5: Nuevas Tecnologías ⭐⭐⭐
   └─ Eye tracking, sensores, wireless, gestos

✅ UNIDAD 6: Colaboración
   └─ Comunicación en tiempo real entre dispositivos
```

**Impacto IHC:** ⭐⭐⭐⭐ (Muy Alto)

---

## 🔗 Enlaces Útiles

- [MediaPipe Documentation](https://mediapipe.dev/)
- [Flutter Documentation](https://flutter.dev/docs)
- [Unity Documentation](https://docs.unity3d.com/)
- [OpenCV Python](https://docs.opencv.org/master/)
- [FastAPI](https://fastapi.tiangolo.com/)

---

## 📝 Especificaciones Técnicas

Para documentación técnica más detallada, ver:
- `docs/ARCHITECTURE.md` - Arquitectura del sistema
- `docs/PROTOCOL.md` - Protocolo de comunicación JSON
- `docs/SETUP.md` - Instrucciones de setup detalladas
- `tablet/README.md` - Especificación Flutter
- `backend/README.md` - Especificación Python
- `unity/README.md` - Especificación Unity

---

## 📄 Licencia

Este proyecto es de uso académico para la Universidad Católica San Pablo.

---

## 🤝 Contribuciones

Este es un proyecto académico colaborativo. Para contribuir:

1. Crear rama con tu nombre
2. Hacer cambios
3. Crear Pull Request
4. Coordinarse con el equipo

---

## ⚠️ Estado del Proyecto

```
ESTADO: En Desarrollo
VERSIÓN: 0.1.0 (Alpha)
ÚLTIMA ACTUALIZACIÓN: [Fecha]

TODO:
- [ ] Backend Python 50%
- [ ] Flutter Frontend 30%
- [ ] Motor Unity 20%
- [ ] Integración 0%
- [ ] Testing 0%
- [ ] Documentación 40%
```

---

## 📞 Contacto

Para preguntas o problemas, abrir issue en GitHub o contactar al equipo de desarrollo.

---

**Creado con ❤️ para CS2H1 - UCSP**

