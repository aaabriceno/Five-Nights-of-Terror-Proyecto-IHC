# 📱 ESPECIFICACIÓN TÉCNICA - APLICATIVO TABLET (FLUTTER)
## Proyecto: "Five Nights at Freddy's - Attention Defense"

**Curso:** CS2H1 - Interacción Humano Computador  
**Universidad:** UCSP  
**Especialista:** [Tu nombre]  
**Rol:** Frontend Mobile + Networking  

---

## 📚 Tabla de Contenidos

1. [Resumen Ejecutivo](#resumen-ejecutivo)
2. [Tecnologías Principales](#tecnologías-principales)
3. [Especificaciones Funcionales](#especificaciones-funcionales)
4. [Arquitectura de Networking](#arquitectura-de-networking)
5. [Mini-juegos Detallados](#mini-juegos-detallados)
6. [Protocolo de Comunicación](#protocolo-de-comunicación)
7. [Setup y Instalación](#setup-e-instalación)
8. [Cronograma 3 Semanas](#cronograma-3-semanas)
9. [Checklist de Desarrollo](#checklist-de-desarrollo)

---

## 🎯 Resumen Ejecutivo

### Tu Responsabilidad

```
┌─────────────────────────────────────────────────────┐
│  ESPECIALISTA FLUTTER: Frontend + Networking        │
├─────────────────────────────────────────────────────┤
│                                                     │
│  QUÉ HACES:                                         │
│  ✅ Desarrollo de interfaz tablet (Flutter)        │
│  ✅ Mini-juegos/tareas simples                     │
│  ✅ Comunicación con servidor Python               │
│  ✅ Captura de estado de tareas                    │
│  ✅ Feedback visual y háptico                      │
│                                                     │
│  QUÉ NO HACES:                                      │
│  ❌ Gaze tracking (Python Backend)                 │
│  ❌ Motor de juego 3D (Unity)                      │
│  ❌ Animatrónico graphics (Unity)                  │
│  ❌ Lógica de ataque (Python)                      │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### Interfaz de Conexión

```
[TABLET]             WiFi/Bluetooth          [SERVIDOR PYTHON]
  ↓                        ↓                         ↓
Flutter App  ←──────→  JSON Messages  ←──────→  Backend
  ├─ UI                ├─ Task Status              ├─ Gaze Tracking
  ├─ Mini-games        ├─ Attack Event            ├─ Game Logic
  ├─ Feedback          ├─ Pause/Resume            └─ Orchestration
  └─ Status            └─ Game Over

ENVÍA:
  {"type": "task", "task_id": 1, "completed": true, "time_taken": 25}
  
RECIBE:
  {"type": "attack", "damage": 30, "urgency": "critical"}
```

---

## 🛠️ Tecnologías Principales

### Stack de Desarrollo

```
LENGUAJE:
✅ Dart 3.0+
   └─ Lenguaje de programación para Flutter
   └─ Sintaxis similar a Java/Kotlin/C#

FRAMEWORK:
✅ Flutter 3.13+
   ├─ Framework multiplataforma (Android + iOS)
   ├─ Hot Reload (recarga en tiempo real)
   ├─ Renderizado a 60 FPS (o 120 FPS)
   └─ Acceso a APIs nativas

COMUNICACIÓN:
✅ WebSocket (recomendado)
   ├─ Conexión persistente (mejor para tiempo real)
   ├─ Latencia baja (<50ms)
   ├─ Ideal para eventos en vivo
   └─ Librería: web_socket_channel

   O (Alternativa)

✅ HTTP + Polling
   ├─ Más simple de implementar
   ├─ Compatible con más servidores
   ├─ Latencia más alta (~100-200ms)
   └─ Librería: http

NOTIFICACIONES:
✅ flutter_local_notifications
   ├─ Vibración (feedback háptico)
   ├─ Sonido (feedback auditivo)
   ├─ Notificaciones push
   └─ Toast/Snackbar

ESTADO:
✅ Provider
   ├─ Gestión de estado reactivo
   ├─ Sincronización entre widgets
   ├─ Fácil debugging
   └─ Escalable

UI/UX:
✅ Material Design 3 (Flutter built-in)
✅ CustomPainter (para tareas dibujadas)
✅ Animations (transiciones suaves)
✅ flutter_svg (iconos escalables)
```

### Requisitos Previos

```
INSTALAR (en orden):
1. Flutter SDK 3.13+
   └─ Descargar: https://flutter.dev/docs/get-started/install
   
2. Dart (incluido en Flutter)
   └─ Verificar: dart --version

3. IDE:
   ✅ Visual Studio Code (recomendado)
      └─ Extensiones: Flutter, Dart
   o
   ✅ Android Studio (más pesado)
      └─ Incluye emulador

4. Android SDK (para emular tablet)
   └─ Descargado automáticamente por Flutter

5. Emulador de Tablet (AVD)
   └─ Recomendado: 10" tablet size
   └─ API Level 30+
```

---

## 📋 Especificaciones Funcionales

### Pantalla Principal

```
┌──────────────────────────────────────┐
│        FIVE NIGHTS TABLET APP        │
├──────────────────────────────────────┤
│                                      │
│  BARRA DE ESTADO (arriba):           │
│  ├─ Conexión: [●] Conectado          │
│  ├─ Nivel: 3/5                       │
│  ├─ Energía del servidor: 85%        │
│  └─ Tiempo jugado: 15:32             │
│                                      │
│  ÁREA PRINCIPAL:                     │
│  ┌──────────────────────────────────┐│
│  │                                  ││
│  │      [TAREA ACTUAL]              ││
│  │      Conectar cables...          ││
│  │      Tiempo: 00:23               ││
│  │                                  ││
│  │    [Zona interactiva - Custom]   ││
│  │                                  ││
│  └──────────────────────────────────┘│
│                                      │
│  ESTADO (abajo):                     │
│  ├─ Vida: ████░░░░░░ 65%            │
│  ├─ Progreso tarea: ███░░░░░░░ 30%  │
│  └─ [ENVIAR] [REINTENTAR]           │
│                                      │
└──────────────────────────────────────┘
```

### Estados Posibles

```
1. IDLE (Esperando)
   ├─ Muestra pantalla de inicio
   ├─ Intenta conectar con servidor
   └─ Botón "Jugar"

2. CONECTANDO
   ├─ Spinner de carga
   ├─ Texto: "Conectando con servidor..."
   └─ Timeout: 30 segundos

3. ESPERANDO TAREA
   ├─ Muestra mensaje: "Esperando siguiente tarea"
   ├─ Spinner
   └─ Barra de estado del servidor

4. EJECUTANDO TAREA
   ├─ Muestra mini-juego específico
   ├─ Cronómetro cuenta progresivamente
   ├─ Feedback visual en tiempo real
   └─ Botón para enviar respuesta

5. ATACADO
   ├─ Pantalla parpadea en ROJO
   ├─ Vibración fuerte (3x)
   ├─ Sonido de alarma
   ├─ Muestra daño recibido
   └─ Vuelve a tarea (o game over)

6. GAME OVER
   ├─ Pantalla con "JUEGO TERMINADO"
   ├─ Estadísticas (tiempo, tareas completadas)
   ├─ Botón "Reintentar"
   └─ Botón "Volver al menú"
```

---

## 🌐 Arquitectura de Networking

### Conexión a Servidor

```
PASO 1: Iniciar conexión (Cuando app inicia)
┌─────────────────────────────────────┐
│  WebSocket connect                  │
│  ├─ URL: ws://[IP_SERVIDOR]:8000    │
│  ├─ IP puede ser: 192.168.1.X       │
│  ├─ Puerto: 8000 (default Flask)    │
│  └─ Timeout: 10 segundos            │
│                                     │
│  Si conexión exitosa:               │
│  └─ Enviar: {"type": "connect",     │
│              "device": "tablet",    │
│              "player_id": "123"}    │
└─────────────────────────────────────┘

PASO 2: Esperar comando del servidor
┌─────────────────────────────────────┐
│  Escuchar mensajes entrantes        │
│                                     │
│  Tipos posibles:                    │
│  ├─ {"type": "new_task", ...}       │
│  ├─ {"type": "attack", ...}         │
│  ├─ {"type": "pause", ...}          │
│  └─ {"type": "game_over", ...}      │
└─────────────────────────────────────┘

PASO 3: Enviar respuesta
┌─────────────────────────────────────┐
│  Cuando usuario completa tarea:     │
│                                     │
│  Enviar: {                          │
│    "type": "task_completed",        │
│    "task_id": 1,                    │
│    "time_taken": 23.5,              │
│    "success": true,                 │
│    "attempts": 1                    │
│  }                                  │
└─────────────────────────────────────┘

PASO 4: Reconexión automática
┌─────────────────────────────────────┐
│  Si conexión se pierde:             │
│  ├─ Mostrar banner: "Reconectando"  │
│  ├─ Intentar cada 3 segundos        │
│  ├─ Máximo 5 intentos               │
│  └─ Después: mostrar error          │
│                                     │
│  Si reconecta:                      │
│  └─ Sincronizar estado actual       │
└─────────────────────────────────────┘
```

### Flujo de Datos en Tiempo Real

```
SERVIDOR                                TABLET
   │                                      │
   ├─ Calcula gaze tracking               │
   ├─ Verifica atención del usuario       │
   │                                      │
   └─ {"type": "new_task",        ─────→ │ Recibe tarea
      "task_id": 1,                       │ Muestra UI
      "task_type": "cables"}              │ Espera usuario
                                          │
                                   Usuario interactúa
                                          │
                          ←─────────────── {"type": "task_completed",
                                          "success": true}
   Calcula resultado                      │
   Verifica si ataque                     │
   │                                      │
   └─ {"type": "attack",          ─────→ │ Recibe ataque
      "damage": 30}                       │ Pantalla roja
                                          │ Vibra + Sonido
                                          │
                          ←─────────────── {"type": "ready",
                                          "acknowledged": true}
   Continúa juego...
```

---

## 🎮 Mini-juegos Detallados

### Mini-juego 1: CONECTAR CABLES

**Descripción:** Arrastra cables a sus respectivos conectores

```
┌────────────────────────────────┐
│    CONECTAR CABLES             │
│    Tiempo: 30 segundos         │
├────────────────────────────────┤
│                                │
│  CABLES (lado izquierdo):      │
│                                │
│  [Red cable]    ────────────   │
│                         ↓      │
│  [Blue cable]   ────────────   │
│                         ↓      │
│  [Green cable]  ────────────   │
│                         ↓      │
│  [Yellow cable] ────────────   │
│                                │
│  CONECTORES (lado derecho):    │
│  ○ Rojo        ○ Azul         │
│  ○ Verde       ○ Amarillo     │
│                                │
│  ESTADO:                       │
│  Conectados: 2/4               │
│  Tiempo restante: 00:28        │
│                                │
│  [COMPLETAR] [REINTENTAR]     │
│                                │
└────────────────────────────────┘
```

**Implementación Flutter:**

```dart
// Usar CustomPainter para dibujar
class CableGamePainter extends CustomPainter {
  final List<Cable> cables;
  final List<Connector> connectors;
  
  void paint(Canvas canvas, Size size) {
    // Dibujar cables
    // Dibujar conectores
    // Dibujar líneas de conexión
  }
}

// Detectar toques y arrastres
GestureDetector(
  onPanUpdate: (details) {
    // Mover cable mientras se arrastra
  },
  onPanEnd: (details) {
    // Validar si está conectado correctamente
  },
)
```

**Variables a Capturar:**
```json
{
  "type": "task_completed",
  "task_id": 1,
  "task_type": "cables",
  "connections": [
    {"cable": "red", "connector": "red", "correct": true},
    {"cable": "blue", "connector": "blue", "correct": true},
    {"cable": "green", "connector": "green", "correct": false},
    {"cable": "yellow", "connector": "yellow", "correct": true}
  ],
  "success": false,
  "time_taken": 28.5,
  "attempts": 1
}
```

---

### Mini-juego 2: GIRAR PERILLAS

**Descripción:** Girar perillas a la posición correcta

```
┌────────────────────────────────┐
│    GIRAR PERILLAS              │
│    Tiempo: 20 segundos         │
├────────────────────────────────┤
│                                │
│  PERILLA 1: Girar a 90°        │
│  ┌─────────────┐               │
│  │     ◉       │  Posición     │
│  │   ↙  ↗      │  actual: 45°  │
│  └─────────────┘               │
│  Objetivo: [────○────]  90°    │
│                                │
│  PERILLA 2: Girar a 270°       │
│  ┌─────────────┐               │
│  │     ◉       │  Posición     │
│  │   ↙  ↗      │  actual: 0°   │
│  └─────────────┘               │
│  Objetivo: [────────○]  270°   │
│                                │
│  ESTADO:                       │
│  Completadas: 0/2              │
│  Tolerancia: ±15°              │
│                                │
│  [COMPLETAR] [REINTENTAR]     │
│                                │
└────────────────────────────────┘
```

**Implementación Flutter:**

```dart
// Detectar rotación de toque
GestureDetector(
  onPanUpdate: (details) {
    // Calcular ángulo basado en movimiento
    double angle = calculateRotation(details.globalPosition);
    
    // Actualizar rotación de perilla
    setState(() {
      knobAngle = angle;
    });
  },
  onPanEnd: (details) {
    // Verificar si está dentro de tolerancia
    if (isWithinTolerance(knobAngle, targetAngle)) {
      completeKnob();
    }
  },
)

Transform.rotate(
  angle: knobAngle * pi / 180,
  child: Image.asset('assets/knob.png'),
)
```

**Variables a Capturar:**
```json
{
  "type": "task_completed",
  "task_id": 2,
  "task_type": "dials",
  "dials": [
    {"dial_id": 1, "target": 90, "achieved": 92, "correct": true},
    {"dial_id": 2, "target": 270, "achieved": 265, "correct": true}
  ],
  "success": true,
  "time_taken": 18.3,
  "attempts": 2
}
```

---

### Mini-juego 3: RESOLVER PUZZLES

**Descripción:** Presionar objetos en el orden correcto

```
┌────────────────────────────────┐
│    RESOLVER SECUENCIA          │
│    Tiempo: 25 segundos         │
├────────────────────────────────┤
│                                │
│  ORDEN CORRECTO:               │
│  [3] → [1] → [4] → [2] → [5]  │
│                                │
│  ELEMENTOS:                    │
│  ┌─────┐ ┌─────┐ ┌─────┐      │
│  │  1  │ │  2  │ │  3  │      │
│  │     │ │     │ │     │      │
│  └─────┘ └─────┘ └─────┘      │
│                                │
│  ┌─────┐ ┌─────┐              │
│  │  4  │ │  5  │              │
│  │     │ │     │              │
│  └─────┘ └─────┘              │
│                                │
│  PROGRESO:                     │
│  ✓ [3]  ✓ [1]  ░ [4] ░ ░     │
│  Secuencia: 3/5                │
│                                │
│  [COMPLETAR] [REINTENTAR]     │
│                                │
└────────────────────────────────┘
```

**Implementación Flutter:**

```dart
// Gestionar secuencia de toques
List<int> correctOrder = [3, 1, 4, 2, 5];
List<int> userSequence = [];

GestureDetector(
  onTap: () {
    setState(() {
      userSequence.add(tappedElement);
    });
    
    if (tappedElement != correctOrder[userSequence.length - 1]) {
      // Error - reiniciar o mostrar feedback negativo
      userSequence.clear();
      showErrorFeedback();
    }
    
    if (userSequence.length == correctOrder.length) {
      completeTask();
    }
  },
)
```

**Variables a Capturar:**
```json
{
  "type": "task_completed",
  "task_id": 3,
  "task_type": "sequence",
  "correct_order": [3, 1, 4, 2, 5],
  "user_sequence": [3, 1, 4, 2, 5],
  "success": true,
  "time_taken": 22.1,
  "attempts": 1,
  "errors": 0
}
```

---

### Mini-juego 4: PRESIONAR AL RITMO

**Descripción:** Presionar botones al ritmo de música/pulso

```
┌────────────────────────────────┐
│    RITMO CRÍTICO               │
│    Tiempo: 15 segundos         │
├────────────────────────────────┤
│                                │
│  PULSO OBJETIVO:               │
│  ♪  ♪  ♪  ♪  ♪  ♪             │
│                                │
│  BOTONES (presiona al ritmo):  │
│  ┌─────┐ ┌─────┐ ┌─────┐     │
│  │ROJO │ │VERDE│ │AZUL │     │
│  └─────┘ └─────┘ └─────┘     │
│                                │
│  ESTADO:                       │
│  Aciertos: 8/12                │
│  Precisión: 95%                │
│  BPM: 120                      │
│                                │
│  [COMPLETAR] [REINTENTAR]     │
│                                │
└────────────────────────────────┘
```

**Implementación Flutter:**

```dart
// Generar pulsos
Timer.periodic(Duration(milliseconds: beatDuration), (timer) {
  playBeep();
  expectedPress = generateNextButton();
  
  // Ventana de tiempo para presionar
  startTimeWindow();
});

// Detectar presiones
GestureDetector(
  onTap: () {
    int timeDifference = calculateTimeDifference();
    
    if (timeDifference < 200) { // 200ms tolerancia
      correctPress();
    } else {
      missedPress();
    }
  },
)
```

**Variables a Capturar:**
```json
{
  "type": "task_completed",
  "task_id": 4,
  "task_type": "rhythm",
  "rhythm_data": {
    "bpm": 120,
    "total_beats": 12,
    "correct_presses": 11,
    "missed_presses": 1,
    "early_presses": 0,
    "late_presses": 1,
    "accuracy": 91.7
  },
  "success": true,
  "time_taken": 14.2,
  "attempts": 1
}
```

---

## 📡 Protocolo de Comunicación

### Formato JSON (Definición)

**MENSAJES DE TABLET → SERVIDOR:**

```json
// Conexión inicial
{
  "type": "connect",
  "device": "tablet",
  "player_id": "unique_id_123",
  "app_version": "1.0.0",
  "timestamp": 1695123456789
}

// Tarea completada
{
  "type": "task_completed",
  "task_id": 1,
  "task_type": "cables",
  "success": true,
  "time_taken": 25.3,
  "attempts": 1,
  "timestamp": 1695123456900,
  "task_data": {
    "connections": [
      {"cable": "red", "connector": "red", "correct": true},
      {"cable": "blue", "connector": "blue", "correct": true}
    ]
  }
}

// Tarea fallida
{
  "type": "task_failed",
  "task_id": 1,
  "time_taken": 30.0,
  "reason": "timeout",
  "timestamp": 1695123456950
}

// Acknowledgment de ataque
{
  "type": "attack_acknowledged",
  "attack_id": "atk_456",
  "damage_taken": 30,
  "remaining_health": 70,
  "timestamp": 1695123457000
}

// Desconexión
{
  "type": "disconnect",
  "player_id": "unique_id_123",
  "reason": "user_quit",
  "timestamp": 1695123457100
}
```

**MENSAJES DE SERVIDOR → TABLET:**

```json
// Nueva tarea
{
  "type": "new_task",
  "task_id": 2,
  "task_type": "dials",
  "duration": 20,
  "description": "Girar perillas a posición correcta",
  "difficulty": 2,
  "timestamp": 1695123457200,
  "task_params": {
    "num_dials": 2,
    "targets": [90, 270],
    "tolerance": 15
  }
}

// Ataque entrante
{
  "type": "attack",
  "attack_id": "atk_789",
  "damage": 30,
  "urgency": "critical",
  "animatronic": "Freddy",
  "message": "¡Liberé mi atención!",
  "timestamp": 1695123457300
}

// Pausa del juego
{
  "type": "pause",
  "reason": "server_maintenance",
  "duration": 30,
  "message": "Mantenimiento del servidor. Reanudará en 30 segundos",
  "timestamp": 1695123457400
}

// Game Over
{
  "type": "game_over",
  "result": "loss",
  "final_stats": {
    "duration": 900,
    "tasks_completed": 15,
    "tasks_failed": 3,
    "total_damage": 150,
    "score": 4500
  },
  "timestamp": 1695123457500
}

// Reconexión exitosa
{
  "type": "reconnect_success",
  "session_data": {
    "current_health": 70,
    "current_task": 3,
    "time_elapsed": 145,
    "score": 3200
  },
  "timestamp": 1695123457600
}
```

### Estructura de Datos Local (Dart Model)

```dart
// Modelo de Tarea
class Task {
  final int taskId;
  final String taskType; // "cables", "dials", "sequence", "rhythm"
  final int duration; // segundos
  final String description;
  final int difficulty;
  final DateTime createdAt;
  final Map<String, dynamic> params;
  
  Task({
    required this.taskId,
    required this.taskType,
    required this.duration,
    required this.description,
    required this.difficulty,
    required this.createdAt,
    required this.params,
  });
  
  factory Task.fromJson(Map<String, dynamic> json) {
    return Task(
      taskId: json['task_id'],
      taskType: json['task_type'],
      duration: json['duration'],
      description: json['description'],
      difficulty: json['difficulty'],
      createdAt: DateTime.parse(json['timestamp']),
      params: json['task_params'] ?? {},
    );
  }
}

// Modelo de Ataque
class Attack {
  final String attackId;
  final int damage;
  final String urgency; // "low", "medium", "critical"
  final String animatronic;
  final String message;
  final DateTime timestamp;
  
  Attack({
    required this.attackId,
    required this.damage,
    required this.urgency,
    required this.animatronic,
    required this.message,
    required this.timestamp,
  });
  
  factory Attack.fromJson(Map<String, dynamic> json) {
    return Attack(
      attackId: json['attack_id'],
      damage: json['damage'],
      urgency: json['urgency'],
      animatronic: json['animatronic'],
      message: json['message'],
      timestamp: DateTime.parse(json['timestamp']),
    );
  }
}

// Modelo de Sesión
class GameSession {
  final String playerId;
  int health = 100;
  int score = 0;
  int tasksCompleted = 0;
  int tasksFailed = 0;
  DateTime? startTime;
  DateTime? endTime;
  bool isConnected = false;
  Task? currentTask;
  
  GameSession({required this.playerId});
}
```

---

## 🚀 Setup e Instalación

### Instalación de Flutter

```bash
# 1. Descargar Flutter SDK
# Ir a https://flutter.dev/docs/get-started/install
# Seleccionar tu SO (Windows/Mac/Linux)

# 2. Verificar instalación
flutter --version
dart --version

# 3. Verificar dependencias
flutter doctor

# Debería mostrar:
# [✓] Flutter
# [✓] Android toolchain
# [✓] Android Studio
# [✓] Dart
```

### Crear Proyecto Flutter

```bash
# Crear nuevo proyecto
flutter create --org com.example fn_five_nights_tablet

# Navegar a carpeta
cd fn_five_nights_tablet

# Verificar estructura
tree lib/  # o dir lib (Windows)

# Debería mostrar:
# lib/
# ├── main.dart
# ├── screens/
# ├── widgets/
# ├── services/
# ├── models/
# └── utils/
```

### Agregar Dependencias (pubspec.yaml)

```yaml
dependencies:
  flutter:
    sdk: flutter
  
  # Networking
  web_socket_channel: ^2.4.0
  http: ^1.1.0
  
  # Estado
  provider: ^6.0.0
  
  # Notificaciones
  flutter_local_notifications: ^16.2.0
  vibration: ^1.7.7
  
  # UI
  flutter_svg: ^2.0.5
  lottie: ^2.4.0  # Animaciones
  
  # Logging
  logger: ^2.0.2
  
  # Storage local
  shared_preferences: ^2.2.0
  sqflite: ^2.3.0  # Si necesitas DB local

dev_dependencies:
  flutter_test:
    sdk: flutter
  flutter_linter: ^3.0.0

flutter:
  uses-material-design: true
  assets:
    - assets/images/
    - assets/sounds/
    - assets/animations/
```

### Estructura de Carpetas

```
fn_five_nights_tablet/
├── lib/
│   ├── main.dart                      # Punto de entrada
│   ├── models/
│   │   ├── task.dart                  # Modelo de tarea
│   │   ├── attack.dart                # Modelo de ataque
│   │   └── game_session.dart          # Sesión del juego
│   ├── screens/
│   │   ├── splash_screen.dart         # Pantalla de inicio
│   │   ├── game_screen.dart           # Pantalla principal del juego
│   │   ├── task_screen.dart           # Pantalla de tarea actual
│   │   └── game_over_screen.dart      # Pantalla de fin del juego
│   ├── widgets/
│   │   ├── cable_game.dart            # Mini-juego cables
│   │   ├── dial_game.dart             # Mini-juego perillas
│   │   ├── sequence_game.dart         # Mini-juego secuencias
│   │   ├── rhythm_game.dart           # Mini-juego ritmo
│   │   ├── health_bar.dart            # Widget barra de vida
│   │   └── status_bar.dart            # Widget estado conexión
│   ├── services/
│   │   ├── websocket_service.dart     # Comunicación WebSocket
│   │   ├── game_logic_service.dart    # Lógica del juego
│   │   └── notification_service.dart  # Notificaciones/vibración
│   ├── providers/
│   │   ├── game_provider.dart         # Estado del juego (Provider)
│   │   └── connection_provider.dart   # Estado conexión
│   ├── utils/
│   │   ├── constants.dart             # Constantes globales
│   │   ├── colors.dart                # Paleta de colores
│   │   └── logger.dart                # Logging utility
│   └── config/
│       └── server_config.dart         # URLs y puertos del servidor
├── assets/
│   ├── images/                        # Imágenes y íconos
│   ├── sounds/                        # Efectos de sonido
│   └── animations/                    # Animaciones Lottie
├── test/                              # Tests
├── pubspec.yaml                       # Dependencias
├── pubspec.lock                       # Lock file
└── README.md                          # Documentación
```

### Archivo main.dart (Básico)

```dart
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'providers/game_provider.dart';
import 'providers/connection_provider.dart';
import 'screens/splash_screen.dart';

void main() {
  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => ConnectionProvider()),
        ChangeNotifierProvider(create: (_) => GameProvider()),
      ],
      child: const MyApp(),
    ),
  );
}

class MyApp extends StatelessWidget {
  const MyApp({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Five Nights at Freddy\'s - Tablet',
      theme: ThemeData(
        primarySwatch: Colors.red,
        darkness: Brightness.dark,
      ),
      home: const SplashScreen(),
      debugShowCheckedModeBanner: false,
    );
  }
}
```

---

## 📅 Cronograma 3 Semanas

### SEMANA 1: Setup + Mini-juegos Básicos

```
LUNES:
├─ [ ] Instalar Flutter en tu máquina
├─ [ ] Crear proyecto Flutter base
├─ [ ] Setupear estructura de carpetas
├─ [ ] Agregar dependencias (pubspec.yaml)
└─ [ ] Commit inicial en GitHub
  TIEMPO: 2-3 horas
  
MARTES:
├─ [ ] Crear modelos (Task, Attack, Session)
├─ [ ] Crear pantalla splash/inicio
├─ [ ] Implementar ConnectionProvider (Provider)
└─ [ ] Testing conexión a servidor
  TIEMPO: 3-4 horas
  
MIÉRCOLES:
├─ [ ] Implementar WebSocketService
├─ [ ] Crear GameProvider (estado del juego)
├─ [ ] Conectar tablet ↔ servidor (primer JSON)
├─ [ ] Testing: enviar mensaje, recibir respuesta
└─ [ ] Debug de conexión
  TIEMPO: 4-5 horas
  
JUEVES:
├─ [ ] Crear interfaz de Mini-juego 1 (Cables)
├─ [ ] Implementar lógica de cables
├─ [ ] CustomPainter para dibujar
├─ [ ] Testing del mini-juego
└─ [ ] Captura y envío de datos JSON
  TIEMPO: 4-5 horas
  
VIERNES:
├─ [ ] Crear interfaz de Mini-juego 2 (Perillas)
├─ [ ] Implementar lógica de rotación
├─ [ ] Testing del mini-juego
├─ [ ] Code review y refactoring
└─ [ ] Commit semanal
  TIEMPO: 4-5 horas

TOTAL SEMANA 1: 18-22 horas
```

### SEMANA 2: Más Mini-juegos + Feedback

```
LUNES:
├─ [ ] Crear interfaz de Mini-juego 3 (Secuencias)
├─ [ ] Implementar lógica de secuencias
├─ [ ] Testing del mini-juego
└─ [ ] Optimizar performance
  TIEMPO: 3-4 horas

MARTES:
├─ [ ] Crear interfaz de Mini-juego 4 (Ritmo)
├─ [ ] Implementar generador de pulsos
├─ [ ] Testing de timing/precisión
└─ [ ] Debug de latencia
  TIEMPO: 4-5 horas

MIÉRCOLES:
├─ [ ] Implementar feedback háptico (vibración)
├─ [ ] Agregar sonidos (alarma, acierto, error)
├─ [ ] Implementar NotificationService
└─ [ ] Testing de feedback en emulador
  TIEMPO: 3-4 horas

JUEVES:
├─ [ ] Crear UI de estado (barras de vida, timer)
├─ [ ] Implementar StatusBar widget
├─ [ ] Crear GameScreen principal
├─ [ ] Integrar todos los mini-juegos
└─ [ ] Testing de transiciones entre tareas
  TIEMPO: 4-5 horas

VIERNES:
├─ [ ] Crear pantalla GameOver
├─ [ ] Implementar lógica de reinicio
├─ [ ] Code review completo
├─ [ ] Bug fixes y optimización
└─ [ ] Commit semanal + documentación
  TIEMPO: 4-5 horas

TOTAL SEMANA 2: 18-23 horas
```

### SEMANA 3: Pulido + Integración + Testing

```
LUNES:
├─ [ ] Implementar reconexión automática
├─ [ ] Mejorar manejo de errores
├─ [ ] Crear pantalla de desconexión
├─ [ ] Testing con servidor real (Python)
└─ [ ] Debug de latencia end-to-end
  TIEMPO: 4-5 horas

MARTES:
├─ [ ] Agregar efectos visuales (animaciones)
├─ [ ] Mejorar UI de mini-juegos
├─ [ ] Agregar transiciones suaves
├─ [ ] Testing en tablet real (si disponible)
└─ [ ] Optimizar para diferentes tamaños pantalla
  TIEMPO: 4-5 horas

MIÉRCOLES:
├─ [ ] Crear archivo de configuración del servidor
├─ [ ] Agregar logging completo
├─ [ ] Testing de concurrencia (múltiples mensajes)
├─ [ ] Stress testing (muchas tareas rápidas)
└─ [ ] Debug de problemas de sincronización
  TIEMPO: 4-5 horas

JUEVES:
├─ [ ] Pulir UI completa
├─ [ ] Corregir bugs menores
├─ [ ] Testing en múltiples dispositivos
├─ [ ] Documentar código
└─ [ ] Crear manual de usuario
  TIEMPO: 3-4 horas

VIERNES:
├─ [ ] Presentación y demostración
├─ [ ] Preparar builds (APK/IPA si aplica)
├─ [ ] Documentación final
├─ [ ] Commit final
└─ [ ] Revisión con compañero (Backend Python)
  TIEMPO: 3-4 horas

TOTAL SEMANA 3: 18-23 horas
```

**TOTAL PROYECTO (3 semanas): 54-68 horas**

---

## ✅ Checklist de Desarrollo

### Semana 1

- [ ] Instalación de Flutter completada
- [ ] Proyecto creado y estruturado
- [ ] Dependencias instaladas correctamente
- [ ] Conexión WebSocket funcionando
- [ ] Primer mensaje JSON enviado/recibido
- [ ] Mini-juego 1 (cables) implementado
- [ ] Mini-juego 2 (perillas) implementado
- [ ] Tests básicos pasando
- [ ] Commit en GitHub

### Semana 2

- [ ] Mini-juego 3 (secuencias) implementado
- [ ] Mini-juego 4 (ritmo) implementado
- [ ] Vibración funcionando
- [ ] Sonidos implementados
- [ ] StatusBar con estado conexión
- [ ] GameScreen principal funcional
- [ ] Todas las transiciones suaves
- [ ] GameOver screen implementada
- [ ] Tests de mini-juegos pasando
- [ ] Commit en GitHub

### Semana 3

- [ ] Reconexión automática funcionando
- [ ] Manejo de errores robusto
- [ ] Efectos visuales agregados
- [ ] Optimizado para diferentes pantallas
- [ ] Testing con servidor Python completado
- [ ] Logging implementado
- [ ] Documentación completa
- [ ] Código refactorizado y limpio
- [ ] APK generado (si Android)
- [ ] Demostración funcionando
- [ ] Commit final

---

## 📞 Interfaz con Backend Python

### Punto de Contacto: WebSocket Server

```
PUERTO: 8000 (default)
PROTOCOLO: WebSocket (ws://)

DIRECCIÓN:
- Local: ws://localhost:8000
- En red: ws://[IP_SERVIDOR]:8000

EJEMPLO PARA TESTING:
ws://192.168.1.100:8000

TIEMPOS DE RESPUESTA ESPERADOS:
- Conexión: < 2 segundos
- Mensaje task: < 100ms
- Mensaje attack: < 50ms
```

### Cómo Conectar con Backend Python

```dart
// Código en Flutter para conectar
import 'package:web_socket_channel/web_socket_channel.dart';

class WebSocketService {
  late WebSocketChannel channel;
  
  void connect(String serverAddress) {
    try {
      channel = WebSocketChannel.connect(
        Uri.parse('ws://$serverAddress:8000'),
      );
      
      // Escuchar mensajes
      channel.stream.listen(
        (dynamic message) {
          handleServerMessage(message);
        },
        onError: (error) {
          handleConnectionError(error);
        },
        onDone: () {
          handleConnectionClosed();
        },
      );
    } catch (e) {
      print('Error conectando: $e');
    }
  }
  
  void sendMessage(Map<String, dynamic> data) {
    channel.sink.add(jsonEncode(data));
  }
}
```

---

## 🔧 Configuración del Servidor (Para Testing)

**Mientras tu compañero desarrolla el backend Python, puedes usar esto para testing:**

```python
# test_server.py - Simple Flask WebSocket server

from flask import Flask, render_template
from flask_socketio import SocketIO, emit, join_room

app = Flask(__name__)
app.config['SECRET_KEY'] = 'secret!'
socketio = SocketIO(app, cors_allowed_origins="*")

@socketio.on('connect')
def handle_connect():
    print('Cliente conectado')
    emit('response', {'data': 'Conectado al servidor'})

@socketio.on('message')
def handle_message(data):
    print(f'Mensaje recibido: {data}')
    # Echo back
    emit('message', {'data': f'Server recibió: {data}'}, broadcast=True)

@socketio.on('task_completed')
def handle_task_completed(data):
    print(f'Tarea completada: {data}')
    emit('response', {'status': 'ok', 'data': data})

if __name__ == '__main__':
    socketio.run(app, host='0.0.0.0', port=8000, debug=True)
```

---

## 📝 Notas Importantes

### Para Comunicarte con Backend

```
INFORMACIÓN NECESARIA DEL SERVIDOR:
1. IP del servidor (Ej: 192.168.1.100)
2. Puerto WebSocket (default: 8000)
3. Formato exacto de mensajes JSON
4. Tiempos de timeout esperados
5. Qué hacer si hay desconexión

INFORMACIÓN QUE DEBES DAR AL BACKEND:
1. Estructura de cada tarea (cables, perillas, etc)
2. Datos que esperas recibir (attack, new_task, etc)
3. Frecuencia esperada de mensajes (cada 16ms, etc)
4. Latencia máxima aceptable (< 50ms)
5. Qué ocurre si no se recibe respuesta
```

### Testing sin Backend

```dart
// Mock server para testing (sin backend real)
class MockGameProvider {
  void sendMockTask() {
    Future.delayed(Duration(seconds: 2), () {
      handleNewTask({
        "type": "new_task",
        "task_id": 1,
        "task_type": "cables",
        "duration": 30,
        "description": "Conectar cables",
      });
    });
  }
  
  void sendMockAttack() {
    Future.delayed(Duration(seconds: 5), () {
      handleAttack({
        "type": "attack",
        "damage": 30,
        "urgency": "critical",
        "animatronic": "Freddy",
      });
    });
  }
}
```

---

## 🎯 Puntos Críticos

```
1. CONEXIÓN
   ├─ Establecer conexión WebSocket al iniciar app
   ├─ Mantener conexión persistente
   ├─ Reconectar automáticamente si se cae
   └─ Mostrar estado de conexión al usuario

2. TIMING
   ├─ Sincronizar con servidor
   ├─ Capturar timestamps exactos
   ├─ Enviar datos antes de timeout
   └─ Manejar retrasos de red

3. FEEDBACK
   ├─ Responder inmediatamente a input del usuario
   ├─ Vibrar cuando ataca animatrónico
   ├─ Sonido de alarma cuando se ataca
   ├─ Cambio visual (pantalla roja)
   └─ Todo ocurre < 200ms

4. DATOS
   ├─ Capturar datos exactos de cada tarea
   ├─ Enviar como JSON bien formado
   ├─ Incluir timestamps
   ├─ Validar respuesta del servidor
   └─ Reintentar si no hay respuesta
```

---

## 📞 Comunicación entre Equipos

**TÚ (Flutter):**
- Comienzas con estructura básica
- Implementas UI de mini-juegos
- Estableces comunicación WebSocket
- Envías estado de tareas

**BACKEND (Python):**
- Recibe datos de las tareas
- Procesa gaze tracking
- Decide si atacar o no
- Envía comandos de ataque

**UNITY (3D):**
- Recibe comandos de ataque
- Anima animatrónico
- Renderiza la escena
- Muestra vida del jugador

**COORDINACIÓN:**
```
Reunión diaria 15 min (Discord):
├─ Lunes: Protocolo JSON definitivo
├─ Miércoles: Testing de conexión end-to-end
├─ Viernes: Demo de integración
└─ Cualquier día: Debugging emergencias
```

---

**Documento Versión:** 1.0  
**Fecha:** [Hoy]  
**Estado:** Listo para desarrollo

