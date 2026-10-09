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
