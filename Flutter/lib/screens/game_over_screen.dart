import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/game_provider.dart';
import '../providers/connection_provider.dart';
import 'menu_principal_screen.dart';

class GameOverScreen extends StatelessWidget {
  const GameOverScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final GameProvider game = context.watch<GameProvider>();
    final int? noche = game.ultimaNocheDeGameOver;

    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('JUEGO TERMINADO',
                style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            if (noche != null) Text('Te atraparon en la Noche $noche'),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: () {
                context.read<GameProvider>().reset();
                context.read<ConnectionProvider>().disconnect();
                Navigator.of(context).pushAndRemoveUntil(
                  MaterialPageRoute<void>(
                    builder: (_) => const MenuPrincipalScreen(),
                  ),
                  (route) => false,
                );
              },
              child: const Text('Continuar'),
            ),
          ],
        ),
      ),
    );
  }
}
