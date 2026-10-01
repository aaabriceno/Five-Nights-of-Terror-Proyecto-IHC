import 'package:flutter/material.dart';
import '../config/server_config.dart';

/// Pantalla de "Opciones": permite elegir entre modo simulado (mock,
/// sin backend real) o conectarse a un servidor Python real por IP y
/// puerto en la red local. Los valores se guardan en el dispositivo
/// (ver ServerConfig.guardar) y quedan activos desde la próxima vez que
/// se toque "Iniciar" en el menú principal.
class ConfigurarServidorScreen extends StatefulWidget {
  const ConfigurarServidorScreen({super.key});

  @override
  State<ConfigurarServidorScreen> createState() =>
      _ConfigurarServidorScreenState();
}

class _ConfigurarServidorScreenState extends State<ConfigurarServidorScreen> {
  late bool _useMock;
  late final TextEditingController _controladorHost;
  late final TextEditingController _controladorPuerto;
  String? _errorValidacion;

  @override
  void initState() {
    super.initState();
    _useMock = ServerConfig.useMock;
    _controladorHost = TextEditingController(text: ServerConfig.host);
    _controladorPuerto =
        TextEditingController(text: ServerConfig.port.toString());
  }

  @override
  void dispose() {
    _controladorHost.dispose();
    _controladorPuerto.dispose();
    super.dispose();
  }

  Future<void> _guardar() async {
    if (!_useMock) {
      final String host = _controladorHost.text.trim();
      final int? puerto = int.tryParse(_controladorPuerto.text.trim());
      if (host.isEmpty) {
        setState(() => _errorValidacion = 'Ingresa la IP del servidor.');
        return;
      }
      if (puerto == null || puerto <= 0 || puerto > 65535) {
        setState(() => _errorValidacion = 'Puerto inválido.');
        return;
      }
    }

    setState(() => _errorValidacion = null);
    await ServerConfig.guardar(
      useMock: _useMock,
      host: _controladorHost.text.trim(),
      port: int.tryParse(_controladorPuerto.text.trim()) ?? ServerConfig.port,
    );

    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Configuración guardada.')),
    );
    Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Opciones')),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SwitchListTile(
              title: const Text('Modo simulado (sin servidor real)'),
              subtitle: const Text(
                'Si está apagado, la app intentará conectarse por WiFi a '
                'la IP indicada abajo.',
              ),
              value: _useMock,
              onChanged: (valor) => setState(() => _useMock = valor),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: _controladorHost,
              enabled: !_useMock,
              decoration: const InputDecoration(
                labelText: 'IP del servidor',
                hintText: 'ej. 192.168.1.100',
                border: OutlineInputBorder(),
              ),
              keyboardType: TextInputType.number,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _controladorPuerto,
              enabled: !_useMock,
              decoration: const InputDecoration(
                labelText: 'Puerto',
                border: OutlineInputBorder(),
              ),
              keyboardType: TextInputType.number,
            ),
            if (_errorValidacion != null) ...[
              const SizedBox(height: 8),
              Text(
                _errorValidacion!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: _guardar,
              child: const Text('Guardar'),
            ),
          ],
        ),
      ),
    );
  }
}
