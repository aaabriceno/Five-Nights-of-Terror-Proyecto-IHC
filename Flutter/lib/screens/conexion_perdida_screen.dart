import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/connection_provider.dart';
import '../providers/game_provider.dart';
import '../utils/colors.dart';
import 'menu_principal_screen.dart';

/// La tablet perdió la conexión con el juego a mitad de la noche. El que tiene
/// que sobrevivir es el jugador 1: sin él la noche termina (Unity la cierra y
/// avisa al jugador 2) y no hay forma de volver a ella. Solo queda volver al
/// menú y empezar de nuevo cuando la red esté bien.
class ConexionPerdidaScreen extends StatelessWidget {
  const ConexionPerdidaScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.wifi_off, size: 120, color: AppColors.peligro),
                const SizedBox(height: 24),
                const Text(
                  'SE PERDIÓ LA CONEXIÓN',
                  style: TextStyle(fontSize: 40, fontWeight: FontWeight.bold),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 12),
                const Text(
                  'La tablet se desconectó del juego y la noche terminó.',
                  style: TextStyle(fontSize: 26),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 8),
                const Text(
                  'Revisa que la tablet y la PC sigan en la misma red Wi-Fi y vuelve a empezar desde el menú.',
                  style: TextStyle(
                    fontSize: 22,
                    color: AppColors.textoSecundario,
                  ),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 32),
                ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    minimumSize: const Size(300, 72),
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
                  child: const Text('Volver al menú'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
