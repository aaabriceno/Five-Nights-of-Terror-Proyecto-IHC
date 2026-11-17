import 'dart:async';

import 'package:flutter/foundation.dart';
import '../config/player_identity.dart';
import '../config/server_config.dart';
import '../services/websocket_service.dart';
import '../services/mock_server_service.dart';
import '../utils/constants.dart';
import '../utils/logger.dart';

enum ConnectionState { idle, connecting, connected, error }

class ConnectionProvider extends ChangeNotifier {
  final WebSocketService _wsService;
  final MockServerService _mockService;

  /// Los servicios se pueden inyectar para probar el proveedor sin red.
  ConnectionProvider({
    WebSocketService? wsService,
    MockServerService? mockService,
  }) : _wsService = wsService ?? WebSocketService(),
       _mockService = mockService ?? MockServerService();

  ConnectionState _state = ConnectionState.idle;

  /// true si había conexión con el juego y se cayó (la tablet se quedó sin red,
  /// o el juego o el relay se cerraron). La noche termina ahí: Unity la cierra
  /// y la tablet no puede volver a ella, así que no se reintenta (2026-11-17).
  bool _conexionPerdida = false;
  StreamSubscription<Map<String, dynamic>>? _forwardingSubscription;
  Timer? _temporizadorDeRespuesta;

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

  /// Por qué el relay no dejó entrar a la tablet (`conexion_rechazada`): por
  /// ejemplo `ya_hay_jugador1`, otra tablet está jugando en esa PC. Null si la
  /// conexión no fue rechazada. Se limpia al volver a conectar.
  String? _motivoDeRechazo;

  ConnectionState get state => _state;
  bool get conexionPerdida => _conexionPerdida;
  String? get motivoDeRechazo => _motivoDeRechazo;

  Stream<Map<String, dynamic>> get messages =>
      ServerConfig.useMock ? _mockService.messages : _wsService.messages;

  void Function(Map<String, dynamic>) get sender => ServerConfig.useMock
      ? _mockService.sendTaskCompleted
      : _wsService.sendMessage;

  /// Instancia de `MockServerService` activa, expuesta para que
  /// `GameProvider` pueda notificarle cambios de riesgo directamente.
  /// Devuelve null cuando se usa el WebSocket real
  /// (`ServerConfig.useMock == false`).
  MockServerService? get mockService =>
      ServerConfig.useMock ? _mockService : null;

  /// [modo] es 'nuevo' (empezar desde la noche 1, se guarda así en el
  /// servidor), 'continuar' (retomar la última noche alcanzada según
  /// la base de datos del servidor) o 'tutorial'.
  ///
  /// Con el servidor real el estado queda en `connecting` hasta que llega el
  /// primer mensaje del juego (Unity responde en menos de un segundo). Antes
  /// pasaba a `connected` al instante, así que con una IP equivocada o el juego
  /// cerrado la app decía "Conectado" sin que hubiera nadie del otro lado.
  void connect({String modo = 'nuevo'}) {
    _motivoDeRechazo = null;
    _conexionPerdida = false;
    _state = ConnectionState.connecting;
    notifyListeners();

    _forwardingSubscription?.cancel();
    _temporizadorDeRespuesta?.cancel();

    if (ServerConfig.useMock) {
      appLogger.i('Connecting via MockServerService');
      _forwardingSubscription = _mockService.messages.listen(
        (Map<String, dynamic> mensaje) => onMessage?.call(mensaje),
      );
      // El tutorial no juega ninguna noche: arrancar el mock pondría a
      // correr el reloj y a generar tareas de fondo sin que nadie las vea.
      if (modo != 'tutorial') _mockService.start();
      _state = ConnectionState.connected;
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
      (Map<String, dynamic> mensaje) {
        if (mensaje['type'] == 'conexion_rechazada') {
          _alSerRechazada((mensaje['motivo'] as String?) ?? 'desconocido');
          return;
        }
        _alResponderElJuego();
        onMessage?.call(mensaje);
      },
      onError: (Object error) => _alFallarElSocket(),
      onDone: _alFallarElSocket,
    );

    _wsService.sendMessage({
      'type': 'connect',
      'device': 'tablet',
      'player_id': PlayerIdentity.playerId,
      'app_version': '0.1.0',
      'modo': modo,
      'timestamp': DateTime.now().millisecondsSinceEpoch,
    });
    _temporizadorDeRespuesta = Timer(AppConstants.connectTimeout, () {
      if (_state == ConnectionState.connecting) _fallarLaConexion();
    });
  }

  /// Llegó un mensaje del juego: recién ahí hay alguien del otro lado.
  void _alResponderElJuego() {
    if (_state != ConnectionState.connecting) return;
    _temporizadorDeRespuesta?.cancel();
    _state = ConnectionState.connected;
    notifyListeners();
  }

  /// El relay no dejó entrar a la tablet (otra tablet ya juega en esa PC). No
  /// tiene sentido reintentar solo: la respuesta sería la misma. Se corta y se
  /// le dice al jugador por qué.
  void _alSerRechazada(String motivo) {
    _motivoDeRechazo = motivo;
    _temporizadorDeRespuesta?.cancel();
    _forwardingSubscription?.cancel();
    _forwardingSubscription = null;
    _wsService.disconnect();
    _state = ConnectionState.error;
    notifyListeners();
  }

  /// El socket falló o se cerró. Si ya había conexión, la conexión se perdió
  /// (y con ella la noche); si todavía se estaba conectando, el juego no se
  /// encontró. En los dos casos se avisa y no se reintenta solo.
  void _alFallarElSocket() {
    switch (_state) {
      case ConnectionState.connected:
        _fallarLaConexion(perdida: true);
      case ConnectionState.connecting:
        _fallarLaConexion(perdida: false);
      case ConnectionState.idle:
      case ConnectionState.error:
        break; // aviso atrasado de un socket que ya se descartó
    }
  }

  void _fallarLaConexion({bool perdida = false}) {
    _temporizadorDeRespuesta?.cancel();
    _forwardingSubscription?.cancel();
    _forwardingSubscription = null;
    _wsService.disconnect();
    _conexionPerdida = perdida;
    _state = ConnectionState.error;
    notifyListeners();
  }

  void disconnect() {
    // Avisar antes de cortar: Unity deja la pantalla de resultado ("6:00 AM"
    // o el jumpscare) a la vista hasta recibir esto, porque es el momento en
    // que el jugador volvió al menú de la tablet. Si solo se cerrara el
    // socket, la pantalla de la PC se quedaría mostrando el final.
    if (!ServerConfig.useMock && _state == ConnectionState.connected) {
      _wsService.sendMessage({'type': 'disconnect'});
    }
    _temporizadorDeRespuesta?.cancel();
    _forwardingSubscription?.cancel();
    _forwardingSubscription = null;
    _wsService.disconnect();
    _mockService.stop();
    _conexionPerdida = false;
    _state = ConnectionState.idle;
    notifyListeners();
  }

  @override
  void dispose() {
    _temporizadorDeRespuesta?.cancel();
    _forwardingSubscription?.cancel();
    _wsService.dispose();
    _mockService.dispose();
    super.dispose();
  }
}
