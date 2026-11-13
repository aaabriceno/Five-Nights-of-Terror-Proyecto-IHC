import 'package:flutter/material.dart';
import '../config/server_config.dart';
import '../utils/colors.dart';

/// Pantalla de "Opciones": a qué PC se conecta la tablet (IP y puerto que el
/// juego muestra en su pantalla de espera) o, para probar la tablet sola, el
/// modo de prueba sin PC. Los valores se guardan en el dispositivo (ver
/// ServerConfig.guardar) y valen desde el próximo «Nuevo juego».
///
/// Está escrita para quien juega, no para quien programa: textos grandes (se
/// lee en una tablet de 11" apoyada en la mesa), teclado numérico con punto
/// para la IP y la IP se comprueba antes de guardarla, para que el error se
/// vea aquí y no como un «No se encontró el juego» después.
class ConfigurarServidorScreen extends StatefulWidget {
  const ConfigurarServidorScreen({super.key});

  @override
  State<ConfigurarServidorScreen> createState() =>
      _ConfigurarServidorScreenState();
}

class _ConfigurarServidorScreenState extends State<ConfigurarServidorScreen> {
  static const TextStyle _estiloTitulo =
      TextStyle(fontSize: 26, fontWeight: FontWeight.bold);
  static const TextStyle _estiloTexto = TextStyle(fontSize: 22);
  static const TextStyle _estiloAyuda =
      TextStyle(fontSize: 20, color: AppColors.textoSecundario);
  static const TextStyle _estiloCampo = TextStyle(fontSize: 26);

  late bool _useMock;
  late final TextEditingController _controladorHost;
  late final TextEditingController _controladorPuerto;
  String? _errorIp;
  String? _errorPuerto;

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
    final String host = _controladorHost.text.trim();
    final int? puerto = int.tryParse(_controladorPuerto.text.trim());
    final bool puertoValido = puerto != null && puerto >= 1 && puerto <= 65535;

    if (!_useMock) {
      setState(() {
        _errorIp = ServerConfig.esIpValida(host)
            ? null
            : 'Escribe la IP con sus cuatro números, como 192.168.1.100.';
        _errorPuerto = puertoValido ? null : 'El puerto va de 1 a 65535.';
      });
      if (_errorIp != null || _errorPuerto != null) return;
    }

    setState(() {
      _errorIp = null;
      _errorPuerto = null;
    });
    await ServerConfig.guardar(
      useMock: _useMock,
      host: ServerConfig.esIpValida(host) ? host : ServerConfig.host,
      port: puertoValido ? puerto : ServerConfig.port,
    );

    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          _useMock
              ? 'Listo: modo de prueba, se juega sin la PC.'
              : 'Listo: la tablet buscará el juego en ${ServerConfig.direccion}.',
          style: _estiloTexto,
        ),
      ),
    );
    Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        toolbarHeight: 80,
        title: const Text('Opciones', style: TextStyle(fontSize: 30)),
        leading: IconButton(
          iconSize: 44,
          icon: const Icon(Icons.arrow_back),
          tooltip: 'Volver al menú',
          onPressed: () => Navigator.of(context).pop(),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1000),
            child: ListView(
              padding: const EdgeInsets.all(32),
              children: [
                const Text('Conexión con la PC', style: _estiloTitulo),
                const SizedBox(height: 8),
                const Text(
                  'La tablet se conecta al juego de la PC por Wi-Fi. La IP sale en la '
                  'pantalla de la PC al abrir el juego: «Conecta la tablet a …».',
                  style: _estiloAyuda,
                ),
                const SizedBox(height: 20),
                TextField(
                  key: const Key('campo-ip'),
                  controller: _controladorHost,
                  enabled: !_useMock,
                  style: _estiloCampo,
                  decoration: InputDecoration(
                    labelText: 'IP de la PC',
                    labelStyle: _estiloTexto,
                    hintText: '192.168.1.100',
                    hintStyle: _estiloCampo.copyWith(color: AppColors.textoSecundario),
                    errorText: _errorIp,
                    errorStyle: const TextStyle(fontSize: 20),
                    border: const OutlineInputBorder(),
                    contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 22),
                  ),
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                ),
                const SizedBox(height: 16),
                TextField(
                  key: const Key('campo-puerto'),
                  controller: _controladorPuerto,
                  enabled: !_useMock,
                  style: _estiloCampo,
                  decoration: InputDecoration(
                    labelText: 'Puerto (normalmente 8000)',
                    labelStyle: _estiloTexto,
                    errorText: _errorPuerto,
                    errorStyle: const TextStyle(fontSize: 20),
                    border: const OutlineInputBorder(),
                    contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 22),
                  ),
                  keyboardType: TextInputType.number,
                ),
                const SizedBox(height: 28),
                const Text('Probar la tablet sola', style: _estiloTitulo),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Jugar sin la PC (modo de prueba)', style: _estiloTexto),
                  subtitle: const Text(
                    'Las tareas aparecen solas en la tablet, sin juego en la PC. '
                    'Para la partida de verdad, apágalo.',
                    style: _estiloAyuda,
                  ),
                  value: _useMock,
                  onChanged: (valor) => setState(() => _useMock = valor),
                ),
                const SizedBox(height: 28),
                ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    minimumSize: const Size(260, 68),
                    textStyle: const TextStyle(fontSize: 26, fontWeight: FontWeight.bold),
                  ),
                  onPressed: _guardar,
                  icon: const Icon(Icons.check, size: 32),
                  label: const Text('Guardar'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
