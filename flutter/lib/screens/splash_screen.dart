import 'package:flutter/material.dart' hide ConnectionState;
import 'package:provider/provider.dart';
import '../providers/connection_provider.dart';
import 'game_screen.dart';

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<ConnectionProvider>().connect();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<ConnectionProvider>(
      builder: (context, connectionProvider, _) {
        if (connectionProvider.state == ConnectionState.connected) {
          WidgetsBinding.instance.addPostFrameCallback((_) {
            Navigator.of(context).pushReplacement(
              MaterialPageRoute<void>(builder: (_) => const GameScreen()),
            );
          });
        }
        return Scaffold(
          body: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text(
                  "Five Nights at Freddy's",
                  style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 24),
                const CircularProgressIndicator(),
                const SizedBox(height: 16),
                Text(_statusText(connectionProvider.state)),
              ],
            ),
          ),
        );
      },
    );
  }

  String _statusText(ConnectionState state) {
    switch (state) {
      case ConnectionState.connecting:
        return 'Conectando con servidor...';
      case ConnectionState.reconnecting:
        return 'Reconectando...';
      case ConnectionState.error:
        return 'No se pudo conectar';
      default:
        return 'Iniciando...';
    }
  }
}
