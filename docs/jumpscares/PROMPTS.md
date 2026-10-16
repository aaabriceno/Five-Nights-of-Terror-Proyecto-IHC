# Prompts para generar los jumpscares

Una imagen de referencia por animatrónico (carpeta `referencias/`). Subir la
imagen en Gemini → "Crear video" y pegar el prompt. Formato 16:9.
Sin audio: el juego pone el sonido del jumpscare y la estática.

Archivos finales (nombres exactos, .mp4 o .webm):
`jumpscare_freddy`, `jumpscare_vixy`, `jumpscare_puppet`.

## Lo que falló en el primer intento

El prompt decía "zombie-like" y Gemini lo convirtió en un zombie de carne con
la cara deformándose. Los tres son **animatrónicos**: máquinas rígidas de
plástico, tela o madera. Por eso ahora se prohíbe explícitamente lo orgánico.

## Cómo es un jumpscare de FNaF 2

No hay caminata ni cuerpo entero. El cuadro empieza casi negro; el rostro
del animatrónico aparece pequeño y centrado, y en una fracción de segundo
crece de golpe hasta llenar la pantalla, con la cámara temblando y la imagen
parpadeando. Dura 1 a 2 segundos. La cámara no se mueve por un pasillo: el
animatrónico es el que se agranda.

## Freddy (izquierda) — `referencias/recorte_freddy.jpg`

The green plastic animatronic doll head with the fixed toothy grin from the
reference image, red shirt collar visible. Mouth stays in the same molded
grin.

Style: classic Five Nights at Freddy's 2 jumpscare animation. The character
is a malfunctioning animatronic, NOT a zombie, NOT a creature, NOT a person.
Hard plastic / rubber / felt surface, rigid face that never changes shape,
glassy fixed eyes, visible mechanical seams. No flesh, no skin, no wrinkles.
Fixed camera, no walking, no corridor, no full body shot. Black background
with almost no light. The face starts small and centered in the darkness and
in a split second it snaps forward, scaling up violently until it fills the
whole screen, with violent camera shake, flickering light and glitchy
stuttering motion. Total duration 1.5 seconds, very fast. No text, no music.

## Vixy (centro) — `referencias/recorte_vixy.jpg`

The dark metal and rubber animatronic head with the glowing red eyes from the
reference image. The red eyes glow brightest as it fills the screen.

Style: classic Five Nights at Freddy's 2 jumpscare animation. The character
is a malfunctioning animatronic, NOT a zombie, NOT a creature, NOT a person.
Hard plastic / rubber / felt surface, rigid face that never changes shape,
glassy fixed eyes, visible mechanical seams. No flesh, no skin, no wrinkles.
Fixed camera, no walking, no corridor, no full body shot. Black background
with almost no light. The face starts small and centered in the darkness and
in a split second it snaps forward, scaling up violently until it fills the
whole screen, with violent camera shake, flickering light and glitchy
stuttering motion. Total duration 1.5 seconds, very fast. No text, no music.

## Puppet (derecha) — `referencias/recorte_puppet.jpg`

The rigid wooden brown bear head with big round glass eyes and blue felt
sweater from the reference image. The head is a solid mask.

Style: classic Five Nights at Freddy's 2 jumpscare animation. The character
is a malfunctioning animatronic, NOT a zombie, NOT a creature, NOT a person.
Hard plastic / rubber / felt surface, rigid face that never changes shape,
glassy fixed eyes, visible mechanical seams. No flesh, no skin, no wrinkles.
Fixed camera, no walking, no corridor, no full body shot. Black background
with almost no light. The face starts small and centered in the darkness and
in a split second it snaps forward, scaling up violently until it fills the
whole screen, with violent camera shake, flickering light and glitchy
stuttering motion. Total duration 1.5 seconds, very fast. No text, no music.

## Origen de cada video (para el informe)

| Animatrónico | Cómo se hizo |
|---|---|
| Freddy | Generado con IA (Gemini) con el prompt de arriba y `referencias/recorte_freddy.jpg`; recortado al tramo del salto (1,45 s). |
| Puppet | Generado con IA (Gemini) con el prompt de arriba y `referencias/recorte_puppet.jpg`; recortado al tramo del salto (1,5 s). |
| Vixy | **Sin IA.** Lo genera `generar_jumpscare_vixy.py` a partir de la imagen de espera del juego (acercamiento, temblor, destellos y brillo de los ojos). El prompt de Vixy de arriba no se usó: Gemini devolvió un video de Puppet. |

Para regenerar el de Vixy: `python3 docs/jumpscares/generar_jumpscare_vixy.py` (Pillow, numpy y
ffmpeg con libvpx). El resultado es siempre el mismo.

## Después

Dejar los archivos en cualquier carpeta y avisar: se recortan al tramo del
salto (1–2.5 s), se convierten a `.webm` (VP8, 1280x720) y se copian a
`Unity/Assets/StreamingAssets/jumpscares/`.
