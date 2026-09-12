import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/game_provider.dart';
import '../providers/connection_provider.dart';
import 'splash_screen.dart';

class GameOverScreen extends StatelessWidget {
  const GameOverScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final GameProvider game = context.watch<GameProvider>();
    final session = game.session;
    final int? noche = game.ultimaNocheDeGameOver;

    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('JUEGO TERMINADO',
                style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            if (noche != null) Text('Fallaste en la Noche $noche'),
            const SizedBox(height: 16),
            Text('Tareas completadas: ${session.tasksCompleted}'),
            Text('Tareas fallidas: ${session.tasksFailed}'),
            Text('Puntuación: ${session.score}'),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: () {
                context.read<GameProvider>().reset();
                context.read<ConnectionProvider>().disconnect();
                Navigator.of(context).pushAndRemoveUntil(
                  MaterialPageRoute<void>(
                    builder: (_) => const SplashScreen(modo: 'continuar'),
                  ),
                  (route) => false,
                );
              },
              child: const Text('Reintentar'),
            ),
          ],
        ),
      ),
    );
  }
}
