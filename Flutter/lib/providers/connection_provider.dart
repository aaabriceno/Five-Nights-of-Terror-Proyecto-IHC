import 'dart:async';

import 'package:flutter/foundation.dart';
import '../config/player_identity.dart';
import '../config/server_config.dart';
import '../services/websocket_service.dart';
import '../services/mock_server_service.dart';
import '../utils/constants.dart';
import '../utils/logger.dart';

enum ConnectionState { idle, connecting, connected, reconnecting, error }

class ConnectionProvider extends ChangeNotifier {
  final WebSocketService _wsService = WebSocketService();
  final MockServerService _mockService = MockServerService();

  ConnectionState _state = ConnectionState.idle;
  int _reconnectAttempts = 0;
  String _ultimoModo = 'nuevo';
  StreamSubscription<Map<String, dynamic>>? _forwardingSubscription;

  /// Callback asignado por la capa de pantallas (una sola vez, apenas
  /// arranca la app — ver main.dart) para reenviar cada mensaje entrante
  /// a GameProvider.handleMessage en el momento en que llega, sin esperar
  /// a que GameScreen se monte. Necesario porque el stream de mensajes es
  /// un broadcast stream: un listener que se suscribe tarde (ej. recién
  /// al montar GameScreen, después de la navegación Splash->Game) pierde
  /// para siempre cualquier mensaje emitido antes de esa suscripción —
  /// típicamente el task_list inicial, que Unity manda enseguida tras
  /// procesar 'connect' a través del relay.
  void Function(Map<String, dynamic>)? onMessage;

  ConnectionState get state => _state;
  int get reconnectAttempts => _reconnectAttempts;

  Stream<Map<String, dynamic>> get messages =>
      ServerConfig.useMock ? _mockService.messages : _wsService.messages;

  void Function(Map<String, dynamic>) get sender =>
      ServerConfig.useMock ? _mockService.sendTaskCompleted : _wsService.sendMessage;

  /// Instancia de `MockServerService` activa, expuesta para que
  /// `GameProvider` pueda notificarle cambios de riesgo directamente.
  /// Devuelve null cuando se usa el WebSocket real
  /// (`ServerConfig.useMock == false`).
  MockServerService? get mockService =>
      ServerConfig.useMock ? _mockService : null;

  /// [modo] es 'nuevo' (empezar desde la noche 1, se guarda así en el
  /// servidor) o 'continuar' (retomar la última noche alcanzada según
  /// la base de datos del servidor). Se recuerda para que los reintentos
  /// automáticos de reconexión (`_handleDisconnect`) usen el mismo modo.
  void connect({String modo = 'nuevo'}) {
    _ultimoModo = modo;
    _state = ConnectionState.connecting;
    notifyListeners();

    _forwardingSubscription?.cancel();

    if (ServerConfig.useMock) {
      appLogger.i('Connecting via MockServerService');
      _forwardingSubscription = _mockService.messages.listen(
        (Map<String, dynamic> mensaje) => onMessage?.call(mensaje),
      );
      _mockService.start();
      _state = ConnectionState.connected;
      _reconnectAttempts = 0;
      notifyListeners();
      return;
    }

    // Suscribirse ANTES de mandar 'connect': Unity (vía el relay) responde
    // con task_list de inmediato tras recibirlo, y _wsService.messages es
    // un broadcast stream (no bufferea para listeners tardíos) — si nos
    // suscribimos después de sendMessage, esa primera respuesta puede
    // llegar y perderse antes de que este listener exista.
    _wsService.connect(ServerConfig.wsUrl);
    _forwardingSubscription = _wsService.messages.listen(
      (Map<String, dynamic> mensaje) => onMessage?.call(mensaje),
      onError: (Object error) => _handleDisconnect(),
      onDone: _handleDisconnect,
    );

    _wsService.sendMessage({
      'type': 'connect',
      'device': 'tablet',
      'player_id': PlayerIdentity.playerId,
      'app_version': '0.1.0',
      'modo': _ultimoModo,
      'timestamp': DateTime.now().millisecondsSinceEpoch,
    });
    _state = ConnectionState.connected;
    _reconnectAttempts = 0;
    notifyListeners();
  }

  void _handleDisconnect() {
    if (_reconnectAttempts >= AppConstants.reconnectMaxAttempts) {
      _state = ConnectionState.error;
      notifyListeners();
      return;
    }
    _state = ConnectionState.reconnecting;
    _reconnectAttempts++;
    notifyListeners();

    Future<void>.delayed(
      AppConstants.reconnectDelay,
      () => connect(modo: _ultimoModo),
    );
  }

  void disconnect() {
    // Avisar antes de cortar: Unity deja la pantalla de resultado ("6:00 AM"
    // o el jumpscare) a la vista hasta recibir esto, porque es el momento en
    // que el jugador volvió al menú de la tablet. Si solo se cerrara el
    // socket, la pantalla de la PC se quedaría mostrando el final.
    if (!ServerConfig.useMock && _state == ConnectionState.connected) {
      _wsService.sendMessage({'type': 'disconnect'});
    }
    _forwardingSubscription?.cancel();
    _forwardingSubscription = null;
    _wsService.disconnect();
    _mockService.stop();
    _state = ConnectionState.idle;
    notifyListeners();
  }

  @override
  void dispose() {
    _forwardingSubscription?.cancel();
    _wsService.dispose();
    _mockService.dispose();
    super.dispose();
  }
}
