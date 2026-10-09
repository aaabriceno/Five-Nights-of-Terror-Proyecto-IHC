import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/connection_provider.dart';
import '../models/task.dart';
import '../utils/colors.dart';
import '../widgets/cable_game_widget.dart';
import 'menu_principal_screen.dart';

/// Mando a distancia del tutorial. El contenido se ve en la pantalla de la
/// PC; acá sólo están los controles para pasar las diapositivas.
///
/// Esa división no es un rodeo técnico: es la misma que usa el juego, así
/// que el tutorial enseña a repartir la atención entre tablet y pantalla por
/// el solo hecho de usarse.
///
/// El total de diapositivas lo informa Unity (mensaje `tutorial_estado`), no
/// está escrito acá: agregar una diapositiva es poner el archivo en
/// `Resources/tutorial/`, sin tocar la app.
class TutorialScreen extends StatefulWidget {
  const TutorialScreen({super.key});

  @override
  State<TutorialScreen> createState() => _TutorialScreenState();
}

class _TutorialScreenState extends State<TutorialScreen> {
  int _indice = 0;
  int _total = 0;
  bool _conectado = false;
  bool _mirandoCamara = false;
  bool _miradaCompleta = false;
  bool _tareaCompleta = false;
  bool _camaraDisponible = true;
  bool _tareaFallida = false;
  int _intento = 0;

  /// `main.dart` deja `onMessage` apuntando a GameProvider una sola vez al
  /// arrancar la app. Mientras dura el tutorial lo desviamos hacia acá, y
  /// hay que devolverlo al salir: si no, al volver al menú el juego queda
  /// sin recibir mensajes hasta reiniciar la aplicación.
  void Function(Map<String, dynamic>)? _manejadorAnterior;

