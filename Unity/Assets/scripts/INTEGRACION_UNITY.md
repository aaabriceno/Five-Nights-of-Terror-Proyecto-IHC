# Integración Unity, Flutter y relay

La escena de juego es **Face Landmark Detection**. Conserva las instancias de `enemigo1`, `enemigo3` y `personaje2`, los puntos `E1_punto1`–`E1_punto4`, el piso, dos superficies NavMesh horneadas y el seguimiento Face Mesh. Esta escena ya está habilitada como primera escena de Build Settings. Si Unity quedó en `SampleScene` o en una escena nueva vacía, un asistente del Editor abre una vez la escena integrada al importar el proyecto.

Al recibir `connect` de Flutter, Unity inicia la partida. `UnityGameSessionController` lleva la noche y la caja de Puppet, genera tareas con el formato que Flutter ya entiende, procesa `task_completed`/`task_failed` y publica `task_list`, `night_status`, `estado_puppet`, `attack` y `game_over`. La noche completa dura 2 minutos reales (seis horas de juego de 20 segundos cada una); la caja musical sigue la misma escala acelerada. La asignación de actores se puede cambiar en el Inspector: por defecto usa `enemigo1` como Freddy, `enemigo3` como Vixy y `personaje2` como Puppet. Freddy y Vixy comparten puntos de ruta en ambos lados del mapa, incluyendo el marcador `punto_penultimo_jumpscare`; eligen entre los puntos alcanzables más cercanos con más probabilidad para los trayectos cortos. Freddy se detiene mientras la cámara indica que lo miras. En el penúltimo punto, Freddy y Vixy tiran un jumpscare con 10% de probabilidad en la noche 1, aumentando 10 puntos porcentuales por noche (hasta 60% en la noche 6); si falla, esperan y vuelven a tirar en ese lugar. Vixy también causa jumpscare si mantiene 3 tareas propias sin resolver. Puppet conserva su salida al agotarse la caja y recorre su ruta física hasta el jugador. Completar o fallar una tarea rota a otra tarea; cada hora aparecen tareas nuevas de Freddy y Vixy. Al llegar a las 6 AM se avanza de noche en vez de finalizar la partida.

## Arranque

1. Para probar el enlace directo Flutter–Unity, cierra el servidor de juego original si está usando el puerto 8000.
2. Ejecuta `UnityRelay/relay.py` con el Python del entorno virtual que ya tiene `websockets` instalado. El relay solo reenvía mensajes y no modifica ni importa el backend original.
3. Abre en Unity `Assets/MediaPipeUnity/Samples/Scenes/Face Landmark Detection/Face Landmark Detection.unity` y pulsa Play.
4. Ejecuta el proyecto Flutter existente en `D:\ihc_completo\Five-Nights-of-Terror-Proyecto-IHC-main\Five-Nights-of-Terror-Proyecto-IHC-main\flutter`.
5. En Flutter abre **Opciones**, apaga **Modo simulado**, ingresa la IP local de esta PC y el puerto `8000`, y guarda.
6. Inicia o continúa la partida desde Flutter. Ese botón envía `connect` a Unity a través del relay.

Unity se conecta por `ws://127.0.0.1:8000`; Flutter debe usar la IP de la PC en la red local. Para que Flutter vea tareas creadas por Unity, debe conectarse al relay en vez de al servidor original.

## Mensajes entre Unity y Flutter

- Flutter → Unity: `connect`, `task_completed` (incluye `task_id` y `success`), `dar_cuerda_inicio` y `dar_cuerda_fin`.
- Unity → Flutter: `task_list`, `night_status`, `estado_puppet`, `attack` y `game_over`.

Flutter ya procesa esos tipos de mensaje y tiene la pantalla de configuración de servidor; no requiere cambios en su código. La copia del servidor original tampoco se modifica: el relay ocupa su puerto mientras se usa esta integración.

El proyecto original de Unity y el backend original quedan intactos. Este modo de relay enlaza Flutter directamente con Unity; no ejecuta ni conserva las funciones de juego del `servidor.py` original. La progresión de noche de esta integración se guarda localmente en Unity PlayerPrefs.
