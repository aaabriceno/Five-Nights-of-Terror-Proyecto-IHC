# Diseño — Arrancar todo el juego con un doble clic

Fecha: 2026-10-09

## Objetivo

Hoy, para jugar, hay que levantar tres cosas por separado: correr el relay
en una terminal, darle Play al editor de Unity, y conectar la tablet. El
profesor pidió que el juego se abra como un programa normal: **doble clic, se
levantan el relay y el juego juntos, aparece la pantalla de "Esperando..." y
lo único que queda es conectar la tablet a la IP de esa PC.**

La tablet no cambia: la app ya instalada sigue funcionando igual.

## Fases

| Fase | Qué | Dónde | Cuándo |
|---|---|---|---|
| 1 | Exportar el juego y script de arranque | Linux (laptop Ubuntu 24.04) | Ahora |
| 2 | Mostrar la IP en la pantalla de espera | Unity, ambos sistemas | Ahora |
| 3 | El juego arranca el relay solo (sin script) | Linux, después Windows | Después |

Las fases 1 y 2 dan algo que funciona hoy en la laptop. La 3 elimina el
script y la necesidad de tener Python instalado en la máquina de la demo.

## Fase 1 — Script de arranque (Linux)

### 1.1 Exportar el juego: `exportar_linux.sh`

Unity puede generar el ejecutable desde la terminal, sin abrir el editor:

```
Unity -batchmode -quit -projectPath Unity \
      -buildLinux64Player Unity/Builds/Linux/FiveNightsOfTerror.x86_64 \
      -logFile -
```

- Usa la escena ya configurada en Build Settings (`Face Landmark Detection`).
- La salida va a `Unity/Builds/Linux/`, que `.gitignore` ya excluye.
- La ruta del editor sale de la variable `UNITY_EDITOR`, con valor por
  defecto `~/Unity/Hub/Editor/2022.3.30f1/Editor/Unity`.
- **Requisito:** el editor de Unity tiene que estar cerrado con este
  proyecto; Unity no permite dos instancias sobre el mismo proyecto.
- Si la exportación falla, el script termina con error y muestra dónde
  mirar, en vez de dejar un ejecutable viejo que parezca nuevo.

### 1.2 Arrancar todo: `iniciar.sh` (raíz del repo)

En orden:

1. **Si el puerto 8000 ya está ocupado**, asume que ya hay un relay
   corriendo (por ejemplo, uno abierto a mano) y lo usa. No arranca otro ni
   lo apaga al final, porque no es suyo.
2. Si está libre, **arranca el relay** con el Python de `UnityRelay/.venv`.
   Su salida va a `Unity/Builds/Linux/relay.log`, porque al lanzarlo con
   doble clic no hay terminal donde verla.
3. **Espera a que el puerto 8000 esté escuchando**, hasta 10 segundos. Si no
   arranca, avisa y termina sin abrir el juego.
4. **Abre el juego** y espera a que se cierre.
5. **Al cerrar el juego apaga el relay**, también si se corta con Ctrl+C o
   si el script termina por error, para no dejar el puerto ocupado.

Si falta el ejecutable exportado o el `.venv`, lo dice con un mensaje claro
indicando qué correr.

Agregado tras la revisión final:

- **El puerto ocupado solo se reutiliza si es de un relay.** Si quien escucha
  en el 8000 es otro programa (es el puerto por defecto de varios servidores
  de desarrollo), avisa y no abre el juego, en vez de abrirlo con la tablet
  sin poder conectarse.
- **Una sola copia a la vez.** Si se vuelve a hacer doble clic mientras el
  juego carga, la segunda copia avisa "ya se está abriendo" y termina. Sin
  esto se abrían dos juegos, y al cerrar el primero se apagaba el relay del
  que dependía el segundo.

### 1.3 Doble clic en Ubuntu: `crear_acceso_directo.sh`

Ubuntu no ejecuta un `.sh` con doble clic; lo abre como texto. Este script
genera un acceso directo `.desktop` en el Escritorio que lanza `iniciar.sh`
con la ruta absoluta correcta. La ruta del proyecto tiene espacios
(`Semestre 2026-2`), así que el `.desktop` se genera con las comillas
necesarias en vez de escribirse a mano.

La primera vez Ubuntu pide confirmar que el acceso directo es confiable
(clic derecho → "Permitir ejecutar").

## Fase 2 — IP en la pantalla de espera

`PantallaDeEspera` suma una segunda línea bajo "Esperando...":

