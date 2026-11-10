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
PID_OPERADOR=""
PUERTO_OPERADOR=8081

avisar() {
  echo "$1" >&2
  if [ -z "${SIN_NOTIFICACIONES:-}" ] && command -v notify-send >/dev/null; then
    notify-send "Five Nights of Terror" "$1"
  fi
}

puerto_escuchando() { (exec 3<>"/dev/tcp/127.0.0.1/$PUERTO") 2>/dev/null; }

# El 8000 es el puerto por defecto de varios servidores de desarrollo. Si lo
# ocupa otro programa, el juego abriría igual pero la tablet nunca podría
# conectarse, así que solo se reutiliza si quien escucha es un relay
# (relay.py o el ejecutable empaquetado "relay").
puerto_es_del_relay() {
  local pid
  pid="$(ss -tlnpH "sport = :$PUERTO" 2>/dev/null | grep -o 'pid=[0-9]*' | head -1 | cut -d= -f2)"
  [ -n "$pid" ] && tr '\0' ' ' <"/proc/$pid/cmdline" 2>/dev/null | grep -q "relay"
}

# Solo se apaga lo que este script arrancó: un relay ajeno no se toca.
limpiar() {
  if [ -n "$PID_JUEGO" ] && kill -0 "$PID_JUEGO" 2>/dev/null; then
    kill "$PID_JUEGO" 2>/dev/null
  fi
  if [ -n "$PID_OPERADOR" ] && kill -0 "$PID_OPERADOR" 2>/dev/null; then
    kill "$PID_OPERADOR" 2>/dev/null
  fi
  if [ -n "$PID_RELAY" ] && kill -0 "$PID_RELAY" 2>/dev/null; then
    kill "$PID_RELAY" 2>/dev/null
    wait "$PID_RELAY" 2>/dev/null
  fi
}
trap limpiar EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# El juego tarda unos segundos en aparecer y es fácil volver a hacer doble
# clic: una segunda copia abriría otro juego y, al cerrarse la primera, se
# llevaría el relay del que depende la segunda.
exec 9>"${XDG_RUNTIME_DIR:-/tmp}/five-nights-of-terror.lock"
if ! flock -n 9; then
  avisar "El juego ya se está abriendo o ya está abierto."
  exit 0
fi

if [ ! -x "$JUEGO" ]; then
  avisar "No se encontró el juego exportado en $JUEGO. Expórtalo primero con ./exportar_linux.sh"
  exit 1
fi

if puerto_escuchando; then
  if ! puerto_es_del_relay; then
    avisar "El puerto $PUERTO está ocupado por otro programa y la tablet no podría conectarse. Ciérralo y vuelve a intentar."
    exit 1
  fi
  echo "Ya hay un relay escuchando en el puerto $PUERTO: se usa ese."
else
  if [ ! -x "$PYTHON_RELAY" ]; then
    avisar "No se encontró el Python del relay en $PYTHON_RELAY. Crea el entorno: cd UnityRelay && python3 -m venv .venv && .venv/bin/pip install -r requirements.txt"
    exit 1
  fi
  mkdir -p "$(dirname "$REGISTRO")"
  # 9>&- : los hijos no heredan el candado, que debe liberarse al salir este script.
  "$PYTHON_RELAY" -u "$RAIZ/UnityRelay/relay.py" >>"$REGISTRO" 2>&1 9>&- &
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

# Página del jugador 2 (operador): otra computadora de la misma red la abre en
# su navegador. Si el puerto está ocupado o no hay Python, se sigue sin ella.
if [ -f "$RAIZ/UnityRelay/operador/operador.html" ] && [ -x "$PYTHON_RELAY" ]; then
  if (exec 3<>"/dev/tcp/127.0.0.1/$PUERTO_OPERADOR") 2>/dev/null; then
    echo "El puerto $PUERTO_OPERADOR está ocupado: no se sirve la página del jugador 2."
  else
    "$PYTHON_RELAY" -m http.server "$PUERTO_OPERADOR" --directory "$RAIZ/UnityRelay/operador" >/dev/null 2>&1 9>&- &
    PID_OPERADOR=$!
    DIRECCIONES_OPERADOR=""
    for ip in $(hostname -I 2>/dev/null); do
      case "$ip" in *:*|172.1[78].*) continue ;; esac
      DIRECCIONES_OPERADOR="$DIRECCIONES_OPERADOR http://$ip:$PUERTO_OPERADOR/operador.html"
    done
    # Con doble clic no hay terminal: por eso también va en una notificación.
    [ -n "$DIRECCIONES_OPERADOR" ] && avisar "Jugador 2, abre en la otra computadora:$DIRECCIONES_OPERADOR"
  fi
fi

# En laptops con dos tarjetas gráficas (integrada + NVIDIA) Linux abre el juego
# en la integrada por defecto, y Unity atiende la red una vez por cuadro: con
# pocos cuadros el ping del jugador 2 sube (~40 ms en la integrada, ~8 ms en la
# NVIDIA). Se pide la NVIDIA solo si hay una (SIN_TARJETA_NVIDIA=1 lo desactiva).
# Con la NVIDIA, MediaPipe no logra su contexto de GPU (EGL), así que el propio
# juego pasa la detección de la cara a la CPU (ver FaceLandmarkerRunner.cs);
# en una PC sin NVIDIA no se toca nada y MediaPipe usa la GPU integrada o, si
# no hay tarjeta, la CPU.
if [ -z "${SIN_TARJETA_NVIDIA:-}" ] && command -v nvidia-smi >/dev/null && nvidia-smi -L >/dev/null 2>&1; then
  export __NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia __VK_LAYER_NV_optimus=NVIDIA_only
fi

# En segundo plano + wait: así una señal interrumpe la espera y el trap corre
# enseguida, en vez de esperar a que el juego termine por su cuenta.
"$JUEGO" 9>&- &
PID_JUEGO=$!
wait "$PID_JUEGO"
