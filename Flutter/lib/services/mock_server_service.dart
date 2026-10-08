import 'dart:async';
import 'dart:math';
import '../utils/logger.dart';

/// Simula el comportamiento del backend (eventos task_list / attack /
/// night_status) para poder desarrollar y probar la app tablet sin levantar
/// Unity ni el relay. `night_status` (con `risk_percent`) y `task_list`
/// fueron PROPUESTAS de extensión del protocolo (ver
/// docs/superpowers/specs/2026-08-28-sistema-de-noches-design.md,
/// docs/superpowers/specs/2026-09-08-menu-de-tareas-design.md y
/// docs/superpowers/specs/2026-09-09-sistema-de-riesgo-y-tareas-nuevas-design.md)
/// — el backend real todavía no manda esto.
class MockServerService {
  Timer? _taskTimer;
  Timer? _temporizadorRelojDeNoche;
  Timer? _temporizadorRollDeAtaque;
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

  // Duración real de cada noche en segundos, espejo de
  // SegundosRealesPorNoche en UnityGameSessionController (2:00, 2:15, 2:30,
  // 2:30, 2:45, 2:45). Si cambia allá, cambiar acá: el mock existe para
  // probar la tablet sin Unity, y sirve de poco si simula otro juego.
  static const List<int> segundosPorNochePorNivel = [120, 135, 150, 150, 165, 165];
  static const int totalNoches = 6;

  int get _segundosDeEstaNoche =>
      segundosPorNochePorNivel[(_nocheActual - 1).clamp(0, segundosPorNochePorNivel.length - 1)];

  // Índice = noche - 1. riesgoInicial: valor de risk_percent al empezar
  // la noche. incrementoPorCheckpoint: cuánto sube risk_percent en cada
  // una de las 5 horas simuladas de la noche (1AM..5AM).
  static const List<Map<String, int>> _tablaRiesgoPorNoche = [
    {'riesgoInicial': 0, 'incrementoPorCheckpoint': 3},
    {'riesgoInicial': 5, 'incrementoPorCheckpoint': 5},
    {'riesgoInicial': 8, 'incrementoPorCheckpoint': 7},
    {'riesgoInicial': 10, 'incrementoPorCheckpoint': 9},
    {'riesgoInicial': 12, 'incrementoPorCheckpoint': 12},
    {'riesgoInicial': 15, 'incrementoPorCheckpoint': 14},
  ];

  int _nocheActual = 1;
  int _segundosTranscurridosEstaNoche = 0;
  int _riesgoActual = 0;
  int _ultimoCheckpointAplicado = 0; // 0..5, cuántos checkpoints ya sumaron
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
        (_segundosTranscurridosEstaNoche / (_segundosDeEstaNoche / 6)).floor();
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
      'seconds_total': _segundosDeEstaNoche,
      'risk_percent': _riesgoActual,
    });

    if (_segundosTranscurridosEstaNoche >= _segundosDeEstaNoche) {
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

  /// Convierte segundos transcurridos en la noche actual a una hora simulada
  /// 12:00 AM -> 6:00 AM, formateada como "H:MM AM".
  String _formatearHoraEnJuego(int segundosTranscurridos) {
    final double fraccion = segundosTranscurridos / _segundosDeEstaNoche;
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
  /// Cantidad de diapositivas que simula el mock. Sirve para probar los
  /// controles del tutorial en la tablet sin levantar Unity ni el relay; el
  /// número real lo informa Unity al contar los archivos de
  /// `Resources/tutorial/`.
  static const int diapositivasSimuladas = 9;

  void sendTaskCompleted(Map<String, dynamic> data) {
    // En modo mock todos los mensajes salientes pasan por acá, así que es el
    // lugar donde responder los del tutorial.
    switch (data['type']) {
      case 'tutorial_abrir':
        _emitirEstadoDeTutorial(0);
        return;
      case 'tutorial_slide':
        _emitirEstadoDeTutorial((data['indice'] as int?) ?? 0);
        return;
      case 'tutorial_cerrar':
        return;
    }
    appLogger.i('Mock received task_completed: $data');
  }

  void _emitirEstadoDeTutorial(int indice) {
    _messageController.add({
      'type': 'tutorial_estado',
      'indice': indice.clamp(0, diapositivasSimuladas - 1),
      'total': diapositivasSimuladas,
    });
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

  void stop() {
    _taskTimer?.cancel();
    _temporizadorRollDeAtaque?.cancel();
    _temporizadorRelojDeNoche?.cancel();
    _taskCounter = 0;
    _nocheActual = 1;
    _segundosTranscurridosEstaNoche = 0;
    _riesgoActual = _tablaRiesgoPorNoche[_nocheActual - 1]['riesgoInicial']!;
    _ultimoCheckpointAplicado = 0;
  }

  void dispose() {
    stop();
    _messageController.close();
  }
}
