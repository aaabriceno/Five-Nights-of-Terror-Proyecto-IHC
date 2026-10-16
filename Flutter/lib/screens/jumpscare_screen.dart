import 'dart:async';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'package:video_player/video_player.dart';
import '../providers/game_provider.dart';
import '../utils/animatronicos.dart';
import '../utils/video_de_jumpscare.dart';
import 'game_over_screen.dart';

/// `mixWithOthers`: el video no pide el foco de audio de Android. Sin eso, al
/// sonar `jumpscare.ogg` (otro reproductor) Android pausa el video y se queda
/// en el primer cuadro.
VideoPlayerController crearControladorDeJumpscare(String ruta) =>
    VideoPlayerController.asset(
      ruta,
      videoPlayerOptions: VideoPlayerOptions(mixWithOthers: true),
    );

/// Jumpscare de la tablet: el mismo video que se ve en la PC (uno por
/// animatrónico) con el sonido del jumpscare. Si el animatrónico no tiene
/// video o este no se puede reproducir, queda la imagen fija de siempre.
class JumpscareScreen extends StatefulWidget {
  /// [crearControlador] existe para poder probar la pantalla sin un
  /// reproductor de video real.
  const JumpscareScreen({
    super.key,
    this.crearControlador = crearControladorDeJumpscare,
  });

  final VideoPlayerController Function(String ruta) crearControlador;

  @override
  State<JumpscareScreen> createState() => _JumpscareScreenState();
}

class _JumpscareScreenState extends State<JumpscareScreen> {
  /// Igual que en Unity: dura lo mismo que el sonido del jumpscare.
  static const Duration _duracion = Duration(milliseconds: 1600);
  static const Duration _esperaMaximaDelVideo = Duration(milliseconds: 1500);

  final AudioPlayer _audioPlayer = AudioPlayer();
  Timer? _finishTimer;
  VideoPlayerController? _video;
  bool _videoListo = false;

  /// La imagen fija solo se ve cuando no hay video: así no aparece un
  /// instante mientras el video carga ni cuando termina.
  bool _mostrarImagen = false;

  @override
  void initState() {
    super.initState();
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
    _arrancar();
  }

  Future<void> _arrancar() async {
    final String? quien = context.read<GameProvider>().lastAttack?.animatronic;
    final String? ruta = rutaDeVideoDeJumpscare(quien);

    if (ruta != null) {
      try {
        final VideoPlayerController controlador = widget.crearControlador(ruta);
        _video = controlador;
        await controlador.initialize().timeout(_esperaMaximaDelVideo);
        if (!mounted) return;
        await controlador.setLooping(false);
        // El sonido es el del juego (jumpscare.ogg), no el del video.
        await controlador.setVolume(0);
        setState(() => _videoListo = true);
        unawaited(controlador.play());
      } catch (error) {
        debugPrint('No se pudo reproducir el video del jumpscare: $error');
        await _video?.dispose();
        _video = null;
      }
    }
    if (!mounted) return;
    if (_video == null) setState(() => _mostrarImagen = true);

    _playJumpscare();
    _finishTimer = Timer(_duracion, _showGameOver);
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
    _video?.dispose();
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final String? quien = context.watch<GameProvider>().lastAttack?.animatronic;
    final VideoPlayerController? video = _video;
    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        fit: StackFit.expand,
        children: [
          if (_mostrarImagen)
            const Image(
              image: AssetImage('assets/images/jumpscare.jpg'),
              fit: BoxFit.cover,
            ),
          if (_videoListo && video != null)
            FittedBox(
              fit: BoxFit.cover,
              clipBehavior: Clip.hardEdge,
              child: SizedBox(
                width: video.value.size.width,
                height: video.value.size.height,
                child: VideoPlayer(video),
              ),
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
