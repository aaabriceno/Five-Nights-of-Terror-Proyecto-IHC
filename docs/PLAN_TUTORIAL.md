# Plan — Tutorial dentro del juego

Pedido del profesor: que alguien que nunca vio el juego pueda entender cómo
se juega. Dado que el jugador no tiene teclado ni mouse, el tutorial se
controla desde la tablet y se proyecta en la pantalla de la PC — la misma
división que usa el juego, así que el tutorial ya enseña la mecánica por el
solo hecho de usarse.

## Cómo funciona

```
Tablet                         Relay            Unity (PC)
──────                         ─────            ──────────
Menú → "Tutorial"
  └─ tutorial_abrir ──────────────────────────► muestra diapositiva 1
Botón "Siguiente"
  └─ tutorial_slide {n:2} ────────────────────► muestra diapositiva 2
Botón "Salir"
  └─ tutorial_cerrar ─────────────────────────► vuelve a "Esperando..."
```

La tablet no muestra el contenido: solo los controles y el número de
diapositiva. Todo lo que se lee está en la pantalla grande.

Controles en la tablet: **Anterior** y **Siguiente** abajo, y una **X arriba
a la derecha** para salir. La X pide confirmación ("¿Salir del tutorial?",
Confirmar / Cancelar) para que un toque accidental no corte la explicación.
Al confirmar, la PC vuelve a la pantalla de espera.

## Protocolo — mensajes nuevos

Tres mensajes, todos de tablet a Unity:

| Mensaje | Campos | Efecto |
|---|---|---|
| `tutorial_abrir` | — | Unity entra en modo tutorial, muestra la primera |
| `tutorial_slide` | `indice` (int, base 0) | Muestra esa diapositiva |
| `tutorial_cerrar` | — | Sale del tutorial, vuelve a la pantalla de espera |

Unity responde con `tutorial_estado` (`indice`, `total`) para que la tablet
sepa cuántas hay y pueda desactivar los botones en los extremos. Así el
número de diapositivas vive en un solo lugar (Unity) y la tablet no necesita
saberlo de antemano.

## Contenido — las diapositivas

Cada diapositiva es una imagen en `Unity/Assets/Resources/tutorial/`,
nombradas `slide_01`, `slide_02`, etc. Unity las carga en orden y cuenta
cuántas hay; agregar una es poner el archivo, sin tocar código.

Guion propuesto (9 diapositivas):

1. **Qué es el juego** — sos el guardia nocturno, tenés que sobrevivir de
   12 AM a 6 AM.
2. **Las dos pantallas** — la PC muestra el pasillo, la tablet tus tareas.
   Tenés que repartir la atención entre las dos.
3. **La cámara te está viendo** — explicar que el juego detecta si mirás la
   pantalla. Es el mecanismo central y el menos obvio.
4. **Freddy** — avanza cuando no lo mirás. Mirarlo lo congela.
5. **Vixy** — avanza cuando acumulás tareas sin resolver. Resolverlas la
   frena.
6. **Puppet y la caja de música** — se vacía sola, hay que darle cuerda
   desde la tablet.
7. **Las tareas** — captura del menú de tareas, explicando que cada una es
   un minijuego y que dejarlas pendientes tiene costo.
8. **Cómo se pierde** — si un animatrónico llega hasta vos.
9. **Cómo se gana** — llegar a las 6 AM. Cada noche es más difícil.

Las capturas salen del propio juego (Game view de Unity para el pasillo,
capturas de la tablet para los minijuegos), con el texto encima armado en
Figma, Canva o similar. Formato 16:9, bordes oscuros para que el recorte en
otras proporciones no se note — mismo criterio que la pantalla de espera.

## Estado: el código está hecho, falta el contenido

Todo lo técnico quedó implementado y compilando. Lo único pendiente es
**armar las 9 diapositivas** y ponerlas en `Unity/Assets/Resources/tutorial/`
como `slide_01.png`, `slide_02.png`, etc. El sistema las detecta solas: no
hay que tocar código ni declarar cuántas son.

Para probarlo antes de tener el contenido definitivo, alcanza con poner
cualquier imagen con esos nombres.

Archivos nuevos:

- `Unity/Assets/scripts/PantallaDeTutorial.cs`
- `Flutter/lib/screens/tutorial_screen.dart`

Falta un único paso manual en Unity: agregar el componente
**`PantallaDeTutorial`** al GameObject `SelectorDeCamara`, donde ya están
`PantallaDeEspera` y `PantallaDeResultado`.

