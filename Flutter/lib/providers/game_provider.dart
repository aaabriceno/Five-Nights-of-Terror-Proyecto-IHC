import 'package:flutter/foundation.dart';
import '../config/player_identity.dart';
import '../models/attack.dart';
import '../models/aviso_de_companero.dart';
import '../models/game_session.dart';
import '../models/task.dart';
import '../services/mock_server_service.dart';
import '../utils/logger.dart';

class GameProvider extends ChangeNotifier {
  GameSession session = GameSession(playerId: PlayerIdentity.playerId);
  Attack? lastAttack;
  bool isGameOver = false;
  bool esVictoriaFinal = false;

  /// El jugador llegó a las 6 AM y pasó a la noche siguiente. Es distinto de
  /// `isGameOver` (lo atraparon) y de `esVictoriaFinal` (superó las seis).
  bool esNocheSuperada = false;
  int? ultimaNocheDeGameOver;

  /// Asignado por la capa de pantallas al `sender` del provider activo
  /// (ver ConnectionProvider.sender) para que este provider pueda
  /// reportar resultados de tareas sin depender directamente de
  /// ConnectionProvider.
  void Function(Map<String, dynamic>)? sendToServer;

  /// Asignado por la capa de pantallas cuando se usa MockServerService
  /// (ServerConfig.useMock == true), para poder notificarle cambios de
  /// riesgo directamente. Queda null cuando se usa el WebSocket real —
  /// ahí el riesgo llega completo vía night_status, sin necesidad de
  /// notificar nada desde el cliente.
  MockServerService? mockServidor;

  /// Asignado por GameScreen para vibrar cuando el compañero hace algo. Se
  /// llama una vez por cada `efecto` que se entiende.
  void Function(AvisoDeCompanero)? alAvisoDeCompanero;

  /// Un mensaje roto (un campo que falta o viene con otro tipo) se ignora y se
  /// anota: no puede tumbar la partida ni dejar la sesión a medias. Las
  /// pantallas solo se avisan cuando el mensaje se entendió entero.
  void handleMessage(Map<String, dynamic> message) {
    final Object? type = message['type'];
    if (type is! String) {
      appLogger.w('Mensaje sin "type" válido: $message');
      return;
    }
    try {
      _aplicarMensaje(type, message);
    } catch (error) {
      appLogger.w('Mensaje "$type" mal formado, se ignora ($error): $message');
      return;
    }
    notifyListeners();
  }

  void _aplicarMensaje(String type, Map<String, dynamic> message) {
    switch (type) {
      case 'new_task':
        session.currentTask = Task.fromJson(message);
        break;
      case 'task_list':
        final List<dynamic> tareasJson = message['tasks'] as List<dynamic>;
        // Se arma la lista entera antes de reemplazar la actual: si una tarea
        // viene rota, la lista anterior se queda como estaba.
        final List<Task> tareas = tareasJson
            .map((dynamic tareaJson) =>
                Task.fromJson(tareaJson as Map<String, dynamic>))
            .toList();
        session.tareasPendientes = tareas;
        break;
      case 'attack':
        final Attack attack = Attack.fromJson(message);
        lastAttack = attack;
        ultimaNocheDeGameOver = session.nocheActual;
        isGameOver = true;
        break;
      case 'night_status':
        session.nocheActual = message['night'] as int;
        session.horaEnJuego = message['in_game_time'] as String;
        session.riesgo = (message['risk_percent'] as int).clamp(0, 100);
        break;
      case 'estado_puppet':
        session.puppetEnPeligro = message['en_peligro'] as bool;
        session.puppetValorCajaPorcentaje =
            (message['valor_caja_porcentaje'] as int).clamp(0, 100);
        // Un Unity sin multa no manda el campo: la caja va a su ritmo.
        session.multaDeCaja =
            (((message['multa_factor'] as num?) ?? 1).toDouble()).clamp(1.0, 1000.0);
        final double segundosCaja = ((message['segundos_caja'] as num?) ?? 0).toDouble();
        session.segundosDeCaja = segundosCaja > 0 ? segundosCaja : null;
        break;
      case 'efecto':
        _recibirEfecto(message);
        break;
      case 'game_over':
        ultimaNocheDeGameOver = (message['night'] as int?) ?? session.nocheActual;
        switch (message['result']) {
          case 'final_victory':
            esVictoriaFinal = true;
          case 'win':
            esNocheSuperada = true;
          default:
            isGameOver = true;
        }
        break;
      default:
        appLogger.w('Unhandled message type: $type');
    }
  }

