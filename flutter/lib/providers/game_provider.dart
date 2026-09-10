import 'package:flutter/foundation.dart';
import '../models/attack.dart';
import '../models/game_session.dart';
import '../models/task.dart';
import '../services/mock_server_service.dart';
import '../utils/logger.dart';

class GameProvider extends ChangeNotifier {
  GameSession session = GameSession(playerId: 'player_1');
  Attack? lastAttack;
  bool isGameOver = false;
  bool esVictoriaFinal = false;
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

  void handleMessage(Map<String, dynamic> message) {
    final String type = message['type'] as String;
    switch (type) {
      case 'new_task':
        session.currentTask = Task.fromJson(message);
        break;
      case 'task_list':
        final List<dynamic> tareasJson = message['tasks'] as List<dynamic>;
        session.tareasPendientes = tareasJson
            .map((dynamic tareaJson) =>
                Task.fromJson(tareaJson as Map<String, dynamic>))
            .toList();
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
      case 'wifi_status':
        session.wifiActivo = message['activo'] as bool;
        break;
      case 'game_over':
        ultimaNocheDeGameOver = (message['night'] as int?) ?? session.nocheActual;
        if (message['result'] == 'final_victory') {
          esVictoriaFinal = true;
        } else {
          isGameOver = true;
        }
        break;
      default:
        appLogger.w('Unhandled message type: $type');
    }
    notifyListeners();
  }

  /// Se llama cuando el jugador toca una tarjeta del menú de tareas para
  /// empezar a resolverla.
  void elegirTarea(Task tarea) {
    session.currentTask = tarea;
    notifyListeners();
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
      if (task.taskType == 'wifi') {
        mockServidor?.activarWifiTemporalmente();
      }
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
    ultimaNocheDeGameOver = null;
    notifyListeners();
  }

  /// Reinicia riesgo/tareas/tarea actual para reintentar la MISMA noche,
  /// sin tocar `nocheActual` ni `horaEnJuego`. Se usa cuando el jugador
  /// sufre un ataque (fatal) a mitad de la noche. La lista de tareas
  /// pendientes NO se reinicia — las tareas de la noche siguen vigentes
  /// a través de un reintento por ataque.
  void reiniciarNoche() {
    session.riesgo = 0;
    session.tasksCompleted = 0;
    session.tasksFailed = 0;
    session.currentTask = null;
    lastAttack = null;
    isGameOver = false;
    ultimaNocheDeGameOver = null;
    notifyListeners();
  }
}
