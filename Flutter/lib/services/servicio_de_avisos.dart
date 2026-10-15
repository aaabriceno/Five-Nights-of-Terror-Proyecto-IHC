import 'dart:async';

import 'package:vibration/vibration.dart';

import '../models/task.dart';
import '../utils/urgencia_de_tareas.dart';

/// El motor de vibración de la tablet. Es una interfaz para poder probar el
/// servicio sin un dispositivo.
abstract class Vibrador {
  Future<bool> get tieneVibrador;

  /// [patron]: espera, vibra, espera, vibra... en milisegundos.
  Future<void> vibrar(List<int> patron);
  Future<void> cancelar();
}

/// Vibrador real (paquete `vibration`). No usa `HapticFeedback` de Flutter
/// porque esa vibración cae en la categoría "toque", que en esta tablet está
/// desactivada en los ajustes del sistema.
class VibradorReal implements Vibrador {
  @override
  Future<bool> get tieneVibrador async {
    try {
      return await Vibration.hasVibrator();
    } catch (_) {
      return false;
    }
  }

  @override
  Future<void> vibrar(List<int> patron) async {
    try {
      await Vibration.vibrate(pattern: patron);
    } catch (_) {}
  }

  @override
  Future<void> cancelar() async {
    try {
      await Vibration.cancel();
    } catch (_) {}
  }
}

/// Avisa por vibración, sin que el jugador tenga que mirar la tablet, de las
/// dos cosas que lo obligan a actuar:
///
/// 1. **La caja de música de Puppet en peligro** (por debajo del 20 %, el
///    umbral que decide Unity): si no se le da cuerda, Puppet sale y atrapa al
///    jugador. Vibra fuerte ("trrr") al entrar en peligro y se repite cada 4 s
///    hasta que se restaure.
/// 2. **Una tarea pendiente que llega a rojo**: un "trin" corto, una vez por
///    tarea.
///
/// No avisa del ámbar ni de los últimos segundos de una tarea (decisión del
/// usuario: demasiados avisos dejan de significar algo).
class ServicioDeAvisos {
  static const Duration repeticionDeLaCaja = Duration(seconds: 4);

  /// "trrr": seis pulsos rápidos (~1.4 s). El motor solo prende y apaga, sin
  /// control de intensidad, así que lo "fuerte" se logra con la duración y la
  /// repetición.
  static const List<int> patronDeLaCaja = <int>[
    0,
    150,
    70,
    150,
    70,
    150,
    70,
    150,
    70,
    150,
    70,
    150,
  ];

  /// "trin": un pulso corto.
  static const List<int> patronDeTareaRoja = <int>[0, 300];

  final Vibrador vibrador;
  final ({bool cajaEnPeligro, List<Task> tareas}) Function()? leerEstado;
  final DateTime Function() _ahora;

  ServicioDeAvisos({
    required this.vibrador,
    this.leerEstado,
    DateTime Function()? ahora,
  }) : _ahora = ahora ?? DateTime.now;

  bool _disponible = false;
  DateTime? _ultimoAvisoDeLaCaja;
  final Set<int> _tareasYaAvisadas = <int>{};
  Timer? _temporizador;

  /// Comprueba si hay motor de vibración y, si se dio [leerEstado], empieza a
  /// revisar cada segundo.
  Future<void> iniciar() async {
    _disponible = await vibrador.tieneVibrador;
    if (_disponible && leerEstado != null) {
      _temporizador = Timer.periodic(const Duration(seconds: 1), (_) {
        final ({bool cajaEnPeligro, List<Task> tareas}) estado = leerEstado!();
        revisar(
          ahora: _ahora(),
          cajaEnPeligro: estado.cajaEnPeligro,
          tareas: estado.tareas,
        );
      });
    }
  }

  void detener() {
    _temporizador?.cancel();
    _temporizador = null;
    if (_ultimoAvisoDeLaCaja != null) {
      _ultimoAvisoDeLaCaja = null;
      vibrador.cancelar();
    }
  }

  void revisar({
    required DateTime ahora,
    required bool cajaEnPeligro,
    required List<Task> tareas,
  }) {
    if (!_disponible) return;

    if (cajaEnPeligro) {
      final DateTime? ultimo = _ultimoAvisoDeLaCaja;
      if (ultimo == null || ahora.difference(ultimo) >= repeticionDeLaCaja) {
        vibrador.vibrar(patronDeLaCaja);
        _ultimoAvisoDeLaCaja = ahora;
      }
      // Con la caja en peligro no se avisa de tareas: otro aviso cortaría el
      // "trrr" y lo urgente es la caja. Las tareas rojas avisarán después.
      return;
    }

    if (_ultimoAvisoDeLaCaja != null) {
      _ultimoAvisoDeLaCaja = null;
      vibrador.cancelar();
    }

    _tareasYaAvisadas.removeWhere(
      (int id) => !tareas.any((Task t) => t.taskId == id),
    );
    bool hayTareaNuevaEnRojo = false;
    for (final Task tarea in tareas) {
      if (nivelDeUrgencia(tarea, ahora) == NivelDeUrgencia.roja &&
          _tareasYaAvisadas.add(tarea.taskId)) {
        hayTareaNuevaEnRojo = true;
      }
    }
    if (hayTareaNuevaEnRojo) vibrador.vibrar(patronDeTareaRoja);
  }
}