## Detalle de lo implementado

### Unity

1. **`PantallaDeTutorial.cs`** — Canvas por código, mismo patrón que
   `PantallaDeEspera` y `PantallaDeResultado`. Carga las imágenes de
   `Resources/tutorial/`, muestra la que corresponda, y se oculta cuando no
   está en modo tutorial.
2. **`UnityGameSessionController`** — tres `case` nuevos en `OnMessage`, y
   estado `indiceDeTutorial` / `enTutorial` expuesto para la pantalla.
   Enviar `tutorial_estado` al cambiar.
3. **Orden de capas** — el tutorial va encima de la pantalla de espera
   (`short.MaxValue - 4` o similar, por debajo de resultado y jumpscare).

### Flutter

4. **Imagen del menú regenerada** con cinco opciones en este orden —
   **Tutorial, Nuevo Juego, Continuar, Opciones, Salir** — y las cinco
   posiciones verticales recalibradas en `menu_principal_screen.dart`.
   Tutorial va primero porque es lo que necesita quien abre el juego sin
   conocerlo.
5. **`TutorialScreen`** — pantalla con los controles: Anterior, Siguiente,
   Salir, y un indicador "3 / 9". Manda los mensajes y escucha
   `tutorial_estado`.
6. **Conexión sin partida — RESUELTO, ver abajo.**

## Punto 6: conexión sin iniciar partida (investigado)

El mensaje `connect` hace **dos cosas a la vez**, y el tutorial necesita
solo la primera:

1. En `relay.py` (línea 52-54) le asigna el rol "tablet" al socket. Sin ese
   rol, el relay **no reenvía nada** a Unity (línea 62), así que el tutorial
   no puede simplemente omitirlo.
2. En Unity, `StartGame()` arranca la noche.

El relay además **guarda** ese mensaje en `last_connect_message` y se lo
repite a Unity si conecta más tarde (línea 47-48), así que un mensaje nuevo
tipo `connect_tutorial` obligaría a tocar el relay y coordinar con el
compañero.

**Solución elegida:** la tablet sigue mandando `connect`, pero con
`modo: "tutorial"`. Unity, en `StartGame()`, si el modo es `tutorial` no
arranca la noche: queda conectado esperando los mensajes del tutorial. El
relay no se toca y el protocolo no suma mensajes nuevos — solo un valor más
en un campo que ya existe.

### Contenido

7. Capturas de pantalla del juego y de la tablet.
8. Armado de las 9 diapositivas con texto.

## Cómo armar las diapositivas

1. **Capturar el material**: el pasillo y los animatrónicos desde el Game
   view de Unity; los minijuegos y el menú de tareas desde la tablet.
2. **Componer cada diapositiva** en Figma, Canva o lo que resulte cómodo:
   la captura de fondo y el texto explicativo encima. Formato 16:9
   (1920×1080), con los bordes oscuros para que el recorte en monitores de
   otra proporción no se note.
3. **Guardarlas** en `Unity/Assets/Resources/tutorial/` como `slide_01.png`,
   `slide_02.png`, etc. El número al principio define el orden.

Se puede probar el flujo completo con imágenes de relleno antes de tener el
contenido final.

## Pendiente del lado del relay (coordinar con el equipo)

`relay.py` guarda **cualquier** mensaje `connect` en `last_connect_message`
(línea 55) y se lo repite a Unity si éste conecta más tarde (línea 47-48).
Eso incluye el `connect` con `modo: "tutorial"`.

Consecuencia: si alguien abre el tutorial y después Unity se reinicia, el
relay le reenvía ese mensaje y Unity entra en modo tutorial sin que nadie lo
pidiera. Es poco frecuente —requiere reiniciar Unity sin reiniciar el
relay— pero el arreglo es una línea: no guardar el mensaje cuando el modo es
`tutorial`.

```python
if message_type == "connect":
    role = "tablet"
    tablets.add(socket)
    if message.get("modo") != "tutorial":
        last_connect_message = raw
    await send_to(unity_clients, raw)
```

No se aplicó desde acá porque `relay.py` es territorio compartido.

## Riesgo resuelto

La calibración de la imagen del menú ya está hecha: las cinco coordenadas se
midieron sobre la imagen generada y quedaron con espaciado parejo (~0.108
entre opciones), verificado funcionando. Si se regenera la imagen del menú
hay que volver a medirlas en `menu_principal_screen.dart`.
