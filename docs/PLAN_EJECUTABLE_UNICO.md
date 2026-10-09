# Ejecutable único (fases 1 y 2) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que en la laptop Ubuntu un doble clic levante el relay y el juego exportado juntos, y que la pantalla de espera muestre la IP a la que se conecta la tablet.

**Architecture:** Un script `iniciar.sh` orquesta los dos procesos (relay de Python + juego exportado de Unity) y garantiza que el relay se apague al cerrar el juego. `exportar_linux.sh` genera el ejecutable con Unity en modo `-batchmode`. En Unity, una clase sin dependencias de UnityEngine (`DireccionDeRed`) detecta la IP de la red local y `PantallaDeEspera` la muestra.

**Tech Stack:** Bash, Unity 2022.3.30f1 (C#), Mono de Unity (`mcs`/`mono`) para probar C# fuera del editor, Python 3.12 (relay existente, sin cambios).

**Spec:** `docs/DISENO_EJECUTABLE_UNICO.md`. Este plan cubre las fases 1 y 2; la fase 3 (el juego arranca el relay solo) tendrá su propio plan.

## Global Constraints

- Nombres de variables, funciones, clases y comentarios en castellano.
- Todo texto que aparece en pantalla usa tú: "Conecta", "Exporta", "Revisa" — nunca voseo.
- Puerto del relay: `8000`. El relay escucha en `0.0.0.0`.
- Editor de Unity: variable `UNITY_EDITOR`, por defecto `~/Unity/Hub/Editor/2022.3.30f1/Editor/Unity`.
- Ejecutable exportado: `Unity/Builds/Linux/FiveNightsOfTerror.x86_64` (`Unity/Builds/` ya está en `.gitignore`).
- No se modifican `UnityRelay/relay.py`, el protocolo JSON ni la app Flutter.
- No se commitea `Flutter/pubspec.lock` (difiere por versión de Flutter entre compañeros).
- Commits: mensaje en castellano, **sin** trailer `Co-Authored-By`, verificar con `git log -1` después de cada uno, y solo con confirmación del usuario.

## Review Focus

- Puerto 8000 ya ocupado por un relay abierto a mano → se reutiliza y **no** se apaga al cerrar el juego.
- Script interrumpido (Ctrl+C, cerrar la terminal) mientras el juego corre → el relay se apaga y el puerto queda libre.
- El relay muere al arrancar (falta `websockets`, Python roto) → no se abre el juego y se avisa dónde mirar.
- Juego nunca exportado o exportación fallida → mensaje que indica correr `./exportar_linux.sh`, sin dejar el relay corriendo.
- PC con Docker o varias interfaces (`docker0` = `172.17.0.1`) → se muestra la IP del WiFi, nunca la de Docker.

Cada una tiene su prueba en la tarea que la implementa (Tareas 1 y 2).

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `iniciar.sh` (nuevo) | Levanta relay + juego, apaga el relay al salir |
| `exportar_linux.sh` (nuevo) | Genera el ejecutable Linux con Unity en `-batchmode` |
| `crear_acceso_directo.sh` (nuevo) | Crea el `.desktop` del Escritorio que lanza `iniciar.sh` |
| `pruebas/probar_iniciar.sh` (nuevo) | Pruebas de `iniciar.sh` con un juego falso |
| `Unity/Assets/scripts/DireccionDeRed.cs` (nuevo) | Detecta la IP local útil para la tablet; sin UnityEngine |
| `pruebas/PruebaDireccionDeRed.cs` (nuevo) | Pruebas de `DireccionDeRed` (fuera de `Assets/`, Unity no la compila) |
| `pruebas/probar_direccion_de_red.sh` (nuevo) | Compila y corre esas pruebas con el Mono de Unity |
| `Unity/Assets/scripts/PantallaDeEspera.cs` | + línea "Conecta la tablet a IP : puerto" |
| `Unity/Assets/scripts/UnityWebSocketClient.cs` | + propiedad `Puerto` leída de `serverUrl` |

---

### Task 1: `iniciar.sh` — relay y juego juntos

**Files:**
- Create: `iniciar.sh`
- Test: `pruebas/probar_iniciar.sh`

**Interfaces:**
- Produces: `iniciar.sh` acepta por variable de entorno (para pruebas) `EJECUTABLE_JUEGO`, `PYTHON_RELAY`, `REGISTRO_RELAY`, `SIN_NOTIFICACIONES=1`. Sin variables usa las rutas reales. Código de salida: el del juego si todo salió bien; `1` si falta algo o el relay no arranca.

- [ ] **Step 1: Escribir las pruebas**

`pruebas/probar_iniciar.sh`:

```bash
#!/usr/bin/env bash
# Pruebas de iniciar.sh sin abrir el juego real: EJECUTABLE_JUEGO apunta a un
# juego falso que solo anota si el relay estaba escuchando y termina.
set -u

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PUERTO=8000
TEMP="$(mktemp -d)"
FALLOS=0
export SIN_NOTIFICACIONES=1

puerto_escuchando() { (exec 3<>"/dev/tcp/127.0.0.1/$PUERTO") 2>/dev/null; }

esperar_puerto_libre() {
  for _ in $(seq 1 20); do
    puerto_escuchando || return 0
    sleep 0.5
  done
  return 1
}

comprobar() {
  local descripcion="$1"
  shift
  if "$@"; then
    echo "  ok    $descripcion"
  else
    echo "  FALLA $descripcion"
    FALLOS=$((FALLOS + 1))
  fi
}

trap 'rm -rf "$TEMP"' EXIT

if puerto_escuchando; then
  echo "El puerto $PUERTO está ocupado. Cierra el relay antes de correr las pruebas."
  exit 2
fi

cat > "$TEMP/juego_falso.sh" <<'EOF'
#!/usr/bin/env bash
if (exec 3<>/dev/tcp/127.0.0.1/8000) 2>/dev/null; then echo si > "$MARCA"; else echo no > "$MARCA"; fi
sleep "${DURACION:-0}"
EOF
chmod +x "$TEMP/juego_falso.sh"

echo "Caso 1: puerto libre"
MARCA="$TEMP/marca1" EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" REGISTRO_RELAY="$TEMP/relay1.log" \
  "$RAIZ/iniciar.sh" >/dev/null 2>&1
codigo=$?
comprobar "termina sin error" test "$codigo" -eq 0
comprobar "el relay escuchaba al abrir el juego" grep -qx si "$TEMP/marca1"
comprobar "el puerto queda libre al cerrar" esperar_puerto_libre

echo "Caso 2: ya hay un relay corriendo"
"$RAIZ/UnityRelay/.venv/bin/python" "$RAIZ/UnityRelay/relay.py" >"$TEMP/externo.log" 2>&1 &
PID_EXTERNO=$!
for _ in $(seq 1 20); do puerto_escuchando && break; sleep 0.5; done
MARCA="$TEMP/marca2" EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" REGISTRO_RELAY="$TEMP/relay2.log" \
  "$RAIZ/iniciar.sh" >/dev/null 2>&1
comprobar "usa el relay existente" grep -qx si "$TEMP/marca2"
comprobar "no apaga el relay ajeno" kill -0 "$PID_EXTERNO"
kill "$PID_EXTERNO" 2>/dev/null
wait "$PID_EXTERNO" 2>/dev/null
esperar_puerto_libre

echo "Caso 3: se interrumpe mientras el juego corre"
MARCA="$TEMP/marca3" DURACION=30 EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" REGISTRO_RELAY="$TEMP/relay3.log" \
  "$RAIZ/iniciar.sh" >/dev/null 2>&1 &
PID_INICIAR=$!
for _ in $(seq 1 20); do [ -f "$TEMP/marca3" ] && break; sleep 0.5; done
kill -TERM "$PID_INICIAR"
wait "$PID_INICIAR" 2>/dev/null
comprobar "el puerto queda libre tras interrumpir" esperar_puerto_libre

echo "Caso 4: falta el juego exportado"
EJECUTABLE_JUEGO="$TEMP/no_existe" REGISTRO_RELAY="$TEMP/relay4.log" \
  "$RAIZ/iniciar.sh" >"$TEMP/salida4.txt" 2>&1
codigo=$?
comprobar "termina con error" test "$codigo" -ne 0
comprobar "indica cómo exportarlo" grep -q "exportar_linux.sh" "$TEMP/salida4.txt"
comprobar "no deja el relay corriendo" esperar_puerto_libre

echo "Caso 5: el relay muere al arrancar"
PYTHON_RELAY=/bin/false MARCA="$TEMP/marca5" EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" \
  REGISTRO_RELAY="$TEMP/relay5.log" "$RAIZ/iniciar.sh" >"$TEMP/salida5.txt" 2>&1
codigo=$?
comprobar "termina con error" test "$codigo" -ne 0
comprobar "no abre el juego" test ! -f "$TEMP/marca5"
comprobar "indica dónde mirar" grep -q "relay" "$TEMP/salida5.txt"

echo
if [ "$FALLOS" -eq 0 ]; then
  echo "Todas las pruebas pasaron."
else
  echo "$FALLOS prueba(s) fallaron."
  exit 1
fi
```

Run: `chmod +x pruebas/probar_iniciar.sh`

- [ ] **Step 2: Correr las pruebas y verificar que fallan**

Run: `./pruebas/probar_iniciar.sh`
Expected: varias `FALLA` (no existe `iniciar.sh`) y salida `N prueba(s) fallaron.`

- [ ] **Step 3: Implementar `iniciar.sh`**

```bash
#!/usr/bin/env bash
# Levanta el relay y el juego juntos, y apaga el relay al cerrar el juego.
# Se lanza también con doble clic (ver crear_acceso_directo.sh); como ahí no
# hay terminal, los errores se avisan además con una notificación.
set -u

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUERTO=8000
JUEGO="${EJECUTABLE_JUEGO:-$RAIZ/Unity/Builds/Linux/FiveNightsOfTerror.x86_64}"
PYTHON_RELAY="${PYTHON_RELAY:-$RAIZ/UnityRelay/.venv/bin/python}"
REGISTRO="${REGISTRO_RELAY:-$RAIZ/Unity/Builds/Linux/relay.log}"

PID_RELAY=""
PID_JUEGO=""

avisar() {
  echo "$1" >&2
  if [ -z "${SIN_NOTIFICACIONES:-}" ] && command -v notify-send >/dev/null; then
    notify-send "Five Nights of Terror" "$1"
  fi
}

puerto_escuchando() { (exec 3<>"/dev/tcp/127.0.0.1/$PUERTO") 2>/dev/null; }

# Solo se apaga lo que este script arrancó: un relay ajeno no se toca.
limpiar() {
  if [ -n "$PID_JUEGO" ] && kill -0 "$PID_JUEGO" 2>/dev/null; then
    kill "$PID_JUEGO" 2>/dev/null
  fi
  if [ -n "$PID_RELAY" ] && kill -0 "$PID_RELAY" 2>/dev/null; then
    kill "$PID_RELAY" 2>/dev/null
    wait "$PID_RELAY" 2>/dev/null
  fi
}
trap limpiar EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if [ ! -x "$JUEGO" ]; then
  avisar "No se encontró el juego exportado en $JUEGO. Expórtalo primero con ./exportar_linux.sh"
  exit 1
fi

if puerto_escuchando; then
  echo "Ya hay un relay escuchando en el puerto $PUERTO: se usa ese."
else
  if [ ! -x "$PYTHON_RELAY" ]; then
    avisar "No se encontró el Python del relay en $PYTHON_RELAY. Crea el entorno: cd UnityRelay && python3 -m venv .venv && .venv/bin/pip install -r requirements.txt"
    exit 1
  fi
  mkdir -p "$(dirname "$REGISTRO")"
  "$PYTHON_RELAY" -u "$RAIZ/UnityRelay/relay.py" >>"$REGISTRO" 2>&1 &
  PID_RELAY=$!
  for _ in $(seq 1 20); do
    puerto_escuchando && break
    if ! kill -0 "$PID_RELAY" 2>/dev/null; then
      PID_RELAY=""
      avisar "El relay se cerró al arrancar. Revisa $REGISTRO"
      exit 1
    fi
    sleep 0.5
  done
  if ! puerto_escuchando; then
    avisar "El relay no empezó a escuchar en el puerto $PUERTO a tiempo. Revisa $REGISTRO"
    exit 1
  fi
fi

# En segundo plano + wait: así una señal interrumpe la espera y el trap corre
# enseguida, en vez de esperar a que el juego termine por su cuenta.
"$JUEGO" &
PID_JUEGO=$!
wait "$PID_JUEGO"
```

Run: `chmod +x iniciar.sh`

- [ ] **Step 4: Correr las pruebas y verificar que pasan**

Run: `./pruebas/probar_iniciar.sh`
Expected: todas `ok`, salida `Todas las pruebas pasaron.`

- [ ] **Step 5: Commit (con confirmación del usuario)**

```bash
git add iniciar.sh pruebas/probar_iniciar.sh
git commit -m "Agregar script que levanta el relay y el juego juntos"
git log -1
```

---

### Task 2: `DireccionDeRed` — IP local útil para la tablet

**Files:**
- Create: `Unity/Assets/scripts/DireccionDeRed.cs`
- Test: `pruebas/PruebaDireccionDeRed.cs`, `pruebas/probar_direccion_de_red.sh`

**Interfaces:**
- Produces:
  - `public static string DireccionDeRed.ObtenerIpLocal()` → IP como texto o `null` si no hay red.
  - `public static bool DireccionDeRed.EsUtilParaLaTablet(IPAddress direccion, string nombreInterfaz)`
  - `public static bool DireccionDeRed.EsPrivada(IPAddress direccion)`

- [ ] **Step 1: Escribir las pruebas**

`pruebas/PruebaDireccionDeRed.cs`:

```csharp
using System;
using System.Net;

// Pruebas de DireccionDeRed fuera de Unity: está fuera de Assets/ para que el
// editor no la compile (tiene su propio Main).
public static class PruebaDireccionDeRed
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static bool Util(string ip, string interfaz) =>
        DireccionDeRed.EsUtilParaLaTablet(IPAddress.Parse(ip), interfaz);

    public static int Main()
    {
        Comprobar("acepta la IP del WiFi", Util("192.168.1.7", "wlo1"));
        Comprobar("acepta una red 10.x", Util("10.20.30.40", "eth0"));
        Comprobar("descarta loopback", !Util("127.0.0.1", "lo"));
        Comprobar("descarta el puente de Docker por dirección", !Util("172.17.0.1", "docker0"));
        Comprobar("descarta otras redes de Docker por nombre", !Util("172.18.0.1", "br-1a2b3c4d"));
        Comprobar("descarta link-local sin DHCP", !Util("169.254.10.20", "wlo1"));
        Comprobar("descarta IPv6", !Util("fe80::1", "wlo1"));
        Comprobar("192.168.x es privada", DireccionDeRed.EsPrivada(IPAddress.Parse("192.168.0.5")));
        Comprobar("172.20.x es privada", DireccionDeRed.EsPrivada(IPAddress.Parse("172.20.1.1")));
        Comprobar("8.8.8.8 no es privada", !DireccionDeRed.EsPrivada(IPAddress.Parse("8.8.8.8")));

        string ip = DireccionDeRed.ObtenerIpLocal();
        Console.WriteLine("  info  IP detectada en esta PC: " + (ip ?? "(ninguna)"));
        Comprobar("la IP detectada no es la de Docker", ip == null || !ip.StartsWith("172.17."));
        Comprobar("la IP detectada no es loopback", ip == null || !ip.StartsWith("127."));

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
```

`pruebas/probar_direccion_de_red.sh`:

```bash
#!/usr/bin/env bash
# Compila DireccionDeRed.cs con sus pruebas usando el Mono que trae Unity y
# las corre, sin abrir el editor.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MONO_BIN="${MONO_BIN:-$HOME/Unity/Hub/Editor/2022.3.30f1/Editor/Data/MonoBleedingEdge/bin}"
TEMP="$(mktemp -d)"
trap 'rm -rf "$TEMP"' EXIT

"$MONO_BIN/mcs" -out:"$TEMP/prueba.exe" \
  "$RAIZ/Unity/Assets/scripts/DireccionDeRed.cs" \
  "$RAIZ/pruebas/PruebaDireccionDeRed.cs"
"$MONO_BIN/mono" "$TEMP/prueba.exe"
```

Run: `chmod +x pruebas/probar_direccion_de_red.sh`

- [ ] **Step 2: Correr y verificar que falla**

Run: `./pruebas/probar_direccion_de_red.sh`
Expected: error de compilación de `mcs` porque `DireccionDeRed.cs` no existe.

- [ ] **Step 3: Implementar `DireccionDeRed.cs`**

```csharp
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>
/// Averigua la IP de esta PC en la red local: la que la tablet debe usar para
/// conectarse al relay. No usa UnityEngine para poder probarse fuera del
/// editor (pruebas/probar_direccion_de_red.sh).
/// </summary>
public static class DireccionDeRed
{
    /// IP como texto, o null si la PC no está en ninguna red.
    public static string ObtenerIpLocal()
    {
        string porRuta = IpDeLaRutaPorDefecto();
        if (porRuta != null) return porRuta;

        // Sin ruta por defecto (red sin salida a internet): se toma la primera
        // interfaz activa con una IPv4 privada que la tablet pueda alcanzar.
        foreach (NetworkInterface interfaz in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (interfaz.OperationalStatus != OperationalStatus.Up) continue;
            foreach (UnicastIPAddressInformation informacion in interfaz.GetIPProperties().UnicastAddresses)
            {
                IPAddress direccion = informacion.Address;
                if (EsUtilParaLaTablet(direccion, interfaz.Name) && EsPrivada(direccion))
                    return direccion.ToString();
            }
        }
        return null;
    }

    /// Un socket UDP "conectado" no envía ningún paquete, pero obliga al
    /// sistema a elegir la interfaz por la que saldría el tráfico: esa es la de
    /// la red real (WiFi o cable), no la de Docker ni la de loopback.
    private static string IpDeLaRutaPorDefecto()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint local = socket.LocalEndPoint as IPEndPoint;
                if (local != null && EsUtilParaLaTablet(local.Address, string.Empty))
                    return local.Address.ToString();
            }
        }
        catch (SocketException)
        {
        }
        return null;
    }

    /// La tablet solo puede llegar a una IPv4 de la red física: se descartan
    /// loopback, link-local (169.254.x.x, señal de que no hubo DHCP) y las
    /// redes virtuales de Docker, que existen en la PC pero no hacia afuera.
    public static bool EsUtilParaLaTablet(IPAddress direccion, string nombreInterfaz)
    {
        if (direccion.AddressFamily != AddressFamily.InterNetwork) return false;
        if (IPAddress.IsLoopback(direccion)) return false;

        string nombre = nombreInterfaz ?? string.Empty;
        if (nombre.StartsWith("docker") || nombre.StartsWith("br-") || nombre.StartsWith("veth")) return false;

        byte[] bytes = direccion.GetAddressBytes();
        if (bytes[0] == 169 && bytes[1] == 254) return false;
        if (bytes[0] == 172 && bytes[1] == 17) return false;
        return true;
    }

    public static bool EsPrivada(IPAddress direccion)
    {
        if (direccion.AddressFamily != AddressFamily.InterNetwork) return false;
        byte[] bytes = direccion.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `./pruebas/probar_direccion_de_red.sh`
Expected: todas `ok`; la línea `info` muestra la IP del WiFi (en la laptop, `192.168.1.7` o la que tenga ese día); salida `Todas las pruebas pasaron.`

- [ ] **Step 5: Commit (con confirmación del usuario)**

```bash
git add Unity/Assets/scripts/DireccionDeRed.cs pruebas/PruebaDireccionDeRed.cs pruebas/probar_direccion_de_red.sh
git commit -m "Detectar la IP local que la tablet usa para conectarse"
git log -1
```

---

### Task 3: Mostrar la IP en la pantalla de espera

**Files:**
- Modify: `Unity/Assets/scripts/UnityWebSocketClient.cs` (agregar propiedad junto a `serverUrl`, línea 12)
- Modify: `Unity/Assets/scripts/PantallaDeEspera.cs` (campos, `Start`, `Update`, `AgregarTexto`)

**Interfaces:**
- Consumes: `DireccionDeRed.ObtenerIpLocal()` (Task 2).
- Produces: `public int UnityWebSocketClient.Puerto` — puerto leído de `serverUrl`.

- [ ] **Step 1: Exponer el puerto en `UnityWebSocketClient`**

Debajo de `private string serverUrl = "ws://127.0.0.1:8000";`:

```csharp
    /// Puerto del relay, leído de la misma URL a la que se conecta el
    /// cliente: así la pantalla de espera muestra siempre el que se usa.
    public int Puerto => new Uri(serverUrl).Port;
```

(`using System;` ya está en el archivo.)

- [ ] **Step 2: Campos nuevos en `PantallaDeEspera`**

Reemplazar:

```csharp
    private GameObject lienzo;
    private bool visible;
```

por:

```csharp
    // Respaldo si en la escena no hay cliente WebSocket del que leer el puerto.
    private const int PuertoPorDefecto = 8000;
    // La PC puede conectarse a la red después de abrir el juego.
    private const float SegundosEntreRevisionesDeIp = 3f;

    private GameObject lienzo;
    private Text textoDeConexion;
    private UnityWebSocketClient clienteWebSocket;
    private float segundosHastaRevisarIp;
    private bool visible;
```

- [ ] **Step 3: `Start` y `Update`**

Reemplazar `Start()` y `Update()` por:

```csharp
    private void Start()
    {
        if (controlador == null) controlador = FindObjectOfType<UnityGameSessionController>();
        clienteWebSocket = FindObjectOfType<UnityWebSocketClient>();
        ConstruirLienzo();
        AplicarVisibilidad(true);
        ActualizarTextoDeConexion();
    }

    private void Update()
    {
        // La partida la inicia la tablet, así que esto puede cambiar en
        // cualquier momento y hay que seguirlo cuadro a cuadro.
        bool deberiaVerse = controlador == null || !controlador.PartidaActiva;
        if (deberiaVerse != visible) AplicarVisibilidad(deberiaVerse);

        if (!visible) return;
        segundosHastaRevisarIp -= Time.deltaTime;
        if (segundosHastaRevisarIp <= 0f) ActualizarTextoDeConexion();
    }

    private void ActualizarTextoDeConexion()
    {
        segundosHastaRevisarIp = SegundosEntreRevisionesDeIp;
        string ip = DireccionDeRed.ObtenerIpLocal();
        int puerto = clienteWebSocket != null ? clienteWebSocket.Puerto : PuertoPorDefecto;
        textoDeConexion.text = ip != null
            ? $"Conecta la tablet a {ip} : {puerto}"
            : "Sin red — conecta la PC al WiFi";
    }
```

- [ ] **Step 4: Dos líneas de texto en vez de una**

Reemplazar el método `AgregarTexto` completo por:

```csharp
    private void AgregarTexto(Transform padre)
    {
        CrearTexto(padre, "TextoDeEspera", textoDeEspera, new Vector2(60f, 100f), 32);
        textoDeConexion = CrearTexto(padre, "TextoDeConexion", string.Empty, new Vector2(60f, 50f), 26);
    }

    private Text CrearTexto(Transform padre, string nombre, string contenido, Vector2 posicion, int tamano)
    {
        GameObject objetoTexto = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        objetoTexto.transform.SetParent(padre, false);

        RectTransform rect = objetoTexto.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = new Vector2(1200f, 60f);

        Text texto = objetoTexto.GetComponent<Text>();
        texto.text = contenido;
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = tamano;
        texto.alignment = TextAnchor.LowerLeft;
        texto.color = new Color(0.85f, 0.85f, 0.8f, 0.9f);
        return texto;
    }
```

- [ ] **Step 5: Verificar compilación**

No hay compilador de Unity sin abrir el editor fuera del export. La compilación se verifica en la Task 4 (el export falla si hay errores de C#). Revisión estática ahora:

Run: `grep -n "textoDeConexion\|ActualizarTextoDeConexion\|CrearTexto\|Puerto" Unity/Assets/scripts/PantallaDeEspera.cs Unity/Assets/scripts/UnityWebSocketClient.cs`
Expected: cada símbolo declarado una vez y usado donde corresponde.

- [ ] **Step 6: Commit (con confirmación del usuario)**

```bash
git add Unity/Assets/scripts/PantallaDeEspera.cs Unity/Assets/scripts/UnityWebSocketClient.cs
git commit -m "Mostrar en la pantalla de espera la IP para conectar la tablet"
git log -1
```

---

### Task 4: `exportar_linux.sh` — generar el ejecutable

**Files:**
- Create: `exportar_linux.sh`

**Interfaces:**
- Produces: `Unity/Builds/Linux/FiveNightsOfTerror.x86_64`, que consume `iniciar.sh` (Task 1).

- [ ] **Step 1: Implementar**

```bash
#!/usr/bin/env bash
# Genera el ejecutable de Linux sin abrir el editor de Unity, usando la escena
# configurada en Build Settings.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNITY="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/2022.3.30f1/Editor/Unity}"
PROYECTO="$RAIZ/Unity"
CARPETA_SALIDA="$PROYECTO/Builds/Linux"
EJECUTABLE="$CARPETA_SALIDA/FiveNightsOfTerror.x86_64"
REGISTRO="$PROYECTO/Builds/exportar_linux.log"

if [ ! -x "$UNITY" ]; then
  echo "No se encontró el editor de Unity en $UNITY."
  echo "Indica la ruta con: UNITY_EDITOR=/ruta/a/Unity ./exportar_linux.sh"
  exit 1
fi

# Unity no permite dos instancias sobre el mismo proyecto.
if pgrep -fi "projectpath.*$PROYECTO" >/dev/null; then
  echo "El editor de Unity está abierto con este proyecto. Ciérralo y vuelve a intentar."
  exit 1
fi

# Se borra el ejecutable anterior para que un export fallido no deje uno viejo
# que parezca recién generado.
rm -rf "$CARPETA_SALIDA"
mkdir -p "$CARPETA_SALIDA"

echo "Exportando... (la primera vez puede tardar varios minutos)"
if ! "$UNITY" -batchmode -quit -projectPath "$PROYECTO" \
     -buildLinux64Player "$EJECUTABLE" -logFile "$REGISTRO"; then
  echo "La exportación falló. Revisa $REGISTRO (busca 'error CS' o 'Build Failed')."
  exit 1
fi

if [ ! -x "$EJECUTABLE" ]; then
  echo "Unity terminó pero no generó $EJECUTABLE. Revisa $REGISTRO."
  exit 1
fi

echo "Listo: $EJECUTABLE"
echo "Para jugar: ./iniciar.sh"
```

Run: `chmod +x exportar_linux.sh`

- [ ] **Step 2: Verificar el chequeo de editor abierto**

Con el editor de Unity abierto en este proyecto:
Run: `./exportar_linux.sh`
Expected: `El editor de Unity está abierto con este proyecto. Ciérralo y vuelve a intentar.`, código 1, sin tocar `Unity/Builds/`.

- [ ] **Step 3: Exportar de verdad**

Con el editor cerrado:
Run: `./exportar_linux.sh`
Expected: `Listo: .../FiveNightsOfTerror.x86_64`. Además: `grep -c "error CS" Unity/Builds/exportar_linux.log` da `0` (confirma que compilan los cambios de la Task 3).

- [ ] **Step 4: Commit (con confirmación del usuario)**

```bash
git add exportar_linux.sh
git commit -m "Agregar script que exporta el juego para Linux sin abrir Unity"
git log -1
```

---

### Task 5: Acceso directo en el Escritorio

**Files:**
- Create: `crear_acceso_directo.sh`

**Interfaces:**
- Consumes: `iniciar.sh` (Task 1).

- [ ] **Step 1: Implementar**

```bash
#!/usr/bin/env bash
# Crea en el Escritorio un acceso directo que lanza iniciar.sh con doble clic.
# Ubuntu no ejecuta un .sh con doble clic, pero sí un .desktop.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ESCRITORIO="$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Escritorio")"
ACCESO="$ESCRITORIO/five-nights-of-terror.desktop"

# Exec va entre comillas porque la ruta del proyecto tiene espacios.
cat > "$ACCESO" <<EOF
[Desktop Entry]
Type=Application
Name=Five Nights of Terror
Comment=Inicia el relay y el juego
Exec="$RAIZ/iniciar.sh"
Path=$RAIZ
Icon=applications-games
Terminal=false
Categories=Game;
EOF
chmod +x "$ACCESO"

# En GNOME marca el acceso como confiable para que no pida "Permitir ejecutar".
gio set "$ACCESO" metadata::trusted true 2>/dev/null || true

echo "Acceso directo creado: $ACCESO"
echo "Si Ubuntu lo muestra con una X, clic derecho → Permitir ejecutar."
```

Run: `chmod +x crear_acceso_directo.sh`

- [ ] **Step 2: Crear y validar**

Run: `./crear_acceso_directo.sh && desktop-file-validate "$(xdg-user-dir DESKTOP)/five-nights-of-terror.desktop"`
Expected: `Acceso directo creado: ...` y ninguna línea de error de `desktop-file-validate` (si no está instalado: `sudo apt install desktop-file-utils`, o omitir este chequeo).

- [ ] **Step 3: Commit (con confirmación del usuario)**

```bash
git add crear_acceso_directo.sh
git commit -m "Agregar script que crea el acceso directo del juego en el Escritorio"
git log -1
```

---

### Task 6: Prueba completa con la tablet y documentación

**Files:**
- Modify: `README.md` (sección nueva "Ejecutar el juego")
- Modify: `PROGRESS.md` (gitignored, no se commitea)

- [ ] **Step 1: Prueba de punta a punta (con el usuario)**

1. Relay apagado: `ss -tlnp | grep 8000` no muestra nada.
2. Doble clic al acceso directo del Escritorio.
3. Se abre el juego a pantalla completa con la imagen de espera, "Esperando..." y "Conecta la tablet a <IP> : 8000" con la IP del WiFi.
4. En la tablet, Opciones → host = esa IP → Nuevo Juego. Se juega una noche (para acortar, bajar temporalmente `SegundosRealesPorNoche[0]`).
5. Cerrar el juego (Alt+F4). `ss -tlnp | grep 8000` vuelve a no mostrar nada.

- [ ] **Step 2: README**

Agregar al `README.md`, antes de la sección de desarrollo:

```markdown
## Ejecutar el juego (Linux)

1. Exporta el juego una vez (con el editor de Unity cerrado):
   `./exportar_linux.sh`
2. Crea el acceso directo: `./crear_acceso_directo.sh`
3. Doble clic en "Five Nights of Terror" en el Escritorio. Se levantan el
   relay y el juego juntos; al cerrar el juego se apaga el relay.
4. En la tablet, Opciones → servidor = la IP que muestra la pantalla de
   espera, puerto 8000.

Sin acceso directo: `./iniciar.sh`. Si el relay ya estaba corriendo a mano,
lo reutiliza y no lo apaga.
```

- [ ] **Step 3: PROGRESS.md**

Agregar una entrada con fecha 2026-10-09 resumiendo las fases 1 y 2, las decisiones (script hoy, relay dentro del juego después; reutilizar un relay existente; IP en pantalla) y el resultado de la prueba del Step 1.

- [ ] **Step 4: Commit (con confirmación del usuario)**

```bash
git add README.md docs/DISENO_EJECUTABLE_UNICO.md docs/PLAN_EJECUTABLE_UNICO.md
git commit -m "Documentar cómo ejecutar el juego con un doble clic"
git log -1
```
