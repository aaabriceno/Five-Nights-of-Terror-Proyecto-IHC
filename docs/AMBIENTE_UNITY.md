# Ambiente del juego en Unity (2026-11-12)

Qué se hizo por código para que el juego se sienta más oscuro e inmersivo sin tocar los modelos ni el archivo `.unity` que comparte el equipo, cómo se prueba y qué queda para Marcelo (Blender).

## Lo que había

- Pipeline integrado de Unity 2022.3, sin URP ni posprocesado.
- 10 luces sin sombras; iluminación global marcada como «baked» pero nunca horneada; niebla apagada; skybox por defecto (cielo azul por los huecos sobre los ductos y a los lados).
- Freddy, Vixy y Puppet son `.obj` sin esqueleto: sin animación, se deslizan por el NavMesh.
- Tres fuentes de audio 2D (música, efectos, estática): nada posicional.

## Lo que hay ahora

Todo lo arma `AjusteDeAmbiente.cs` al cargar la escena (`RuntimeInitializeOnLoadMethod`), así que el `.unity` queda igual para Marcelo. Los números viven en `AmbienteDelJuego.cs` (puro, con pruebas Mono en `pruebas/PruebaAmbienteDelJuego.cs`).

| Pieza | Archivo | Qué hace |
|---|---|---|
| Cielo negro, ambiente oscuro, niebla | `AjusteDeAmbiente.cs` | `Camera.clearFlags = SolidColor` negro (tapa los huecos), ambiente trilight bajo, niebla exponencial oscura cuya densidad sube con la noche |
| Posprocesado | `Posprocesado.cs` + `Resources/shaders/Posprocesado.shader` | Viñeta, grano proporcional a la luz, aberración cromática, color frío y desaturado, bloom (4 pasadas a 1/4 de resolución, solo nivel alto) y la **estática** de la pantalla. Solo durante la partida |
| Ojos brillantes | `AjusteDeAmbiente.PonerOjos` + `shaders/Emisivo.shader` | Dos esferas emisivas a la altura de la cara, calculadas de la caja del modelo en metros del mundo (Freddy rojos, Vixy ámbar, Puppet blancos). No les afecta la niebla: en un ducto a oscuras se ven solo los ojos |
| Balanceo procedural | `BalanceoDeAnimatronico.cs` | Cuelga las mallas de un pivote y lo balancea (±4,5°), cabecea y sube hasta 3,5 cm con cada paso según la velocidad del agente; quieto, gira el cuerpo hasta 60° hacia el jugador |
| Pasos posicionales | `SonidoDeAnimatronico.cs` | Fuente 3D por animatrónico con un sonido generado en código (golpe + chasquido + chirrido); volumen y ritmo según la velocidad. El jugador 1 oye de qué lado viene |
| Luces que reaccionan | `AjusteDeAmbiente.Update` | La oficina parpadea desde que Freddy está a 8 m y se apaga 0,36 s cada 3 s a menos de 5 m; luz roja pulsante cuando la caja de Puppet baja del 20 %; las luces `Alerta_Ventilacion*` titilan cuando un animatrónico entra en 1,5 veces su alcance; parpadeos sueltos en las demás luces, más frecuentes cada noche; la luz baja 7 % por noche (mínimo 0,65) |
| Sombras | `AjusteDeAmbiente.ClasificarLuces` | Solo en nivel alto: la luz de la oficina proyecta sombras suaves |
| Polvo | `AjusteDeAmbiente.CrearPolvo` + `shaders/Aditivo.shader` | Partículas lentas alrededor de la luz de la oficina (9/s en alto, 3/s en básico) |
| Respiración de la cámara | `UnityGameSessionController.ActualizarCamara` | Cabeceo de ±0,3° cada 4 s y balanceo de ±0,15° |
| Estática | `AmbienteDelJuego.PedirEstatica` | 0,8 s cuando el jugador 2 actúa (retraso, trampa, rescate), 1,2 s si se desconecta, 1 s si la tablet se cae. Baja en línea recta y nunca tapa el 100 % de la imagen |

### Nivel gráfico

`PerfilDeVideo.NivelGrafico(tarjeta, argumentos)`: **alto** con tarjeta dedicada (NVIDIA, Radeon RX, Intel Arc), **básico** con integrada o sin tarjeta (llvmpipe). `-graficos-alto` y `-graficos-basico` mandan sobre la tarjeta. El básico no tiene bloom ni sombras y pone menos polvo; todo lo demás es igual.

### Opciones de prueba

- `-sin-ambiente`: nada de lo anterior (para comparar o para pruebas viejas).
- `-prueba-posar <Freddy|Vixy|Puppet>`: ese animatrónico se planta a 3,5 m frente al jugador, mirándolo, y no se mueve.
- `-prueba-estatica`: la estática queda a nivel medio, fija.

`pruebas/capturar_ambiente.py <carpeta> [escena ...]` abre el juego exportado en una pantalla virtual (Xvfb, render por software) y guarda una captura por escena: `oficina_sin_ambiente`, `oficina`, `freddy`, `vixy`, `puppet`, `estatica`, `basico`. Sirve para ver la composición, no los FPS. El log del juego deja `[Ambiente] nivel …, N luces …` al arrancar.

## Lo que queda para Marcelo (Blender)

- Esqueleto y clips de caminar, quieto y jumpscare para los tres modelos (exportar FBX con armadura y armar un `Animator`). Cuando existan, `BalanceoDeAnimatronico` se puede quitar o dejar como capa extra.
- Mapas de normales en el shader «Mapa V2 Desgastado» (es un surface shader: basta añadir `_BumpMap`).
- Cerrar el techo sobre los ductos y los huecos laterales (el cielo negro los disimula, pero siguen ahí).
- Props: afiches, cables, puertas de ducto que se abran.
- Opcional: hornear lightmaps y light probes en el editor (el shader del mapa ya los soporta) y marcar el mapa como estático.
