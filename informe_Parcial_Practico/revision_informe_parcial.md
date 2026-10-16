# Revisión del informe (`informe_Parcial_Practico/main.tex`)

El `.tex` **no fue modificado**. Esta es solo la lista de hallazgos. Las líneas son las del `main.tex` actual.
Compilado en una copia aparte: compila bien, 7 páginas (el rango pedido es 5 a 8). La única advertencia es la de `enumitem`
(etiqueta negativa en las referencias), que es inofensiva.

## A. Errores que hay que corregir

| # | Línea | Hallazgo | Corrección sugerida |
|---|---|---|---|
| A1 | 118 | `\item` **vacío** al final de la lista de la sección 2: sale una viñeta sin texto. | Borrar la línea, o llenarla con otro problema (ver sección C). |
| A2 | 110-116 | La figura de la caja musical está **dentro del `itemize`**, y el pie dice `\caption{Cajita musical: Interfaz cambiada} y facilidad con la respuesta tactil`: el texto "y facilidad..." quedó **fuera** del `\caption`. | Sacar la figura de la lista y cerrar bien el pie. |
| A3 | 107-117 | Estilo de la lista incoherente: solo el primer ítem lleva negrita. | Dar el mismo formato a todos (`\textbf{...}`). |
| A4 | 125 | `se;al` en vez de `señal`. | `señal`. |
| A5 | 127 | `comom` en vez de `como`. | `como`. |
| A6 | 117 | `si haberlas culminado` en vez de `sin haberlas culminado`. | `sin`. |
| A7 | 117 | `en un lugar no indicado .` con espacio antes del punto. | Quitar el espacio. |
| A8 | 109 | `windows:en` sin espacio tras los dos puntos y `windows` en minúscula. | `Windows: en`. |
| A9 | 4 y 38 | Título de portada y encabezado dicen **"Avance final"**, pero lo que se entrega es el informe del examen práctico parcial. | Que lo decidan ustedes; si cambia, que sea solo ese texto. |

## B. Ortografía y tildes

Sección 2 (líneas 107-117): `ultima` → última · `actualizacion` → actualización · `mecnica` → mecánica · `despues` → después ·
`mecanicos` → mecánicos · `creacion` → creación · `conexion` → conexión · `tactil` → táctil (2 veces) · `envio` → envío ·
`salian` → salían · `aparecian` → aparecían · `habiamos` → habíamos · `queriamos` → queríamos · `puppet` → Puppet.

Sección 3 (líneas 122-136): `Discusion` → Discusión · `centrara` → centrará · `opinion` → opiniones · `aveces` → a veces ·
`respondian` → respondían · `podia` → podía · `Tambien` → También · `enfasis` → énfasis · `animatronico` → animatrónico ·
`mecanicas` → mecánicas · `camara` → cámara · `perdidas` → pérdidas · `Fredy` / `fredy` → **Freddy** (línea 130-132 y 136).

Más: "de este modo" aparece dos veces seguidas en la línea 136; la frase "planteamos hacer que el videojuego se base con la
tablet a un costado" (línea 136) debe ser "se base en tener la tablet a un costado". La línea 107 es una oración de 60 palabras con
gerundio ("dejando de lado...") y conviene partirla.

Terminología: el informe usa "screamer" y "jumpscare" para lo mismo, y el juego lo llama jumpscare.
Elegir uno ("jumpscare") y mantenerlo.

## C. Falta contenido que ya existe y respalda lo corregido

El profesor pide "las correcciones a los problemas encontrados". Hoy la sección 2 tiene 3 problemas (uno sin evidencia, ver D1).
Esto es lo que se arregló tras la presentación y no aparece. Cada punto sale de los commits `142a582`, `a53a120`, `865237d`, `faf7bbe`:

| Problema de los testers | Qué se hizo | Evidencia disponible |
|---|---|---|
| No sabían quién los mató | La pantalla de derrota dice **TE ATRAPÓ <NOMBRE>** con icono y el motivo (Freddy: no mirar la pantalla; Vixy: tareas pendientes; Puppet: caja sin cuerda). | `12_te_atrapo_puppet.png` |
| Toques que no respondían | `Listener` en vez de `GestureDetector` (cancelaba el toque si el dedo se movía unos píxeles); en procesar datos un deslizamiento equivocado reinicia la secuencia y no termina la tarea. | commit `142a582` |
| Tablet difícil de leer / tocar | Botón de volver y X de 80x80 (antes 48x48), `SafeArea`, tamaños mayores en todos los minijuegos. | `08_lista_de_tareas.png`, minijuegos |
| Sin aviso de peligro | Avisos por vibración. | commit `142a582` |
| Muy fácil las primeras noches | Freddy: 32→22 s en la noche 1 (22, 20, 18, 16, 14, 12); Vixy avanza cada 6 s en vez de 8. | commits `a53a120`, `faf7bbe` |
| Jumpscare poco intenso | Video por animatrónico, 1.6 s, termina directo en "Perdiste". | `jumpscare_freddy_secuencia.jpg` |
| La app decía "Conectado" sin juego | Estado real con tiempo máximo de 8 s y pantalla "No se encontró el juego". | commit `865237d` |
| Textos en voseo ("Mirá") | Corregidos a "tú". | `tutorial_*.png` |