  /// Guardado en initState porque en dispose() el contexto ya no sirve para
  /// buscar providers.
  ConnectionProvider? _conexion;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _abrirTutorial());
  }

  @override
  void dispose() {
    if (_conexion != null && _conexion!.onMessage == _alRecibirMensaje) {
      _conexion!.onMessage = _manejadorAnterior;
    }
    super.dispose();
  }

  void _abrirTutorial() {
    final ConnectionProvider conexion = context.read<ConnectionProvider>();
    _conexion = conexion;
    // El modo 'tutorial' hace que Unity no arranque ninguna noche: el relay
    // igual necesita el `connect` para asignarnos el rol de tablet.
    conexion.connect(modo: 'tutorial');
    _manejadorAnterior = conexion.onMessage;
    conexion.onMessage = _alRecibirMensaje;
    // Explícito además del modo del `connect`: así el tutorial también
    // funciona contra el mock, que no interpreta ese campo.
    conexion.sender({'type': 'tutorial_abrir'});
    setState(() => _conectado = true);
  }

  void _alRecibirMensaje(Map<String, dynamic> mensaje) {
    if (!mounted) return;
    if (mensaje['type'] == 'tutorial_prueba_estado') {
      setState(() {
        _mirandoCamara = mensaje['mirando'] == true;
        _miradaCompleta = mensaje['mirada_completada'] == true;
        _tareaCompleta = _tareaCompleta || mensaje['tarea_completada'] == true;
        _camaraDisponible = mensaje['camara_disponible'] != false;
      });
      return;
    }
    if (mensaje['type'] != 'tutorial_estado') return;
    setState(() {
      _indice = (mensaje['indice'] as int?) ?? _indice;
      _total = (mensaje['total'] as int?) ?? _total;
    });
  }

  void _irADiapositiva(int indice) {
    if (indice < 0 || (_total > 0 && indice >= _total)) return;
    context.read<ConnectionProvider>().sender({
      'type': 'tutorial_slide',
      'indice': indice,
    });
    setState(() => _indice = indice);
  }

  Task _tareaDePrueba() => Task(
        taskId: -1,
        taskType: 'cables',
        duration: 90,
        description: 'Conecta cada cable con su forma y color',
        difficulty: 1,
        createdAt: DateTime.now(),
        params: const {},
      );

  void _terminarPruebaDeCables(bool exito, List<Map<String, dynamic>> _) {
    if (!mounted) return;
    setState(() {
      _tareaCompleta = exito;
      _tareaFallida = !exito;
    });
    if (exito) {
      context.read<ConnectionProvider>().sender({
        'type': 'tutorial_prueba_tarea',
        'exito': true,
      });
    }
  }

  Widget _panelDePrueba() {
    return Column(
      children: [
        const SizedBox(height: 12),
        Text(
          _miradaCompleta
              ? '✓ Cámara: mirada verificada'
              : !_camaraDisponible
                  ? 'Cámara: detector no disponible en Unity'
                  : _mirandoCamara
                      ? 'Cámara: mantén la mirada en la pantalla'
                      : 'Cámara: mira hacia la pantalla del PC',
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 8),
        const Text('Práctica: arrastra cada cable a su conector del mismo color y forma.'),
        const SizedBox(height: 8),
        if (_tareaCompleta)
          const Expanded(child: Center(child: Text('✓ Minitarea completada')))
        else if (_tareaFallida)
          Expanded(
            child: Center(
              child: ElevatedButton(
                onPressed: () => setState(() {
                  _tareaFallida = false;
                  _intento++;
                }),
                child: const Text('Reintentar cables'),
              ),
            ),
          )
        else
          Expanded(
            child: CableGameWidget(
              key: ValueKey(_intento),
              task: _tareaDePrueba(),
              onComplete: _terminarPruebaDeCables,
            ),
          ),
      ],
    );
  }

  Future<void> _confirmarSalida() async {
    final bool? salir = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('¿Salir del tutorial?'),
        content: const Text('Volverás al menú principal.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancelar'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Confirmar'),
          ),
        ],
      ),
    );

    if (salir != true || !mounted) return;
    _cerrarTutorial();
  }

  /// Cierra el tutorial en la PC y vuelve al menú. La X lo usa tras
  /// confirmar; "Terminar" lo usa directo, porque llegar a la última
  /// diapositiva y tocarlo es intencional, no un toque accidental.
  void _cerrarTutorial() {
    final ConnectionProvider conexion = context.read<ConnectionProvider>();
    conexion.sender({'type': 'tutorial_cerrar'});
    conexion.disconnect();
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute<void>(builder: (_) => const MenuPrincipalScreen()),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    final bool hayAnterior = _indice > 0;
    final bool esLaUltima = _total > 0 && _indice >= _total - 1;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Tutorial'),
        actions: [
          IconButton(
            icon: const Icon(Icons.close),
            tooltip: 'Salir del tutorial',
            onPressed: _confirmarSalida,
          ),
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.desktop_windows,
              size: 64,
              color: AppColors.acento.withValues(alpha: 0.7),
            ),
            const SizedBox(height: 16),
            Text(
              _conectado
                  ? 'Mirá la pantalla de la computadora'
                  : 'Conectando...',
              style: Theme.of(context).textTheme.titleLarge,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 8),
            Text(
              'Usá los botones de abajo para avanzar',
              style: Theme.of(context).textTheme.bodyMedium,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 16),
            Text(
              _total > 0 ? '${_indice + 1} de $_total' : '—',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            if (_indice == 2 && _total >= 3)
              Expanded(child: _panelDePrueba())
            else
              const Spacer(),
            const SizedBox(height: 16),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceEvenly,
              children: [
                _boton(
                  icono: Icons.arrow_back,
                  etiqueta: 'Anterior',
                  alTocar: hayAnterior ? () => _irADiapositiva(_indice - 1) : null,
                ),
                // En la última diapositiva no hay a dónde avanzar: el botón
                // pasa a cerrar el tutorial, así el recorrido termina con
                // una acción clara en vez de un botón desactivado.
                esLaUltima
                    ? _boton(
                        icono: Icons.check,
                        etiqueta: 'Terminar',
                        alTocar: _miradaCompleta && _tareaCompleta
                            ? _cerrarTutorial
                            : null,
                      )
                    : _boton(
                        icono: Icons.arrow_forward,
                        etiqueta: 'Siguiente',
                        alTocar: () => _irADiapositiva(_indice + 1),
                      ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _boton({
    required IconData icono,
    required String etiqueta,
    required VoidCallback? alTocar,
  }) {
    return ElevatedButton.icon(
      onPressed: alTocar,
      icon: Icon(icono),
      label: Text(etiqueta),
      style: ElevatedButton.styleFrom(
        padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 20),
        textStyle: const TextStyle(fontSize: 18),
      ),
    );
  }
}
