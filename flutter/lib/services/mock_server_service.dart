import 'dart:async';
import 'dart:math';
import '../utils/logger.dart';

/// Simula el comportamiento del backend (eventos task_list / attack /
/// night_status / wifi_status) para poder desarrollar y probar la app
/// tablet antes de que exista el backend real en Python. `night_status`
/// (con `risk_percent`), `task_list` y `wifi_status` son PROPUESTAS de
/// extensión del protocolo (ver
/// docs/superpowers/specs/2026-08-28-sistema-de-noches-design.md,
/// docs/superpowers/specs/2026-09-08-menu-de-tareas-design.md y
/// docs/superpowers/specs/2026-09-09-sistema-de-riesgo-y-tareas-nuevas-design.md)
/// — el backend real todavía no manda esto.
class MockServerService {
  Timer? _taskTimer;
  Timer? _temporizadorRelojDeNoche;
  Timer? _temporizadorRollDeAtaque;
  Timer? _temporizadorWifi;
  int _taskCounter = 0;
  final StreamController<Map<String, dynamic>> _messageController =
      StreamController<Map<String, dynamic>>.broadcast();

  Stream<Map<String, dynamic>> get messages => _messageController.stream;

  static const List<String> _taskTypes = [
    'cables', 'dials', 'sequence', 'rhythm',
    'wifi', 'ventiladores', 'temperatura', 'procesar_datos',
    'subir_datos', 'trazar_curso',
  ];

  // Duración en segundos de cada tipo de tarea, ajustada a la complejidad
  // real de cada minijuego (antes era 25s fijo para todas).
  static const Map<String, int> _duracionPorTipo = {
    'cables': 15,
    'dials': 25,
    'sequence': 20,
    'rhythm': 20,
    'wifi': 10,
    'ventiladores': 15,
    'temperatura': 15,
    'procesar_datos': 15,
    'subir_datos': 12,
    'trazar_curso': 20,
  };

  // Duración de una noche en segundos reales. 360 = 6 minutos (spec real).
  // Se puede acortar temporalmente para pruebas manuales rápidas.
  static const int segundosPorNoche = 360;
  static const int totalNoches = 5;

  // Índice = noche - 1. riesgoInicial: valor de risk_percent al empezar
  // la noche. incrementoPorCheckpoint: cuánto sube risk_percent en cada
  // una de las 5 horas simuladas de la noche (1AM..5AM).
  static const List<Map<String, int>> _tablaRiesgoPorNoche = [
    {'riesgoInicial': 0, 'incrementoPorCheckpoint': 3},
    {'riesgoInicial': 5, 'incrementoPorCheckpoint': 5},
    {'riesgoInicial': 8, 'incrementoPorCheckpoint': 7},
    {'riesgoInicial': 10, 'incrementoPorCheckpoint': 9},
    {'riesgoInicial': 12, 'incrementoPorCheckpoint': 12},
  ];

  int _nocheActual = 1;
  int _segundosTranscurridosEstaNoche = 0;
  int _riesgoActual = 0;
  int _ultimoCheckpointAplicado = 0; // 0..5, cuántos checkpoints ya sumaron
  // Se mantiene solo para posible uso interno futuro del mock (ej.
  // validaciones antes de emitir un mensaje). El estado real del wifi se
  // transmite al Flutter vía el mensaje 'wifi_status' y se lee del lado
  // de GameSession.wifiActivo, no de este campo.
  // ignore: unused_field
  bool _wifiActivo = false;
  final Random _aleatorio = Random();

  void start() {
    appLogger.i('MockServerService started');
    _riesgoActual = _tablaRiesgoPorNoche[_nocheActual - 1]['riesgoInicial']!;
    _ultimoCheckpointAplicado = 0;
    // Se retrasa la lista de tareas porque este es un stream broadcast: si
    // se emite de forma síncrona, se pierde para cualquier listener que se
    // suscriba después (ej. GameScreen, que recién escucha un frame más
    // tarde tras la navegación desde SplashScreen).
    _taskTimer = Timer(const Duration(milliseconds: 300), _emitTaskList);
    _temporizadorRollDeAtaque = Timer.periodic(const Duration(seconds: 5), (_) {
      _intentarAtaque();
    });
    _temporizadorRelojDeNoche = Timer.periodic(const Duration(seconds: 1), (_) {
      _avanzarRelojDeNoche();
    });
  }

  void _emitTaskList() {
    final List<Map<String, dynamic>> tareas = [];
    for (int i = 0; i < 6; i++) {
      _taskCounter++;
      final String type = _taskTypes[i % _taskTypes.length];
      tareas.add({
        'task_id': _taskCounter,
        'task_type': type,
        'duration': _duracionPorTipo[type] ?? 20,
        'description': 'Tarea simulada: $type',
        'difficulty': 1,
        'timestamp': DateTime.now().millisecondsSinceEpoch,
        'task_params': <String, dynamic>{},
      });
    }
    _riesgoActual = (_riesgoActual + tareas.length * 4).clamp(0, 100);
    _messageController.add({
      'type': 'task_list',
      'night': _nocheActual,
      'tasks': tareas,
    });
  }

  void _intentarAtaque() {
    final int roll = _aleatorio.nextInt(100) + 1; // 1..100
    if (roll <= _riesgoActual) {
      _emitAttack();
    }
  }