## D. Afirmaciones que no se pueden respaldar o necesitan matiz

- **D1.** Línea 109: "creamos scripts... .ps1 ... funcionaba perfectamente". **En el repositorio no hay ningún `.ps1`**
  (solo `iniciar.sh` para Linux). Si el script existe, no está versionado; conviene confirmarlo con tu compañero antes de afirmarlo.
- **D2.** Línea 127: "agregamos un video como parte final al recibir un screamer". Es cierto **solo para Freddy**;
  Vixy y Puppet siguen con la imagen fija. Decirlo así evita que el profesor espere los tres.
- **D3.** Línea 136: la solución "tablet a un costado" frente al truco de la cabeza. El detector actual mide la **inclinación vertical**
  (proporción frente-nariz / nariz-mentón), no el giro lateral. Poner la tablet a un lado **no está verificado**; presentarlo como
  propuesta, no como corrección ya hecha.
- **D4.** Línea 84: "No presenta resultados de una evaluación formal con participantes, porque no consta una prueba..."
  **contradice la sección 3**, que justamente discute las opiniones de los testers. Además "materiales revisados" y "versión examinada"
  suenan a un revisor externo.
- **D5.** Línea 80 cita `[R1]` (documentación de Flutter) para describir el **juego completo**; no corresponde. Y **R2, R3, R4 y R5
  no se citan en ninguna parte**. Posibles lugares: R2 en la línea 86 (MediaPipe), R3 en la 87 (`NavMeshAgent`), R4 en la 104 (WebSocket),
  R5 donde se hable de los cables por forma y color.

Verificado en el código y **correcto**: 10 minijuegos (cables, perillas, secuencia, ritmo, WiFi, ventiladores, temperatura, procesar datos,
subir datos, trazar curso), 6 noches, Unity 2022.3.30f1, `HeadOrientationTracker` y `mirandoPC`, los mensajes `connect`, `task_list`,
`night_status`, `estado_puppet`, `task_completed`, `task_failed`, `attack`, `game_over`, `NavMeshAgent`, `PlayerPrefs`,
tutorial de tres pantallas.

## E. Figuras

- El informe usa **2 figuras** y en la carpeta hay **7 imágenes sin usar**: `menu.png`, `tutorial_mision.png`, `tutorial_minitareas.png`,
  `tutorial_prueba_pc.png`, `tutorial_prueba_tablet.png`, `cables_solo_colores_anterior.png`, `cables_formas_actual.png`.
- Las conclusiones 2 y 3 hablan del **tutorial** y de **cables por formas**, pero esas secciones no aparecen antes ni muestran su figura.
  Lo natural es usar `cables_*` (antes/ahora) en la sección 2 y el tutorial en la 3.
- Reglas del profesor: una figura por página (o figuras al final como anexo, que no cuentan en las 5-8 páginas) y no más del
  45 % de la página. Las 2 actuales cumplen. Para sumar más hay que elegir cuáles, o usar un anexo, que sí cambiaría la estructura.
  Esa decisión es de ustedes.
- Las capturas de la tablet son de 2000x1200; a `0.5\linewidth` el texto interno queda chico. Aceptable, pero legible solo en el PDF.

## F. Lo que está bien

- La estructura pedida está completa y en orden: carátula, descripción, problemas, discusión, conclusiones y referencias.
- Compila sin errores, 7 páginas, sin advertencias reales.
- Las secciones 1 y 4 están bien redactadas y sus datos técnicos coinciden con el código.

## Qué sugiero hacer primero

1. A1-A8 y todo lo de la sección B (son correcciones seguras, sin cambiar el sentido).
2. D1-D4: lo que podría quedar mal si el profesor pregunta.
3. C: sumar los problemas que faltan, con su evidencia.
4. E y D5: figuras y citas, al final.