  /// Lo que hace el jugador 2 (retrasar, trampa, rescate) o su presencia
  /// (entra, se cae): Unity lo manda como `efecto` y aquí se vuelve un aviso
  /// para el jugador 1. El que no se entiende ni trae texto se ignora.
  void _recibirEfecto(Map<String, dynamic> message) {
    final AvisoDeCompanero? aviso = AvisoDeCompanero.deEfecto(message);
    if (aviso == null) {
      appLogger.w('Efecto sin tipo conocido ni texto: $message');
      return;
    }
    session.avisoDeCompanero = aviso;
    switch (aviso.tipo) {
      case 'operador_conectado':
        session.companeroConectado = true;
      case 'operador_perdido':
        session.companeroConectado = false;
      case 'puppet_rescatado':
        if (aviso.multaDeCaja > 1) session.multaDeCaja = aviso.multaDeCaja;
        if (aviso.segundosDeCaja > 0) session.segundosDeCaja = aviso.segundosDeCaja;
    }
    alAvisoDeCompanero?.call(aviso);
  }

  /// Se llama cuando el jugador toca una tarjeta del menú de tareas para
  /// empezar a resolverla.
  void elegirTarea(Task tarea) {
    session.currentTask = tarea;
    notifyListeners();
  }

  /// El jugador cierra la tarea abierta sin resolverla. La tarea sigue en
  /// `tareasPendientes` (nunca se quitó de ahí al elegirla), así que puede
  /// retomarse después; no se avisa al servidor ni cuenta como fallo — el
  /// castigo por dejarla pendiente ya lo aplica el animatrónico dueño, que
  /// acelera según la antigüedad de la tarea.
  void volverAlMenuDeTareas() {
    if (session.currentTask == null) return;
    session.currentTask = null;
    notifyListeners();
  }

  /// El jugador empezó a sostener el control de la caja de música de
  /// Puppet. Se manda una sola vez al iniciar el gesto, no en cada
  /// frame — el servidor recarga la caja mientras no reciba
  /// `dar_cuerda_fin`.
  void iniciarDarCuerdaPuppet() {
    sendToServer?.call({'type': 'dar_cuerda_inicio'});
  }

  /// El jugador soltó el control de la caja de música de Puppet.
  void detenerDarCuerdaPuppet() {
    sendToServer?.call({'type': 'dar_cuerda_fin'});
  }

  /// El jugador mantiene apretado un botón de la cámara (-1 izquierda, 1
  /// derecha) o lo suelta (0). Unity gira la cámara del juego mientras lleguen
  /// órdenes; no hay nada que guardar aquí, la tablet solo avisa.
  void mirarHacia(int direccion) {
    sendToServer?.call({'type': 'camara', 'direccion': direccion});
  }

  void reportTaskCompleted({
    required bool success,
    required double timeTaken,
    Map<String, dynamic>? taskData,
  }) {
    if (session.currentTask == null) return;
    final Task task = session.currentTask!;

    if (success) {
      session.tasksCompleted++;
      session.score += 100;
      session.tareasPendientes.removeWhere(
        (Task tarea) => tarea.taskId == task.taskId,
      );
      mockServidor?.notificarTareaCompletada();
    } else {
      session.tasksFailed++;
      mockServidor?.notificarTareaFallada();
    }

    sendToServer?.call({
      'type': 'task_completed',
      'task_id': task.taskId,
      'task_type': task.taskType,
      'success': success,
      'time_taken': timeTaken,
      'attempts': 1,
      'timestamp': DateTime.now().millisecondsSinceEpoch,
      if (taskData != null) 'task_data': taskData,
    });

    session.currentTask = null;
    notifyListeners();
  }

  void reset() {
    session = GameSession(playerId: session.playerId);
    lastAttack = null;
    isGameOver = false;
    esVictoriaFinal = false;
    esNocheSuperada = false;
    ultimaNocheDeGameOver = null;
    notifyListeners();
  }
}
