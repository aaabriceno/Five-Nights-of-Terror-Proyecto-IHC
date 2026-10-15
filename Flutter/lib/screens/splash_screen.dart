import 'package:flutter/material.dart' hide ConnectionState;
import 'package:provider/provider.dart';
import '../config/server_config.dart';
import '../providers/connection_provider.dart';
import '../utils/colors.dart';
import 'configurar_servidor_screen.dart';
import 'game_screen.dart';
import 'menu_principal_screen.dart';

/// Pantalla de espera mientras la tablet encuentra el juego de la PC.
///
/// "Conectado" solo cuenta cuando el juego respondió (ver ConnectionProvider).
/// Si no lo encuentra en unos segundos (IP equivocada, juego cerrado, red que no
/// deja pasar), avisa qué revisar y deja reintentar, cambiar la IP o volver al
/// menú: antes se quedaba girando para siempre.
class SplashScreen extends StatefulWidget {
  final String modo;

  const SplashScreen({super.key, this.modo = 'nuevo'});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _conectar());
  }

  void _conectar() {
    if (!mounted) return;
    context.read<ConnectionProvider>().connect(modo: widget.modo);
  }

  Future<void> _irAOpciones() async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => const ConfigurarServidorScreen()),
    );
    // Al volver de Opciones la IP pudo haber cambiado: se prueba de nuevo.
    _conectar();
  }

  void _volverAlMenu() {
    context.read<ConnectionProvider>().disconnect();
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute<void>(builder: (_) => const MenuPrincipalScreen()),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<ConnectionProvider>(
      builder: (context, connectionProvider, _) {
        final ConnectionState estado = connectionProvider.state;
        if (estado == ConnectionState.connected) {
          WidgetsBinding.instance.addPostFrameCallback((_) {
            Navigator.of(context).pushReplacement(
              MaterialPageRoute<void>(builder: (_) => const GameScreen()),
            );
          });
        }
        return Scaffold(
          body: SafeArea(
            child: Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(24),
                child: estado == ConnectionState.error
                    ? _sinConexion()
                    : _conectando(estado),
              ),
            ),
          ),
        );
      },
    );
  }

  Widget _conectando(ConnectionState estado) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Text(
          "Five Nights at Freddy's",
          style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 24),
        const CircularProgressIndicator(),
        const SizedBox(height: 16),
        Text(
          estado == ConnectionState.reconnecting
              ? 'Reconectando...'
              : 'Conectando con la PC...',
          style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w600),
        ),
        const SizedBox(height: 8),
        Text(
          'Buscando el juego en ${ServerConfig.host}:${ServerConfig.port}',
          style: const TextStyle(
            fontSize: 20,
            color: AppColors.textoSecundario,
          ),
        ),
      ],
    );
  }

  Widget _sinConexion() {
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 800),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(
            Icons.portable_wifi_off,
            size: 96,
            color: AppColors.peligro,
          ),
          const SizedBox(height: 16),
          const Text(
            'No se encontró el juego',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 40, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 12),
          Text(
            'Se buscó en ${ServerConfig.host}:${ServerConfig.port}',
            style: const TextStyle(
              fontSize: 22,
              color: AppColors.textoSecundario,
            ),
          ),
          const SizedBox(height: 12),
          const Text(
            'Revisa que el juego esté abierto en la PC y que la IP de Opciones sea la de esa PC.',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 24),
          ),
          const SizedBox(height: 32),
          Wrap(
            alignment: WrapAlignment.center,
            spacing: 16,
            runSpacing: 16,
            children: [
              ElevatedButton(
                style: ElevatedButton.styleFrom(
                  minimumSize: const Size(220, 64),
                  textStyle: const TextStyle(
                    fontSize: 24,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                onPressed: _conectar,
                child: const Text('Reintentar'),
              ),
              OutlinedButton(
                style: OutlinedButton.styleFrom(
                  minimumSize: const Size(220, 64),
                  textStyle: const TextStyle(fontSize: 24),
                ),
                onPressed: _irAOpciones,
                child: const Text('Opciones'),
              ),
              TextButton(
                style: TextButton.styleFrom(
                  minimumSize: const Size(220, 64),
                  textStyle: const TextStyle(fontSize: 24),
                ),
                onPressed: _volverAlMenu,
                child: const Text('Volver al menú'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
