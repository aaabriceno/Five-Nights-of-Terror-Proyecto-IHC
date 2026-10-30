# Prueba de red entre dos computadoras

Mide el viaje de ida y vuelta (RTT), la pérdida y el jitter de un mensaje a través del
`relay.py`, con el mismo canal del juego (WebSocket sobre TCP, puerto 8000).

**Hacerla con el juego y la tablet cerrados**: el "eco" manda un `connect` que Unity leería como inicio de partida.

## PC del relay (la laptop)
```bash
./pruebas/red/servidor_de_prueba.sh
```
Levanta el relay (8000) y la página de prueba (8080), abre en la laptop su propia página (rol **Laptop**) e imprime las direcciones para la otra computadora. En esa ventana pulsa **Conectar**.

## La otra computadora (sin instalar nada)
Abrir `http://<IP-de-la-laptop>:8080/prueba_red.html` (rol **Computadora 2**) → **Conectar**.

Con las dos conectadas:
1. **Chat con acuse:** escribe en una y aparece en la otra; el que envió ve `✓ recibido por <equipo> (X ms)`. Eso demuestra que el mensaje llegó al otro equipo, no solo que el servidor respondió.
2. **Medir latencia:** 200 pings por el relay hacia el otro equipo (RTT, pérdida, jitter).
3. **Descargar registro (.txt):** deja constancia de mensajes, acuses y mediciones, con hora y nombre de cada equipo.

Alternativa por terminal (necesita Python y `pip install websockets`; sin pantalla en la laptop el script usa `eco`, que también chatea):
`python3 prueba_red.py medir --host <IP-de-la-laptop>`

## Si no conecta (en orden)
1. **Misma red**: IP de la otra PC con el mismo comienzo que la de la laptop (p. ej. `192.168.1.x`). En el aula, WiFi y cable pueden ser redes distintas o aisladas entre sí.
2. **Ping**: desde la otra PC, `ping <IP-de-la-laptop>`. Si no responde, es la red (aislamiento de clientes, VLAN), no el juego.
3. **Firewall de la laptop**: `sudo ufw allow 8000/tcp && sudo ufw allow 8080/tcp` (si ufw está activo).
4. **Plan B sin red del aula**: cable Ethernet directo entre las dos PC con IP fija (`192.168.50.1` y `192.168.50.2`, máscara `255.255.255.0`), o el celular como hotspot.

## Qué reportar
Mediana y p95 del RTT, pérdida (%) y jitter; cantidad de pings; tipo de enlace (WiFi/Ethernet) y IP usadas.
Referencia del juego: Unity manda un estado cada 0,5 s (< 1 KB/s).
