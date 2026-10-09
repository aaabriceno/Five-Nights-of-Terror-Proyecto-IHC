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

echo
if [ "$FALLOS" -eq 0 ]; then
  echo "Todas las pruebas pasaron."
else
  echo "$FALLOS prueba(s) fallaron."
  exit 1
fi
