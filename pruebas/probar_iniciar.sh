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
comprobar "el relay escuchaba al abrir el juego" grep -sqx si "$TEMP/marca1"
comprobar "el puerto queda libre al cerrar" esperar_puerto_libre

echo "Caso 2: ya hay un relay corriendo"
"$RAIZ/UnityRelay/.venv/bin/python" "$RAIZ/UnityRelay/relay.py" >"$TEMP/externo.log" 2>&1 &
PID_EXTERNO=$!
for _ in $(seq 1 20); do puerto_escuchando && break; sleep 0.5; done
MARCA="$TEMP/marca2" EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" REGISTRO_RELAY="$TEMP/relay2.log" \
  "$RAIZ/iniciar.sh" >/dev/null 2>&1
comprobar "usa el relay existente" grep -sqx si "$TEMP/marca2"
comprobar "no apaga el relay ajeno" kill -0 "$PID_EXTERNO"
kill "$PID_EXTERNO" 2>/dev/null
wait "$PID_EXTERNO" 2>/dev/null
esperar_puerto_libre

echo "Caso 3: se interrumpe mientras el juego corre"
MARCA="$TEMP/marca3" DURACION=30 EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" REGISTRO_RELAY="$TEMP/relay3.log" \
  "$RAIZ/iniciar.sh" >/dev/null 2>&1 &
PID_INICIAR=$!
for _ in $(seq 1 20); do [ -f "$TEMP/marca3" ] && break; sleep 0.5; done
kill -TERM "$PID_INICIAR" 2>/dev/null
wait "$PID_INICIAR" 2>/dev/null
comprobar "el puerto queda libre tras interrumpir" esperar_puerto_libre

echo "Caso 4: falta el juego exportado"
EJECUTABLE_JUEGO="$TEMP/no_existe" REGISTRO_RELAY="$TEMP/relay4.log" \
  "$RAIZ/iniciar.sh" >"$TEMP/salida4.txt" 2>&1
codigo=$?
comprobar "termina con error" test "$codigo" -ne 0
comprobar "indica cómo exportarlo" grep -sq "exportar_linux.sh" "$TEMP/salida4.txt"
comprobar "no deja el relay corriendo" esperar_puerto_libre

echo "Caso 5: el relay muere al arrancar"
PYTHON_RELAY=/bin/false MARCA="$TEMP/marca5" EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" \
  REGISTRO_RELAY="$TEMP/relay5.log" "$RAIZ/iniciar.sh" >"$TEMP/salida5.txt" 2>&1
codigo=$?
comprobar "termina con error" test "$codigo" -ne 0
comprobar "no abre el juego" test ! -f "$TEMP/marca5"
comprobar "indica dónde mirar" grep -sq "relay" "$TEMP/salida5.txt"

echo "Caso 6: el puerto lo ocupa otro programa"
python3 -m http.server "$PUERTO" --bind 127.0.0.1 >"$TEMP/otro.log" 2>&1 &
PID_OTRO=$!
for _ in $(seq 1 20); do puerto_escuchando && break; sleep 0.5; done
MARCA="$TEMP/marca6" EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" REGISTRO_RELAY="$TEMP/relay6.log" \
  "$RAIZ/iniciar.sh" >"$TEMP/salida6.txt" 2>&1
codigo=$?
comprobar "termina con error" test "$codigo" -ne 0
comprobar "no abre el juego" test ! -f "$TEMP/marca6"
comprobar "avisa que el puerto es de otro programa" grep -sq "otro programa" "$TEMP/salida6.txt"
comprobar "no apaga el programa ajeno" kill -0 "$PID_OTRO"
kill "$PID_OTRO" 2>/dev/null
wait "$PID_OTRO" 2>/dev/null
esperar_puerto_libre

echo "Caso 7: se lanza dos veces seguidas"
MARCA="$TEMP/marca7a" DURACION=5 EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" REGISTRO_RELAY="$TEMP/relay7.log" \
  "$RAIZ/iniciar.sh" >/dev/null 2>&1 &
