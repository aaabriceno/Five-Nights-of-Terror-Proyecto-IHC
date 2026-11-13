#!/usr/bin/env bash
# Conecta la tablet al relay por el cable USB, sin Wi-Fi.
#
# Para cuándo: redes que aíslan a cada dispositivo (la de la universidad pone a
# la PC y a la tablet en subredes distintas y el punto de acceso bloquea el
# tráfico entre clientes). Con `adb reverse`, el puerto 8000 de la tablet se
# redirige por el cable al puerto 8000 de esta PC: en Opciones de la tablet se
# pone IP 127.0.0.1 y puerto 8000, y listo.
#
# Hace falta: la tablet con «Depuración USB» activada y `adb` en la PC. Hay que
# volver a correrlo cada vez que se desconecta el cable. Solo sirve para la
# tablet: la computadora del jugador 2 sigue necesitando una red común (un
# punto de acceso del celular, por ejemplo).
set -u

PUERTO="${1:-8000}"

if ! command -v adb >/dev/null; then
  echo "No se encontró adb. Instala las platform-tools de Android (sudo apt install adb)." >&2
  exit 1
fi

dispositivos="$(adb devices | awk 'NR > 1 && $2 == "device" { print $1 }')"
if [ -z "$dispositivos" ]; then
  echo "No hay ninguna tablet conectada por USB con la depuración activada." >&2
  echo "Revisa el cable, activa «Depuración USB» en Opciones de desarrollador y acepta el aviso en la tablet." >&2
  adb devices >&2
  exit 1
fi

if ! adb reverse "tcp:$PUERTO" "tcp:$PUERTO"; then
  echo "No se pudo redirigir el puerto $PUERTO." >&2
  exit 1
fi

echo "Listo: el puerto $PUERTO de la tablet llega por el cable al relay de esta PC."
echo "En la tablet: Opciones → IP de la PC 127.0.0.1, puerto $PUERTO, modo de prueba apagado → Guardar."
echo "Después abre el juego con ./iniciar.sh (si no está abierto) y toca «Nuevo juego»."
