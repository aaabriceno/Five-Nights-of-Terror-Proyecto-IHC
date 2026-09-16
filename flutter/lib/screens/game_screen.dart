import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../models/task.dart';
import '../providers/connection_provider.dart';
import '../providers/game_provider.dart';
import '../widgets/status_bar.dart';
import '../widgets/barra_reloj_de_noche.dart';
import '../widgets/caja_de_puppet_widget.dart';
import '../widgets/placeholder_game_widget.dart';
import '../widgets/menu_de_tareas.dart';
import '../widgets/cable_game_widget.dart';
import '../widgets/sequence_game_widget.dart';
import '../widgets/dial_game_widget.dart';
import '../widgets/rhythm_game_widget.dart';
import '../widgets/wifi_game_widget.dart';
import '../widgets/ventiladores_game_widget.dart';
import '../widgets/temperatura_game_widget.dart';
import '../widgets/procesar_datos_game_widget.dart';
import '../widgets/subir_datos_game_widget.dart';
import '../widgets/trazar_curso_game_widget.dart';
import 'game_over_screen.dart';
import 'menu_principal_screen.dart';
import 'pantalla_victoria.dart';

class GameScreen extends StatefulWidget {
  const GameScreen({super.key});

  @override
  State<GameScreen> createState() => _GameScreenState();
}

class _GameScreenState extends State<GameScreen> {
  bool _wired = false;
  StreamSubscription<Map<String, dynamic>>? _messageSubscription;

  @override
  void dispose() {
    _messageSubscription?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final ConnectionProvider connection = context.watch<ConnectionProvider>();
    final GameProvider game = context.watch<GameProvider>();

    if (!_wired) {
      _wired = true;
      game.sendToServer = connection.sender;
      game.mockServidor = connection.mockService;
      _messageSubscription = connection.messages.listen(game.handleMessage);
    }

    if (game.esVictoriaFinal) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        Navigator.of(context).pushReplacement(
          MaterialPageRoute<void>(builder: (_) => const PantallaVictoria()),
        );
      });
    } else if (game.isGameOver) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        Navigator.of(context).pushReplacement(
          MaterialPageRoute<void>(builder: (_) => const GameOverScreen()),
        );
      });
    }

    return Scaffold(
      appBar: AppBar(
        title: StatusBar(
          connectionState: connection.state,
          reconnectAttempts: connection.reconnectAttempts,
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.close),
            tooltip: 'Salir al menú principal',
            onPressed: () => _confirmarSalidaAlMenu(context),
          ),
        ],
      ),
      body: Stack(
        children: [
          Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              children: [
                BarraRelojDeNoche(
                  nocheActual: game.session.nocheActual,
                  horaEnJuego: game.session.horaEnJuego,
                ),
                const SizedBox(height: 16),
                Expanded(
                  child: game.session.currentTask == null
                      ? MenuDeTareas(
                          tareas: game.session.tareasPendientes,
                          alElegirTarea: game.elegirTarea,
                          wifiActivo: game.session.wifiActivo,
                        )
                      : _buildTaskWidget(game, game.session.currentTask!),
                ),
              ],
            ),
          ),
          // Superpuesta sobre todo lo demás: el jugador debe poder
          // sostenerla sin importar qué otra cosa esté resolviendo.
          CajaDePuppetWidget(
            enPeligro: game.session.puppetEnPeligro,
            alEmpezarASostener: game.iniciarDarCuerdaPuppet,
            alSoltar: game.detenerDarCuerdaPuppet,
          ),
        ],
      ),
    );
  }

  /// Pide confirmación antes de abandonar la partida a mitad de noche
  /// (acción destructiva: se pierde el avance de la noche en curso, tal
  /// como en el juego original). Si el jugador confirma, se corta la
  /// conexión — el servidor ya trata cualquier desconexión como "el
  /// jugador se quedó en esta noche" (mismo comportamiento que una caída
  /// de red), así que no hace falta ningún mensaje de protocolo nuevo.
  Future<void> _confirmarSalidaAlMenu(BuildContext context) async {
    final bool? confirmar = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('¿Salir al menú principal?'),
        content: const Text(
          'Perderás el avance de esta noche. Al volver a jugar, '
          'retomarás la última noche guardada desde el principio.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancelar'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Salir'),
          ),
        ],
      ),
    );

    if (confirmar != true || !context.mounted) return;

    context.read<GameProvider>().reset();
    context.read<ConnectionProvider>().disconnect();
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute<void>(builder: (_) => const MenuPrincipalScreen()),
      (route) => false,
    );
  }

  Widget _buildTaskWidget(GameProvider game, Task task) {
    switch (task.taskType) {
      case 'cables':
        return CableGameWidget(
          task: task,
          onComplete: (success, connections) {
            game.reportTaskCompleted(
              success: success,
              timeTaken: task.duration.toDouble(),
              taskData: {'connections': connections},
            );
          },
        );
      case 'sequence':
        return SequenceGameWidget(
          task: task,
          onComplete: (exito, secuenciaUsuario, errores) {
            game.reportTaskCompleted(
              success: exito,
              timeTaken: task.duration.toDouble(),
              taskData: {
                'correct_order': task.params['targets'] ?? [],
                'user_sequence': secuenciaUsuario,
                'errors': errores,
              },
            );
          },
        );
      case 'dials':
        return DialGameWidget(
          task: task,
          onComplete: (exito, diales) {
            game.reportTaskCompleted(
              success: exito,
              timeTaken: task.duration.toDouble(),
              taskData: {'dials': diales},
            );
          },
        );
      case 'rhythm':
        return RhythmGameWidget(
          task: task,
          onComplete: (exito, datosRitmo) {
            game.reportTaskCompleted(
              success: exito,
              timeTaken: task.duration.toDouble(),
              taskData: {'rhythm_data': datosRitmo},
            );
          },
        );
      case 'wifi':
        return WifiGameWidget(
          task: task,
          onComplete: (exito, datos) {
            game.reportTaskCompleted(
              success: exito,
              timeTaken: task.duration.toDouble(),
              taskData: datos,
            );
          },
        );
      case 'ventiladores':
        return VentiladoresGameWidget(
          task: task,
          onComplete: (exito, datos) {
            game.reportTaskCompleted(
              success: exito,
              timeTaken: task.duration.toDouble(),
              taskData: datos,
            );
          },
        );
      case 'temperatura':
        return TemperaturaGameWidget(
          task: task,
          onComplete: (exito, datos) {
            game.reportTaskCompleted(
              success: exito,
              timeTaken: task.duration.toDouble(),
              taskData: datos,
            );
          },
        );
      case 'procesar_datos':
        return ProcesarDatosGameWidget(
          task: task,
          onComplete: (exito, datos) {
            game.reportTaskCompleted(
              success: exito,
              timeTaken: task.duration.toDouble(),
              taskData: datos,
            );
          },
        );
      case 'subir_datos':
        return SubirDatosGameWidget(
          task: task,
          onComplete: (exito, datos) {
            game.reportTaskCompleted(
              success: exito,
              timeTaken: task.duration.toDouble(),
              taskData: datos,
            );
          },
        );
      case 'trazar_curso':
        return TrazarCursoGameWidget(
          task: task,
          onComplete: (exito, datos) {
            game.reportTaskCompleted(
              success: exito,
              timeTaken: task.duration.toDouble(),
              taskData: datos,
            );
          },
        );
      default:
        return PlaceholderGameWidget(
          task: task,
          onComplete: (success) {
            game.reportTaskCompleted(success: success, timeTaken: 10.0);
          },
        );
    }
  }
}
