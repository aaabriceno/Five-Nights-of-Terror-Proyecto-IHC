import 'package:flutter/material.dart' hide ConnectionState;
import '../providers/connection_provider.dart';
import '../utils/colors.dart';

class StatusBar extends StatelessWidget {
  final ConnectionState connectionState;
  final int reconnectAttempts;

  const StatusBar({
    super.key,
    required this.connectionState,
    required this.reconnectAttempts,
  });

  String _label() {
    switch (connectionState) {
      case ConnectionState.idle:
        return 'Desconectado';
      case ConnectionState.connecting:
        return 'Conectando...';
      case ConnectionState.connected:
        return 'Conectado';
      case ConnectionState.reconnecting:
        return 'Reconectando ($reconnectAttempts/5)...';
      case ConnectionState.error:
        return 'Error de conexión';
    }
  }

  Color _dotColor() {
    switch (connectionState) {
      case ConnectionState.connected:
        return AppColors.exito;
      case ConnectionState.error:
        return AppColors.peligro;
      default:
        return AppColors.advertencia;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(Icons.circle, size: 10, color: _dotColor()),
        const SizedBox(width: 6),
        Text(_label(), style: const TextStyle(color: AppColors.textoSecundario)),
      ],
    );
  }
}
