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
  }) : tareasPendientes = tareasPendientes ?? [];
}
