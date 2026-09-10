import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/game_provider.dart';
import '../providers/connection_provider.dart';
import 'splash_screen.dart';

/// Se muestra cuando el jugador completa la Noche 5 (GameProvider.esVictoriaFinal).
class PantallaVictoria extends StatelessWidget {
  const PantallaVictoria({super.key});

  @override
  Widget build(BuildContext context) {
    final GameProvider game = context.watch<GameProvider>();
    final session = game.session;

    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('¡SOBREVIVISTE LAS 5 NOCHES!',
                style: TextStyle(fontSize: 26, fontWeight: FontWeight.bold)),
            const SizedBox(height: 16),
            Text('Tareas completadas: ${session.tasksCompleted}'),
            Text('Tareas fallidas: ${session.tasksFailed}'),
            Text('Puntuación final: ${session.score}'),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: () {
                context.read<GameProvider>().reset();
                context.read<ConnectionProvider>().disconnect();
                Navigator.of(context).pushAndRemoveUntil(
                  MaterialPageRoute<void>(builder: (_) => const SplashScreen()),
                  (route) => false,
                );
              },
              child: const Text('Jugar de nuevo'),
            ),
          ],
        ),
      ),
    );
  }
}
