import 'task.dart';

class GameSession {
  final String playerId;
  int riesgo;
  int score;
  int tasksCompleted;
  int tasksFailed;
  DateTime? startTime;
  DateTime? endTime;
  bool isConnected;
  Task? currentTask;
  int nocheActual;
  String horaEnJuego;
  List<Task> tareasPendientes;
  bool wifiActivo;

  /// true cuando el servidor avisa que la caja de música de Puppet bajó
  /// del 20% de su valor máximo (o Puppet ya salió de la caja). El
  /// servidor decide el umbral, no Flutter — acá solo se refleja tal
  /// cual llega en `estado_puppet`.
  bool puppetEnPeligro;

  /// Valor 0-100 de la caja de música de Puppet, tal cual lo manda el
  /// servidor en cada tick de `estado_puppet` (100 = llena/segura, 0 =
  /// vacía). Se usa para animar el pastel de `PuppetGameWidget` en
  /// tiempo real.
  int puppetValorCajaPorcentaje;

  GameSession({
    required this.playerId,
    this.riesgo = 0,
    this.score = 0,
    this.tasksCompleted = 0,
    this.tasksFailed = 0,
    this.startTime,
    this.endTime,
    this.isConnected = false,
    this.currentTask,
    this.nocheActual = 1,
    this.horaEnJuego = '12:00 AM',
    List<Task>? tareasPendientes,
    this.wifiActivo = false,
    this.puppetEnPeligro = false,
    this.puppetValorCajaPorcentaje = 100,
  }) : tareasPendientes = tareasPendientes ?? [];
}
