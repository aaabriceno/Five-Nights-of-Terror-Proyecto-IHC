# Relay para la integración Unity

`relay.py` retransmite mensajes entre Flutter y Unity sin ejecutar la lógica Python de partida. El servidor/backend original no se edita.

El entorno virtual del backend ya tiene `websockets`. En Windows, con el servidor original detenido, ejecuta:

```powershell
D:\ihc_completo\Five-Nights-of-Terror-Proyecto-IHC-main\Five-Nights-of-Terror-Proyecto-IHC-main\backend\venv\Scripts\python.exe C:\Users\User\Documents\Codex\Poryecto_IHC_integracion\UnityRelay\relay.py
```

El relay escucha en `0.0.0.0:8000`. Unity se conecta a `127.0.0.1`; Flutter usa la IP local de la PC y ese mismo puerto. Desactiva `useMock` en las opciones de Flutter. No inicies a la vez el servidor original porque ambos necesitan el puerto 8000.

## Reglas del relay

- Roles: `unity_connect` (solo desde la misma PC), `connect`/`reconnect` (tablet, una sola por partida) y `operador_connect` (jugador 2). Cada rol puede mandar solo sus tipos de mensaje; lo demás se descarta. Detalle en `docs/PROTOCOLO_JUGADOR2.md`.
- Sin compresión y mensajes de hasta 64 kB.
- Variables: `PING_INTERVALO_S`, `PING_TIMEOUT_S`, `CIERRE_TIMEOUT_S`, `VENTANA_OPERADOR_S`, `UNITY_DESDE_CUALQUIER_IP=1`.
- Pruebas: `UnityRelay/.venv/bin/python pruebas/probar_relay.py`.