  void _emitAttack() {
    _messageController.add({
      'type': 'attack',
      'attack_id': 'mock_atk_$_taskCounter',
      'message': 'Un animatrónico te atacó',
      'timestamp': DateTime.now().millisecondsSinceEpoch,
    });
  }

  void _avanzarRelojDeNoche() {
    _segundosTranscurridosEstaNoche++;

    final int checkpointsEsperados =
        (_segundosTranscurridosEstaNoche / (segundosPorNoche / 6)).floor();
    if (checkpointsEsperados > _ultimoCheckpointAplicado &&
        checkpointsEsperados <= 5) {
      final int incremento =
          _tablaRiesgoPorNoche[_nocheActual - 1]['incrementoPorCheckpoint']!;
      _riesgoActual = (_riesgoActual + incremento).clamp(0, 100);
      _ultimoCheckpointAplicado = checkpointsEsperados;
    }

    _messageController.add({
      'type': 'night_status',
      'night': _nocheActual,
      'in_game_time': _formatearHoraEnJuego(_segundosTranscurridosEstaNoche),
      'seconds_elapsed': _segundosTranscurridosEstaNoche,
      'seconds_total': segundosPorNoche,
      'risk_percent': _riesgoActual,
    });

    if (_segundosTranscurridosEstaNoche >= segundosPorNoche) {
      if (_nocheActual >= totalNoches) {
        _emitirVictoriaFinal();
      } else {
        _nocheActual++;
        _segundosTranscurridosEstaNoche = 0;
        _riesgoActual = _tablaRiesgoPorNoche[_nocheActual - 1]['riesgoInicial']!;
        _ultimoCheckpointAplicado = 0;
        _emitTaskList();
      }
    }
  }

  /// Convierte segundos transcurridos (0..segundosPorNoche) a una hora
  /// simulada 12:00 AM -> 6:00 AM, formateada como "H:MM AM".
  String _formatearHoraEnJuego(int segundosTranscurridos) {
    final double fraccion = segundosTranscurridos / segundosPorNoche;
    final int minutosTotalesSimulados = (fraccion * 6 * 60).round();
    int hora = 12 + (minutosTotalesSimulados ~/ 60);
    final int minuto = minutosTotalesSimulados % 60;
    if (hora > 12) hora -= 12;
    final String minutoStr = minuto.toString().padLeft(2, '0');
    return '$hora:$minutoStr AM';
  }

  void _emitirVictoriaFinal() {
    _temporizadorRelojDeNoche?.cancel();
    _temporizadorRollDeAtaque?.cancel();
    _taskTimer?.cancel();
    _messageController.add({
      'type': 'game_over',
      'result': 'final_victory',
      'night': totalNoches,
      'final_stats': <String, dynamic>{},
      'timestamp': DateTime.now().millisecondsSinceEpoch,
    });
  }

  /// Llamado por GameProvider cuando el widget de tarea reporta que
  /// terminó. Ya no dispara automáticamente la siguiente tarea — con el
  /// menú de tareas, es el jugador quien decide cuál sigue, no el mock.
  void sendTaskCompleted(Map<String, dynamic> data) {
    appLogger.i('Mock received task_completed: $data');
  }

  /// Reinicia el reloj de la noche actual (sin cambiar `_nocheActual`),
  /// para cuando el jugador reintenta tras fallar. No reinicia el timer
  /// de ataques/tareas, que siguen corriendo independientemente.
  void reiniciarRelojDeNoche() {
    _segundosTranscurridosEstaNoche = 0;
    _riesgoActual = _tablaRiesgoPorNoche[_nocheActual - 1]['riesgoInicial']!;
    _ultimoCheckpointAplicado = 0;
  }

  /// Llamado por GameProvider cuando el jugador completa una tarea con éxito.
  void notificarTareaCompletada() {
    _riesgoActual = (_riesgoActual - 6).clamp(0, 100);
  }

  /// Llamado por GameProvider cuando el jugador falla una tarea (timeout).
  /// No se resta nada: el riesgo sumado al activarse la tarea queda.
  void notificarTareaFallada() {}

  /// Llamado por GameProvider cuando el jugador completa la tarea 'wifi'
  /// con éxito. Activa el WiFi por 30 segundos reales (wall-clock, no
  /// segundos de juego simulados), tras lo cual vuelve a apagarse (para
  /// que 'subir_datos' vuelva a bloquearse si no se resuelve en ese lapso).
  void activarWifiTemporalmente() {
    _temporizadorWifi?.cancel();
    _wifiActivo = true;
    _messageController.add({'type': 'wifi_status', 'activo': true});
    _temporizadorWifi = Timer(const Duration(seconds: 30), () {
      _wifiActivo = false;
      _messageController.add({'type': 'wifi_status', 'activo': false});
    });
  }

  void stop() {
    _taskTimer?.cancel();
    _temporizadorRollDeAtaque?.cancel();
    _temporizadorRelojDeNoche?.cancel();
    _temporizadorWifi?.cancel();
    _taskCounter = 0;
    _nocheActual = 1;
    _segundosTranscurridosEstaNoche = 0;
    _riesgoActual = _tablaRiesgoPorNoche[_nocheActual - 1]['riesgoInicial']!;
    _ultimoCheckpointAplicado = 0;
    _wifiActivo = false;
  }

  void dispose() {
    stop();
    _messageController.close();
  }
}
