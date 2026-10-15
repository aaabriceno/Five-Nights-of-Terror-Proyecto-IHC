import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/game_provider.dart';
import '../providers/connection_provider.dart';
import '../utils/animatronicos.dart';
import '../utils/colors.dart';
import 'menu_principal_screen.dart';

class GameOverScreen extends StatelessWidget {
  const GameOverScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final GameProvider game = context.watch<GameProvider>();
    final int? noche = game.ultimaNocheDeGameOver;
    final String? quien = game.lastAttack?.animatronic;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (quien != null)
                  ..._quienTeAtrapo(infoDeAnimatronico(quien), noche)
                else ...[
                  const Text(
                    'JUEGO TERMINADO',
                    style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 8),
                  if (noche != null) Text('Te atraparon en la Noche $noche'),
                ],
                const SizedBox(height: 32),
                ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    minimumSize: const Size(260, 64),
                    textStyle: const TextStyle(
                      fontSize: 26,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
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
        ),
      ),
    );
  }

  /// Quién fue, en grande: icono, nombre y por qué apareció.
  List<Widget> _quienTeAtrapo(InfoDeAnimatronico info, int? noche) {
    return <Widget>[
      Container(
        width: 150,
        height: 150,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: info.color.withValues(alpha: 0.25),
          border: Border.all(color: info.color, width: 6),
        ),
        child: Icon(info.icono, size: 90, color: Colors.white),
      ),
      const SizedBox(height: 24),
      Text(
        'TE ATRAPÓ ${info.nombre}',
        textAlign: TextAlign.center,
        style: const TextStyle(fontSize: 48, fontWeight: FontWeight.bold),
      ),
      const SizedBox(height: 12),
      if (noche != null)
        Text(
          'Noche $noche',
          style: const TextStyle(
            fontSize: 28,
            color: AppColors.textoSecundario,
          ),
        ),
      const SizedBox(height: 16),
      ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 800),
        child: Text(
          info.motivo,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 28),
        ),
      ),
    ];
  }
}