PID_PRIMERO=$!
for _ in $(seq 1 20); do [ -f "$TEMP/marca7a" ] && break; sleep 0.5; done
MARCA="$TEMP/marca7b" EJECUTABLE_JUEGO="$TEMP/juego_falso.sh" REGISTRO_RELAY="$TEMP/relay7b.log" \
  "$RAIZ/iniciar.sh" >"$TEMP/salida7.txt" 2>&1
comprobar "el segundo no abre otro juego" test ! -f "$TEMP/marca7b"
comprobar "el segundo avisa que ya se está abriendo" grep -sq "ya se está abriendo" "$TEMP/salida7.txt"
wait "$PID_PRIMERO" 2>/dev/null
comprobar "el puerto queda libre al cerrar el primero" esperar_puerto_libre

echo "Caso 8: laptop con dos tarjetas gráficas (el juego debe abrirse en la NVIDIA)"
# Un juego y un nvidia-smi falsos: el juego anota con qué variables lo abrió el script.
cat > "$TEMP/juego_entorno.sh" <<'FIN'
#!/usr/bin/env bash
echo "${__NV_PRIME_RENDER_OFFLOAD:-no}/${__GLX_VENDOR_LIBRARY_NAME:-no}" > "$MARCA"
FIN
chmod +x "$TEMP/juego_entorno.sh"
mkdir -p "$TEMP/bin_nvidia"
printf '#!/usr/bin/env bash\necho "GPU 0: NVIDIA falsa"\n' > "$TEMP/bin_nvidia/nvidia-smi"
chmod +x "$TEMP/bin_nvidia/nvidia-smi"
PATH="$TEMP/bin_nvidia:$PATH" MARCA="$TEMP/marca8a" EJECUTABLE_JUEGO="$TEMP/juego_entorno.sh" \
  REGISTRO_RELAY="$TEMP/relay8a.log" "$RAIZ/iniciar.sh" >/dev/null 2>&1
comprobar "con una NVIDIA presente, el juego se abre con el render offload (sin esto usa la integrada y va a ~20 cuadros/s)" \
  grep -sqx "1/nvidia" "$TEMP/marca8a"
esperar_puerto_libre
PATH="$TEMP/bin_nvidia:$PATH" SIN_TARJETA_NVIDIA=1 MARCA="$TEMP/marca8b" EJECUTABLE_JUEGO="$TEMP/juego_entorno.sh" \
  REGISTRO_RELAY="$TEMP/relay8b.log" "$RAIZ/iniciar.sh" >/dev/null 2>&1
comprobar "con SIN_TARJETA_NVIDIA=1 no se toca nada" grep -sqx "no/no" "$TEMP/marca8b"
esperar_puerto_libre

echo "Caso 9: la página del jugador 2 se sirve sin caché (si no, el navegador sigue con la versión vieja)"
# Un juego falso que, mientras corre, le pide al servidor de la página sus cabeceras.
cat > "$TEMP/juego_pagina.sh" <<'FIN'
#!/usr/bin/env bash
for _ in $(seq 1 20); do
  curl -sI "http://127.0.0.1:8081/operador.js" > "$MARCA" 2>/dev/null && grep -q "200" "$MARCA" && break
  sleep 0.3
done
FIN
chmod +x "$TEMP/juego_pagina.sh"
MARCA="$TEMP/marca9" EJECUTABLE_JUEGO="$TEMP/juego_pagina.sh" REGISTRO_RELAY="$TEMP/relay9.log" "$RAIZ/iniciar.sh" >/dev/null 2>&1
comprobar "operador.js llega con Cache-Control: no-store" grep -qi "^cache-control:.*no-store" "$TEMP/marca9"
esperar_puerto_libre

echo
if [ "$FALLOS" -eq 0 ]; then
  echo "Todas las pruebas pasaron."
else
  echo "$FALLOS prueba(s) fallaron."
  exit 1
fi