```
Esperando...
Conecta la tablet a 192.168.1.7 : 8000
```

**Cómo se obtiene la IP:** se abre un socket UDP "conectado" a una dirección
externa y se lee la dirección local que el sistema eligió. No se manda ningún
paquete; solo revela la interfaz de la ruta por defecto. Así se evitan
direcciones que no sirven para la tablet, como la de Docker (`172.17.0.1`)
presente en la laptop.

Si eso falla (PC sin ruta de red), se recorren las interfaces activas y se
toma la primera IPv4 privada que no sea de loopback ni de Docker. Si tampoco
hay, se muestra "Sin red — conecta la PC al WiFi".

La IP se recalcula cada pocos segundos mientras la pantalla está visible, por
si la PC se conecta a la red después de abrir el juego.

El puerto (8000) es el mismo del relay; si algún día cambia, se lee de un
único lugar.

## Fase 3 — El juego arranca el relay solo

### 3.1 Empaquetar el relay

`relay.py` se convierte en un único ejecutable con PyInstaller
(`--onefile`): `relay` en Linux, `relay.exe` en Windows. **El código Python
no cambia.** El script de exportación copia ese ejecutable dentro de la
carpeta del juego.

PyInstaller no genera ejecutables para otro sistema: el de Windows se
construye en la PC con Windows.

### 3.2 `LanzadorDeRelay.cs` (nuevo)

- Al abrir el juego, si el puerto 8000 está libre, arranca el ejecutable del
  relay en segundo plano, sin ventana.
- Si el puerto ya está ocupado, no hace nada y usa el relay existente. Eso
  cubre el desarrollo con `relay.py` a mano y el caso de un relay huérfano
  que quedó de un cierre abrupto: en vez de fallar, se reutiliza.
- Al cerrar el juego, apaga el relay que arrancó él (y solo ese).
- **Solo actúa en el ejecutable exportado, nunca en el editor**, para no
  cambiar el flujo de trabajo actual.

### 3.3 Reintento de conexión en `UnityWebSocketClient`

Hoy el cliente intenta conectarse una sola vez, en `Start()`. Si el relay
todavía no está escuchando, queda desconectado para siempre. Con el relay
arrancando a la par del juego eso puede pasar, así que el cliente pasa a
reintentar cada segundo hasta conectarse, y también si la conexión se cae.

En la fase 1 no hace falta porque el script espera al relay antes de abrir
el juego, pero el reintento no la perjudica.

### 3.4 Windows

En la PC con Windows hace falta:

- **El módulo "Windows Build Support (Mono)"** en Unity Hub.
- **El paquete de MediaPipe**, que no está en el repo por su tamaño (393 MB);
  se instala a mano según el README.
- Python con PyInstaller, solo para generar `relay.exe` una vez.
- **No hace falta Flutter**: la app ya está instalada en la tablet.

## Qué no cambia

- El protocolo entre tablet, relay y Unity.
- `relay.py` y su forma de uso durante el desarrollo.
- La app de la tablet.
- El flujo dentro del editor de Unity.

## Riesgos

- **La exportación por terminal falla si el editor está abierto.** El script
  lo detecta y lo avisa.
- **`productName` es `Poryecto_IHC`** (con errata). Es el título de la
  ventana y la carpeta donde se guardan las preferencias (cámara elegida,
  noche alcanzada). Cambiarlo borraría esas preferencias guardadas, así que
  queda fuera de este trabajo; si se corrige, hay que hacerlo a propósito.
- **Primera ejecución del ejecutable:** la cámara se vuelve a autodetectar
  la primera vez, igual que en el editor, con el ruido de errores de
  MediaPipe en el log mientras descarta modos. Es esperado.

## Cómo se verifica

**Fase 1 y 2, en la laptop:**
1. `./exportar_linux.sh` genera `Unity/Builds/Linux/FiveNightsOfTerror.x86_64`.
2. Con el relay apagado, `./iniciar.sh` abre el juego en pantalla completa
   con la imagen de espera y la IP correcta.
3. La tablet se conecta a esa IP y se juega una noche completa.
4. Al cerrar el juego, el puerto 8000 queda libre (`ss -tlnp | grep 8000`
   no muestra nada).
5. Con `relay.py` ya corriendo a mano, `./iniciar.sh` lo reutiliza y al
   cerrar el juego **no** lo apaga.
6. El acceso directo del Escritorio hace lo mismo con doble clic.

**Fase 3:** los mismos pasos, abriendo directamente el ejecutable del juego
en vez del script, primero en Linux y después en Windows.
