import 'aviso_de_companero.dart';
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

  /// Cuántas veces más rápido se descarga la caja de Puppet. Sube cuando el
  /// jugador 2 lo rescata (×2, luego ×4...); Unity lo manda en `estado_puppet`
  /// y la tablet solo lo muestra. 1 = a su ritmo normal.
  double multaDeCaja;

  /// Lo que dura ahora la caja de Puppet llena (30 s al empezar la noche, la
  /// mitad tras cada rescate). null si Unity no lo manda o aún no hubo rescate.
  double? segundosDeCaja;

  /// Lo último que hizo el jugador 2 y que hay que mostrar (retraso, trampa,
  /// rescate...). La pantalla lo quita sola a los pocos segundos.
  AvisoDeCompanero? avisoDeCompanero;

  /// true/false según el último aviso de Unity; null si nunca hubo compañero en
  /// esta partida (entonces no se muestra nada: se juega solo).
  bool? companeroConectado;

  /// Batería de las compuertas de los ductos (0-100), tal cual la manda Unity
  /// en `estado_oficina`. null mientras no llegue: un Unity sin compuertas no
  /// la manda y entonces la tablet no muestra los botones.
  int? bateria;

  /// Si la compuerta de cada ducto está cerrada (Unity decide; la tablet solo
  /// lo pide con `compuerta` y lo refleja).
  bool compuertaIzquierdaCerrada;
  bool compuertaDerechaCerrada;

  /// Si la linterna está prendida según Unity (`estado_oficina.linterna`). Gasta
  /// la misma batería; la tablet la pide mientras el botón sigue apretado.
  bool linternaEncendida;

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
    this.puppetEnPeligro = false,
    this.puppetValorCajaPorcentaje = 100,
    this.multaDeCaja = 1,
    this.segundosDeCaja,
    this.avisoDeCompanero,
    this.companeroConectado,
    this.bateria,
    this.compuertaIzquierdaCerrada = false,
    this.compuertaDerechaCerrada = false,
    this.linternaEncendida = false,
  }) : tareasPendientes = tareasPendientes ?? [];
}
