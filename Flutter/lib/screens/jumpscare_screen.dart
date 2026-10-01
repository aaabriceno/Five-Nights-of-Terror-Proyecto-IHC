import 'dart:async';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
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
    return const Scaffold(
      backgroundColor: Colors.black,
      body: SizedBox.expand(
        child: Image(
          image: AssetImage('assets/images/jumpscare.jpg'),
          fit: BoxFit.cover,
        ),
      ),
    );
  }
}
