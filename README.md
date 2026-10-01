# 🎮 Five Nights at Freddy's - Attention Defense

> Un juego innovador que combina detección de atención visual con mini-juegos en tablet para crear una experiencia única de vigilancia y defensa.

**Curso:** CS2H1 - Interacción Humano Computador  
**Universidad:** Universidad Católica San Pablo (UCSP)  
**Año:** 2026  

---

## 📋 Tabla de Contenidos

- [Concepto del Juego](#concepto-del-juego)
- [Idea Principal](#idea-principal)
- [Cómo Funciona](#cómo-funciona)
- [Arquitectura del Sistema](#arquitectura-del-sistema)
- [Componentes](#componentes)
- [Instalación y Setup](#instalación-y-setup)
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

Si usuario IGNORA LA PANTALLA:
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
OBJETIVO: Sobrevivir 6 noches sin ser atrapado

MECÁNICA:
1. Completa tareas en tablet → Resuelves la amenaza de cada animatronico
2. Vigila pantalla principal → Evitas ataques por descuido
3. Equilibra ambas → Avanzas de noche

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
[GAME START - Noche N]
  ├─ Tareas completadas: 0
  └─ Reloj in-game: 12:00 AM
  ↓
BUCLE PRINCIPAL (cada medio segundo):
  ├─ Gaze Tracking detecta si miras la pantalla
  ├─ ¿Miras tablet y resuelves tareas? → Avanza el progreso
  ├─ ¿Ignoras la pantalla? → Sube el riesgo de ataque
  ├─ Freddy/Vixy avanzan por su ruta, Puppet sale de su caja
  └─ ¿Te alcanzan? → GAME OVER
  ↓
VICTORIA: Sobrevivir hasta las 6 AM, noche tras noche, hasta la noche 6
```

### Interacción Usuario

```
TABLET (en tu mano):
├─ Menú de tareas pendientes
├─ Mini-juego de la tarea elegida
├─ Caja de música de Puppet
└─ Feedback (vibración, sonido)

PANTALLA PC (frente a ti):
├─ Escena 3D de vigilancia (Unity)
├─ Animatronicos moviéndose por el mapa
├─ Jumpscare al ser atrapado
└─ Seguimiento facial (MediaPipe)

CÁMARA USB (arriba de monitor):
└─ Detecta si miras la pantalla, continuamente
```

---

## 🏗️ Arquitectura del Sistema

El juego corre sobre 3 componentes. **Unity es la autoridad del juego**: decide
movimiento, tareas, ataques y progreso de noche. Flutter solo resuelve
tareas y muestra el estado. Un relay en Python conecta a ambos sin contener
ninguna lógica de juego propia — únicamente reenvía mensajes.

```
┌──────────────────────────────────────────────────────────┐
│                    JUGADOR                               │
│    ┌─────────────────────────────────────────────────┐  │
│    │  Frente a Escritorio                            │  │
│    │  ├─ Cámara USB (Unity la usa para gaze)        │  │
│    │  ├─ Monitor (ve el juego 3D en Unity)           │  │
│    │  └─ Tablet en mano (hace tareas en Flutter)     │  │
│    └─────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
                          ↓
         ┌────────────────┼────────────────┐
         ↓                ↓                ↓
    ┌─────────┐      ┌───────────┐    ┌─────────┐
    │ FLUTTER │←────→│UnityRelay │←──→│  UNITY  │
    │ Tablet  │ WiFi │ (Python)  │ WS │  (3D)   │
    └─────────┘      └───────────┘    └─────────┘
         │                                  │
         │ task_completed / dar_cuerda_*    │ task_list / night_status
         │                                  │ estado_puppet / attack
         └──────────────────────────────────┘ / game_over
                   Todo vía UnityRelay, que
                   solo reenvía JSON sin leerlo
```

### Flujo de Datos

```
1. Flutter manda "connect" al conectarse → UnityRelay lo reenvía a Unity
2. Unity (UnityGameSessionController) inicia la noche:
   ├─ Decide movimiento de Freddy/Vixy (NavMesh + probabilidad por nivel de IA)
   ├─ Drena/recarga la caja de música de Puppet
   ├─ Genera tareas nuevas cada hora in-game
   └─ Publica task_list / night_status / estado_puppet por el relay
3. Flutter recibe esos mensajes y los muestra en la tablet
4. El jugador resuelve una tarea → manda task_completed/task_failed
5. Unity decide si eso evita o acelera un ataque
6. Si un animatronico alcanza al jugador → attack + game_over,
   mostrado como jumpscare tanto en Unity como en Flutter
```

---

## 📦 Componentes

### 1. Tablet (`Flutter/`)

**Responsabilidades:**
- Mostrar el menú de tareas y los mini-juegos
- Capturar la entrada del usuario (toques, deslices)
- Comunicarse con Unity a través de UnityRelay
- Proporcionar feedback (vibración, sonido, visual)
- Caja de música de Puppet como pantalla dedicada

**Mini-juegos:** cables, perillas, secuencias, ritmo, wifi, temperatura,
ventiladores, procesar datos, subir datos, trazar curso.

---

### 2. Motor Unity (`Unity/`)

**Responsabilidades — es la autoridad del juego:**
- Seguimiento facial (MediaPipe Unity Plugin) para decidir si el jugador
  mira la pantalla
- Movimiento de Freddy, Vixy y Puppet sobre NavMesh, con nivel de IA y
  probabilidad de jumpscare que sube por noche
- Generación de tareas, progreso de noche, estado de la caja de Puppet
- Persistencia de progreso (`PlayerPrefs`, por noche alcanzada)
- Comunicación con Flutter a través de `UnityWebSocketClient`

**Escena principal:** `Assets/MediaPipeUnity/Samples/Scenes/Face Landmark Detection/Face Landmark Detection.unity`.
Documentación detallada del protocolo y las mecánicas en
`Unity/Assets/scripts/INTEGRACION_UNITY.md`.

---

### 3. UnityRelay (`UnityRelay/`)

**Responsabilidades:**
- Reenviar mensajes WebSocket entre la tablet y Unity por rol (`tablet` /
  `unity`), sin leer ni modificar su contenido
- Recordar el último `connect` de la tablet para repetírselo a Unity si se
  conecta después

No contiene ninguna lógica de juego — es sólo un router de mensajes.

---

## 🚀 Instalación y Setup

### Requisitos Previos

- **Windows 10/11** para ejecutar Unity y UnityRelay en la misma PC (el
  seguimiento facial de Unity necesita Windows; ver notas de cada
  componente para detalles de plataforma).
- **Unity Hub** y **Unity Editor 2022.3.30f1** — la versión exacta está en
  `Unity/ProjectSettings/ProjectVersion.txt`.
- **Python 3.10+** y `pip`, sólo para `UnityRelay/relay.py`.
- **Flutter SDK** con Dart **3.12.2 o superior** (`Flutter/pubspec.yaml`).
- **Android Studio**, Android SDK y Platform Tools, con depuración USB
  habilitada en el teléfono (o un emulador Android).
- PC y teléfono en la misma red local. Unity necesita acceso a la cámara
  de la PC para el seguimiento facial.

### Estructura del Repositorio (real, actual)

```
ProyectoIHC-Five-Nights-at-Freddy-s-Attention-Defense/
├── Flutter/                   # App tablet (Flutter/Dart)
│   └── lib/
│       ├── screens/ widgets/ services/ providers/ models/ utils/ config/
│
├── Unity/                     # Motor 3D + autoridad del juego (C#)
│   ├── Assets/scripts/        # Lógica de juego, movimiento, gaze
│   │   └── INTEGRACION_UNITY.md   # Protocolo y mecánicas en detalle
│   └── Packages/              # manifest.json referencia MediaPipe Unity,
│                               # NativeWebSocket, AI Navigation, Unity UI
│
├── UnityRelay/                 # Relay WebSocket puro (Python)
│   ├── relay.py
│   └── requirements.txt
│
├── docs/                       # Especificación técnica y protocolo JSON
│   ├── FLUTTER_TABLET_ESPECIFICACION.md
│   └── ayuda.md
│
├── README.md (este archivo)
└── .gitignore
```

> Nota: hubo un enfoque anterior con lógica de juego en un backend Python
> (`backend/`, decidía movimiento/ataque) y una carpeta `unity/` sin cerebro
> propio. Quedó reemplazado por la arquitectura de arriba — Unity pasó a
> ser la autoridad del juego y el relay solo reenvía mensajes.

### Paquete MediaPipe — paso manual obligatorio

`Unity/Packages/com.github.homuler.mediapipe/` pesa ~400MB (binarios
nativos para todas las plataformas + modelos de ML) y **no está incluido
en este repositorio** (ver `.gitignore`). Antes de abrir el proyecto en
Unity:

1. Conseguir el paquete `MediaPipeUnityPlugin` (release de
   [`homuler/MediaPipeUnityPlugin`](https://github.com/homuler/MediaPipeUnityPlugin)
   o el `.tgz`/carpeta que ya tenga el equipo).
2. Copiarlo completo dentro de `Unity/Packages/`, de forma que quede en
   `Unity/Packages/com.github.homuler.mediapipe/`.
3. Abrir el proyecto en Unity Hub — el resto de paquetes (NativeWebSocket,
   AI Navigation) se resuelven solos vía `manifest.json`.

### Cómo iniciar una partida conectada

Mantener abiertas tres ventanas: relay, Unity y Flutter.

**Terminal 1 — relay:**

```bash
cd UnityRelay
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt      # Linux/Mac
# .\.venv\Scripts\python.exe -m pip install -r requirements.txt   # Windows
.venv/bin/python relay.py
```

La consola debe mostrar `Relay Unity activo en ws://0.0.0.0:8000`.

**Unity:** abrir `Assets/MediaPipeUnity/Samples/Scenes/Face Landmark Detection/Face Landmark Detection.unity`
y pulsar **Play**. Se conecta al relay por `ws://127.0.0.1:8000`.

**Terminal 2 — Flutter**, con el teléfono conectado:

```bash
cd Flutter
flutter pub get
flutter devices
flutter run -d <id-del-telefono>
```

En la app, abrir **Opciones**: apagar **Modo simulado**, poner la IP local
de la PC (no `127.0.0.1` ni `localhost`, el teléfono apuntaría a sí mismo)
y el puerto `8000`, guardar, e iniciar/continuar la partida.

Detalle completo del protocolo de mensajes y las mecánicas de cada
animatronico en `Unity/Assets/scripts/INTEGRACION_UNITY.md`.

---

## 👥 Equipo

```
ESPECIALISTA 1: Frontend Mobile (Flutter/Dart)
├─ Aplicativo tablet
├─ Mini-juegos interactivos
├─ Comunicación WebSocket
└─ Feedback (vibración, sonido)

ESPECIALISTA 2: Motor 3D + lógica del juego (Unity/C#)
├─ Escena de vigilancia 3D
├─ Movimiento y ataque de animatronicos
├─ Seguimiento facial (gaze tracking)
└─ Relay de conexión con la tablet
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
- [MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin)
- [Flutter Documentation](https://flutter.dev/docs)
- [Unity Documentation](https://docs.unity3d.com/)
- [NativeWebSocket](https://github.com/endel/NativeWebSocket)

---

## 📝 Especificaciones Técnicas

- `Unity/Assets/scripts/INTEGRACION_UNITY.md` — protocolo de mensajes y
  mecánicas de cada animatronico, fuente de verdad del lado Unity/relay.
- `docs/FLUTTER_TABLET_ESPECIFICACION.md` — especificación técnica del
  lado tablet.

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

**Creado con ❤️ para CS2H1 - UCSP**
