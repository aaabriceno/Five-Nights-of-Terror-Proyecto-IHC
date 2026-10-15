import 'dart:async';

import 'package:fn_attention_defense_tablet/services/websocket_service.dart';

/// WebSocket de mentira: no abre ninguna red; las pruebas deciden qué llega.
class WebSocketFalso extends WebSocketService {
  final StreamController<Map<String, dynamic>> _entrada =
      StreamController<Map<String, dynamic>>.broadcast();
  final List<Map<String, dynamic>> enviados = <Map<String, dynamic>>[];
  int conexiones = 0;
  int desconexiones = 0;

  @override
  Stream<Map<String, dynamic>> get messages => _entrada.stream;

  @override
  void connect(String url) => conexiones++;

  @override
  void sendMessage(Map<String, dynamic> data) => enviados.add(data);

  @override
  void disconnect() => desconexiones++;

  /// Lo que "manda Unity" (por el relay).
  void llega(Map<String, dynamic> mensaje) => _entrada.add(mensaje);

  /// El socket falla (conexión rechazada, sin ruta...).
  void falla() => _entrada.addError(Exception('sin conexión'));

  /// El servidor cierra el socket.
  void seCierra() => _entrada.close();

  @override
  void dispose() {}
}
