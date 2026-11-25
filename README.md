# 🎮 Five Nights of Terror - Proyecto IHC

> Juego de terror para dos jugadores que combina detección de atención visual, minitareas en una tablet y un compañero que vigila el edificio desde otra computadora.

**Curso:** CS2H1 - Interacción Humano Computador  
**Universidad:** Universidad Católica San Pablo (UCSP)  
**Semestre:** 2026-02  
**Integrantes:** Marcelo Torres y Anthony Briceño

---

## 📋 Tabla de Contenidos

- [Concepto del Juego](#-concepto-del-juego)
- [Cómo se Juega](#-cómo-se-juega)
- [Arquitectura del Sistema](#️-arquitectura-del-sistema)
- [Componentes](#-componentes)
- [Instalación y Puesta en Marcha](#-instalación-y-puesta-en-marcha)
- [Pruebas](#-pruebas)
- [Documentación](#-documentación)
- [Equipo](#-equipo)

---

## 🎯 Concepto del Juego

**Five Nights of Terror** reimagina FNAF (Five Nights at Freddy's) con un elemento propio: **el juego sabe hacia dónde miras**. Una cámara USB sigue tu rostro y el peligro depende de cómo repartes tu atención entre la pantalla de la PC y la tablet que tienes en la mano.

```
JUGADOR 1 (en la oficina)              JUGADOR 2 (sala de control)
├─ Mira la escena 3D en la PC          ├─ Ve el plano de todo el edificio
├─ Usa la tablet: cámara, linterna,    ├─ Retrasa animatrónicos y pone trampas
│  compuertas, tareas, caja de Puppet  ├─ Resuelve cálculos que recargan la batería
└─ La cámara USB detecta dónde mira    └─ Rescata a Puppet y avisa en voz alta
```

**Objetivo:** sobrevivir de 12 AM a 6 AM, noche tras noche, hasta la noche 6.

### Diferenciador vs FNAF original

| Aspecto | FNAF original | Five Nights of Terror |
|---------|---------------|-----------------------|
| Entrada | Teclado y mouse | Tablet táctil + mirada (cámara) |
| Actividades | Mirar cámaras | Minitareas + vigilancia + defensa |
| Atención | No se mide | Seguimiento facial en tiempo real |
| Jugadores | Uno | Uno o dos (jugador 2 opcional desde otra PC o celular) |

---

## 🎮 Cómo se Juega

### Las tres amenazas

| Animatrónico | Por dónde llega | Cómo se frena |
|--------------|-----------------|---------------|
| **Freddy** | Por el pasillo | Avanza cuando dejas de mirar la pantalla: mientras la miras, se queda quieto. Con la linterna encima 2 s, retrocede. |
| **Vixy** | Ducto izquierdo | Avanza si acumulas tareas pendientes. Con luz roja a la izquierda, cierra la compuerta izquierda: si aguanta 4 s cerrada, retrocede. |
| **Puppet** | Ducto derecho | Sale si su caja de música se vacía: dale cuerda antes. Si sale, cierra la compuerta derecha hasta que retroceda. |

La lámpara sobre cada ducto avisa: **verde** lejos, **amarillo** dentro del ducto, **rojo** en la boca.

### Jugador 1: la tablet

- **Cámara ◀ ▶:** mantén apretado para girar la vista; al soltar, se queda mirando hacia ahí.
- **Linterna:** mantenla apretada para alumbrar al frente.
- **Compuertas:** toca una para cerrar o abrir el ducto de ese lado.
- **Batería:** la linterna y las compuertas cerradas la gastan (3 %/s). En 0 se abren las compuertas y se apaga la linterna hasta que el jugador 2 la recargue.
- **Tareas:** 10 minijuegos (cables, perillas, secuencia, ritmo, wifi, ventiladores, temperatura, procesar datos, subir datos, trazar curso).
- **Caja de Puppet:** mantén apretado para darle cuerda.
- **Menú:** Tutorial (7 diapositivas, las dos últimas de práctica con la tablet y la cámara), Nuevo juego, Continuar, Opciones (IP y puerto de la PC) y Salir.

### Jugador 2: la sala de control (opcional)

Desde otra computadora o un celular, en una página web:

- Ve en vivo dónde está cada animatrónico y lo que hace el jugador 1 (mirada, cámara, tareas, caja de Puppet, ductos, batería).
- **Retrasar:** un animatrónico va a la mitad de velocidad por 8 s; se puede volver a usar a los 20 s.
- **Trampas:** en un punto del mapa por donde creas que pasará uno; lo detiene 6 s.
- **Terminal:** un cálculo da +1 trampa y +20 % de batería al jugador 1, pero hay un **cupo por noche** (6, 5, 4, 3, 3 y 3 en las noches 1 a 6; solo gastan los bien resueltos). Una ecuación desbloquea el rescate de Puppet. Si fallas, se bloquea 3 s.
- Las acciones están ordenadas por lo que hacen, cada una con su costo a la vista: **Frenar** (Retrasar y trampas), **Terminal** (cálculo y ecuación) y **Salvar a Puppet** (rescate).
- Su menú tiene un **«Cómo jugar»** de 8 diapositivas con práctica simulada y animada: qué te cuesta y qué te da cada acción (Retrasar, trampa, cálculo, ecuación y rescate), con un ejercicio en cada una.
- **Nadie empieza con el otro en el tutorial:** mientras el jugador 2 tenga «Cómo jugar» abierto, la tablet no puede lanzar la noche (dice «El jugador 2 sigue en "Cómo jugar"» y deja reintentar); y si el jugador 1 está en el tutorial, la página del jugador 2 lo avisa con una franja arriba.
- **El susto también le llega al jugador 2:** cuando atrapan al jugador 1, su página muestra el mismo jumpscare (video del animatrónico y grito) a pantalla completa, y después la tarjeta de fin. El grito suena si en esa página ya se hizo algún clic (por ejemplo «Entrar a la sala»).
- **Jumpscares directos a la cara:** los videos empiezan con el animatrónico ya encima (antes se acercaba ~0,6 s por el pasillo mientras el grito ya sonaba). En la PC, la tablet y la página del jugador 2, la cara del animatrónico y el grito salen al instante; el video la tapa al cargar y, si falla, queda la cara. Los recortes y las caras se rehacen con `docs/jumpscares/recortar_jumpscares.py`.
- **Sala del jugador 2 más inmersiva:** atajos de teclado (`1` `2` `3` Retrasar a Freddy, Vixy y Puppet; `T` trampa; `C` cálculo; `E` ecuación; `R` rescatar; `Esc` cancelar), con la tecla en cada botón; el mouse sigue igual. Las orillas de la pantalla se ponen rojas según el peligro (suave si un animatrónico está a menos de 4 m, la caja de Puppet en peligro o la batería ≤ 20 %; fuerte y latiendo si está a menos de 2 m o Puppet salió). Animaciones al confirmar acciones (destello, onda en el mapa, salto del contador, temblor al fallar) y sonidos de consola sintetizados, a volumen bajo, que se apagan en Opciones («Sonidos de la sala»).
- Con el jugador 2 conectado, Freddy y Vixy van más rápido.

### Si se corta la red

- Si la **tablet** pierde la conexión, la noche termina como «conexión perdida».
- Si la pierde el **jugador 2**, el juego sigue y tiene **15 s** para volver.

---

## 🏗️ Arquitectura del Sistema

**Unity es la autoridad del juego:** decide movimiento, tareas, ataques, batería y progreso. La tablet y la página del jugador 2 solo muestran el estado y mandan lo que hace cada jugador. Un relay en Python conecta a todos y no contiene lógica de juego.

```
 ┌──────────────┐   WebSocket    ┌─────────────────┐   WebSocket   ┌──────────────┐
 │ Tablet       │ ─────────────→ │ UnityRelay      │ ←───────────→ │ Unity (PC)   │
 │ (Flutter)    │ ←───────────── │ relay.py :8000  │   localhost   │ juego 3D +   │
 │ jugador 1    │  Wi-Fi o USB   │ (Python)        │               │ cámara USB   │
 └──────────────┘                └─────────────────┘               └──────────────┘
                                          ↑
                                          │ WebSocket
                                 ┌─────────────────┐
                                 │ Página J2       │  servida en :8081
                                 │ (HTML/CSS/JS)   │  por servir_pagina.py
                                 │ otra PC/celular │
                                 └─────────────────┘
```

- Mensajes en **JSON** sobre **WebSocket**.
- El relay asigna un **rol** a cada conexión (`tablet`, `operador`, `unity`) y filtra qué mensajes puede mandar cada uno. Por ejemplo, el jugador 2 no puede completar tareas del jugador 1 y solo Unity, desde la misma PC, puede ser `unity`.
- El relay también lleva la ventana de 15 s para que el jugador 2 vuelva.

---

## 📦 Componentes

### 1. Tablet (`Flutter/`)

App Flutter/Dart (Material 3, Provider), versión **0.18.4+32**, probada en una Lenovo TB350XU. Menú, tutorial, controles de la oficina, 10 minijuegos, caja de Puppet, avisos del compañero y pantallas de fin de noche. Tiene un modo de prueba sin la PC («Jugar sin la PC» en Opciones).

### 2. Motor Unity (`Unity/`)

Unity **2022.3.30f1**. Escena 3D de vigilancia, seguimiento facial con **MediaPipe Unity Plugin**, movimiento de Freddy, Vixy y Puppet, tareas, batería, compuertas, linterna, reglas del jugador 2 (`FrenoDeOperador`, `RecursosDeOperador`, `TareasDeOperador`), tutorial y persistencia de la noche alcanzada (`PlayerPrefs`).

Escena principal: `Assets/MediaPipeUnity/Samples/Scenes/Face Landmark Detection/Face Landmark Detection.unity`.

### 3. UnityRelay (`UnityRelay/`)

- `relay.py`: relay WebSocket en el puerto 8000 (Python 3.12, `websockets`).
- `servir_pagina.py`: sirve la página del jugador 2 en el puerto 8081 sin caché, para que siempre se vea la última versión. Bajo `/sustos/` sirve los videos, las caras y el grito del jumpscare, tomados de las carpetas de Unity.
- `operador/`: la página del jugador 2:
  - `operador.html`, `operador.css` y `operador.js`: la sala de control.
  - `logica.js`: la lógica pura de la sala.
  - `practica.js` y `como_jugar.js`: el «Cómo jugar» con práctica.
  - `susto.js`: el jumpscare del jugador 2.

### 4. Scripts de la raíz

| Script | Para qué |
|--------|----------|
| `iniciar.sh` | Levanta relay, página del jugador 2 y juego; al cerrar el juego apaga todo. |
| `exportar_linux.sh` | Exporta el juego a `Unity/Builds/Linux/FiveNightsOfTerror.x86_64` (con el editor cerrado). |
| `crear_acceso_directo.sh` | Crea el ícono del Escritorio para abrir el juego con doble clic. |
| `conectar_tablet_por_usb.sh` | Conecta la tablet por cable USB (`adb reverse`), sin Wi-Fi. |

---

## 🚀 Instalación y Puesta en Marcha

### Requisitos

- **PC con Linux** (el juego se exporta y se prueba en Linux), cámara USB.
- **Unity Hub** y **Unity Editor 2022.3.30f1** (`Unity/ProjectSettings/ProjectVersion.txt`); solo para exportar o desarrollar.
- **Python 3.12** para el relay.
- **Flutter SDK** con Dart **^3.12.2** (`Flutter/pubspec.yaml`) y **Android Platform Tools** (`adb`) para instalar la app en la tablet.
- PC, tablet y computadora del jugador 2 en la **misma red** (o la tablet por USB).

### Paquete MediaPipe — paso manual obligatorio

`Unity/Packages/com.github.homuler.mediapipe/` pesa ~400 MB y **no está en el repositorio**. Antes de abrir el proyecto en Unity, copia el paquete `MediaPipeUnityPlugin` ([homuler/MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin)) completo en esa carpeta. El resto de paquetes se resuelven solos con `manifest.json`.

### Primera vez

```bash
# entorno del relay
cd UnityRelay
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
cd ..

# exportar el juego (con el editor de Unity cerrado)
./exportar_linux.sh

# opcional: ícono en el Escritorio
./crear_acceso_directo.sh

# app de la tablet (con la tablet conectada por USB y la depuración activada)
cd Flutter
flutter build apk --release
adb install -r build/app/outputs/flutter-apk/app-release.apk
```

### Jugar

1. Corre `./iniciar.sh` (o doble clic en **Five Nights of Terror**). Se abren el relay, la página del jugador 2 y el juego. Una notificación muestra la dirección para el jugador 2.
2. **Tablet:** en **Opciones**, pon la IP que muestra la PC y el puerto `8000`, guarda y elige **Nuevo juego** o **Continuar**.
3. **Jugador 2 (opcional):** en otra computadora o celular de la misma red, abre `http://IP-DE-LA-PC:8081/operador.html` y elige **«Entrar a la sala de control»**.

Si algo falla, el relay deja su salida en `Unity/Builds/Linux/relay.log`.

### Si la red no deja conectar

- **Red de la universidad:** aísla a cada dispositivo. Conecta la tablet por cable con `./conectar_tablet_por_usb.sh` y pon `127.0.0.1:8000` en Opciones. El jugador 2 necesita una red común, por ejemplo el punto de acceso de un celular.
- **Firewall de la PC:** si `ufw` está activo, abre los puertos con `sudo ufw allow 8000/tcp && sudo ufw allow 8081/tcp`.
- **El jugador 2 no carga la página:** revisa que esa computadora esté en la misma red, con buena señal y sin VPN. Si la página abre pero dice «Desconectado», revisa la IP y el puerto en sus Opciones. Si se reinicia el juego, recarga la página con F5.

### Desarrollo desde el editor

1. Relay: `cd UnityRelay && .venv/bin/python relay.py` (debe decir `Relay Unity activo en ws://0.0.0.0:8000`).
2. Unity: abre la escena principal y pulsa **Play**; se conecta a `ws://127.0.0.1:8000`.
3. Tablet: `cd Flutter && flutter pub get && flutter run -d <id-de-la-tablet>`; en Opciones, apaga «Jugar sin la PC» y pon la IP de la PC (no `127.0.0.1`, salvo por USB).

---

## 🧪 Pruebas

| Qué | Comando |
|-----|---------|
| Reglas de Unity (Mono, sin abrir el editor; con el puerto 8000 libre) | `./pruebas/probar_reglas_de_operador.sh`, `./pruebas/probar_freno_de_operador.sh` y `./pruebas/probar_ruta_de_jumpscare.sh` |
| Tablet | `cd Flutter && flutter test` |
| Relay | `UnityRelay/.venv/bin/python pruebas/probar_relay.py` |
| Página del jugador 2 (Firefox sin ventana) | `UnityRelay/.venv/bin/python pruebas/probar_pagina_operador.py` |
| Servidor de la página (y `/sustos/`) | `UnityRelay/.venv/bin/python pruebas/probar_servir_pagina.py` |
| Lógica de la página y de la práctica | `node UnityRelay/operador/probar_logica.js` y `node UnityRelay/operador/probar_practica.js` |
| Juego real exportado (abre el juego y la cámara) | `pruebas/probar_*_con_juego.py` |
| Scripts de arranque (con el puerto 8000 libre) | `./pruebas/probar_iniciar.sh` y `./pruebas/probar_direccion_de_red.sh` |

---

## 📝 Documentación

- `Unity/Assets/scripts/INTEGRACION_UNITY.md`: protocolo de mensajes y mecánicas de cada animatrónico (fuente de verdad del lado Unity y relay).
- `docs/PROTOCOLO_JUGADOR2.md`: mensajes del jugador 2.
- `docs/FLUTTER_TABLET_ESPECIFICACION.md`: especificación del lado tablet.
- `docs/DISENO_EJECUTABLE_UNICO.md`: diseño del arranque con un solo clic.
- `docs/tutorial/`: fuentes HTML de las diapositivas del tutorial del jugador 1 (`generar.sh` las pasa a PNG).
- `UnityRelay/README.md`: detalles del relay.

---

## 👥 Equipo

**Integrantes:** Marcelo Torres y Anthony Briceño.

```
Tablet (Flutter/Dart)          Motor 3D y lógica (Unity/C#)       Red y jugador 2
├─ App de la tablet            ├─ Escena de vigilancia 3D         ├─ Relay WebSocket (Python)
├─ 10 minijuegos               ├─ Animatrónicos y ataques         ├─ Página de la sala de control
├─ Controles de la oficina     ├─ Seguimiento facial (MediaPipe)  └─ Roles, filtros y reconexión
└─ Tutorial y menú             └─ Reglas del jugador 2
```

---

## 📊 Mapeo a Syllabus IHC

```
✅ UNIDAD 1: Fundamentos           → interfaz centrada en el usuario
✅ UNIDAD 2: Factores humanos      → detección de atención visual
✅ UNIDAD 3: Diseño y evaluación   → diseño iterativo, tutoriales con práctica
✅ UNIDAD 4: Diseño de interacción → varias modalidades de entrada (tacto, mirada)
✅ UNIDAD 5: Nuevas tecnologías    → seguimiento facial, red inalámbrica
✅ UNIDAD 6: Colaboración          → dos jugadores en tiempo real, en dispositivos distintos
```

---

## 🔗 Enlaces Útiles

- [MediaPipe](https://ai.google.dev/edge/mediapipe/solutions/guide)
- [MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin)
- [Flutter](https://docs.flutter.dev/)
- [Unity](https://docs.unity3d.com/)
- [websockets (Python)](https://websockets.readthedocs.io/)

---

## 📄 Licencia

Proyecto de uso académico para la Universidad Católica San Pablo.

---

**Creado con ❤️ para CS2H1 - UCSP**
