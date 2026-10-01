# Relay para la integración Unity

`relay.py` retransmite mensajes entre Flutter y Unity sin ejecutar la lógica Python de partida. El servidor/backend original no se edita.

El entorno virtual del backend ya tiene `websockets`. En Windows, con el servidor original detenido, ejecuta:

```powershell
D:\ihc_completo\Five-Nights-of-Terror-Proyecto-IHC-main\Five-Nights-of-Terror-Proyecto-IHC-main\backend\venv\Scripts\python.exe C:\Users\User\Documents\Codex\Poryecto_IHC_integracion\UnityRelay\relay.py
```

El relay escucha en `0.0.0.0:8000`. Unity se conecta a `127.0.0.1`; Flutter usa la IP local de la PC y ese mismo puerto. Desactiva `useMock` en las opciones de Flutter. No inicies a la vez el servidor original porque ambos necesitan el puerto 8000.
