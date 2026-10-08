import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/game_provider.dart';
import '../providers/connection_provider.dart';
import 'menu_principal_screen.dart';

/// Se muestra al llegar a las 6 AM, cuando el jugador superó la noche y
/// avanza a la siguiente. No lleva estadísticas: el logro es haber
/// sobrevivido, y un marcador de puntos rompe la tensión que el juego
/// construye durante la noche.
class NocheSuperadaScreen extends StatelessWidget {
  const NocheSuperadaScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final int? noche = context.watch<GameProvider>().ultimaNocheDeGameOver;

    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text(
              '6:00 AM',
              style: TextStyle(fontSize: 48, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 12),
            Text(
              noche != null ? 'Superaste la Noche $noche' : 'Superaste la noche',
              style: const TextStyle(fontSize: 20),
            ),
            const SizedBox(height: 32),
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
