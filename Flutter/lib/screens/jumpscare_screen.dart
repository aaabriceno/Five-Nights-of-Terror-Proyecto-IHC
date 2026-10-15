import 'dart:async';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import '../providers/game_provider.dart';
import '../utils/animatronicos.dart';
import 'game_over_screen.dart';

class JumpscareScreen extends StatefulWidget {
  const JumpscareScreen({super.key});

  @override
  State<JumpscareScreen> createState() => _JumpscareScreenState();
}

class _JumpscareScreenState extends State<JumpscareScreen> {
  final AudioPlayer _audioPlayer = AudioPlayer();
  Timer? _finishTimer;

  @override
  void initState() {
    super.initState();
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
    _playJumpscare();
    _finishTimer = Timer(const Duration(milliseconds: 2500), _showGameOver);
  }

  Future<void> _playJumpscare() async {
    try {
      await _audioPlayer.play(AssetSource('sounds/jumpscare.ogg'));
    } catch (error) {
      debugPrint('No se pudo reproducir el sonido del jumpscare: $error');
    }
  }

  void _showGameOver() {
    if (!mounted) return;
    Navigator.of(context).pushReplacement(
      MaterialPageRoute<void>(builder: (_) => const GameOverScreen()),
    );
  }

  @override
  void dispose() {
    _finishTimer?.cancel();
    _audioPlayer.dispose();
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final String? quien = context.watch<GameProvider>().lastAttack?.animatronic;
    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        fit: StackFit.expand,
        children: [
          const Image(
            image: AssetImage('assets/images/jumpscare.jpg'),
            fit: BoxFit.cover,
          ),
          // El nombre sobre la imagen: así el jugador asocia el susto con quién
          // lo causó, y sigue visible después en la pantalla de juego terminado.
          if (quien != null)
            Align(
              alignment: Alignment.bottomCenter,
              child: Padding(
                padding: const EdgeInsets.only(bottom: 64),
                child: Text(
                  infoDeAnimatronico(quien).nombre,
                  style: const TextStyle(
                    fontSize: 96,
                    fontWeight: FontWeight.bold,
                    color: Colors.white,
                    letterSpacing: 8,
                    shadows: [Shadow(blurRadius: 24, color: Colors.black)],
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
