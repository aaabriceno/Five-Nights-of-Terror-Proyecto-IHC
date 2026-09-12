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

    if (ServerConfig.useMock) {
      appLogger.i('Connecting via MockServerService');
      _mockService.start();
      _state = ConnectionState.connected;
      _reconnectAttempts = 0;
      notifyListeners();
      return;
    }

    _wsService.connect(ServerConfig.wsUrl);
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

    _wsService.messages.listen(
      (_) {},
      onError: (Object error) => _handleDisconnect(),
      onDone: _handleDisconnect,
    );
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
    _wsService.disconnect();
    _mockService.stop();
    _state = ConnectionState.idle;
    notifyListeners();
  }

  @override
  void dispose() {
    _wsService.dispose();
    _mockService.dispose();
    super.dispose();
  }
}
