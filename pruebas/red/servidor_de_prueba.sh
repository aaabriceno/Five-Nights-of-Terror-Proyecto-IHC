#!/usr/bin/env bash
# Deja lista la PC del relay para la prueba de red: relay (8000), página de
# prueba (8080) y el "eco". Muestra las IP a las que se puede conectar la otra
# computadora. La laptop se une desde su navegador. Cierra todo con Ctrl+C. Hacerlo con el juego cerrado.
set -u
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PYTHON="${PYTHON_RELAY:-$RAIZ/UnityRelay/.venv/bin/python}"
PIDS=()
trap 'kill "${PIDS[@]}" 2>/dev/null' EXIT

if (exec 3<>/dev/tcp/127.0.0.1/8000) 2>/dev/null; then
  echo "El puerto 8000 ya está en uso (¿relay o juego abiertos?). Ciérralos primero."; exit 1
fi
"$PYTHON" "$RAIZ/UnityRelay/relay.py" & PIDS+=($!)
"$PYTHON" -m http.server 8080 --directory "$RAIZ/pruebas/red" >/dev/null 2>&1 & PIDS+=($!)
sleep 1

# La laptop participa desde su propio navegador (chat y pings); sin pantalla, desde la terminal.
PAGINA_LAPTOP="http://127.0.0.1:8080/prueba_red.html?rol=laptop&host=127.0.0.1"
if [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ] && command -v xdg-open >/dev/null; then
  xdg-open "$PAGINA_LAPTOP" >/dev/null 2>&1 &
  echo "Página de la laptop abierta: $PAGINA_LAPTOP (pulsa Conectar)"
else
  "$PYTHON" "$RAIZ/pruebas/red/prueba_red.py" eco & PIDS+=($!)
fi

echo
echo "Listo. En la OTRA computadora:"
for ip in $(hostname -I | tr ' ' '\n' | grep -E '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' | grep -vE '^(172\.1[78]\.|127\.)'); do
  echo "  navegador:  http://$ip:8080/prueba_red.html"
  echo "  terminal:   python3 prueba_red.py medir --host $ip"
done
echo "Ctrl+C para cerrar."
wait
