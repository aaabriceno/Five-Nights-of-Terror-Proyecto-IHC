/// Algo que hizo (o le pasó a) el jugador 2 y que el jugador 1 debe enterarse:
/// retrasó a un animatrónico, puso una trampa, lo salvó de Puppet, entró o se
/// cayó. Unity lo manda como un mensaje `efecto`; el texto se arma aquí, desde
/// el punto de vista del jugador 1 ("tu compañero..."), para que cada pantalla
/// cuente lo mismo con sus propias palabras.
class AvisoDeCompanero {
  /// El `tipo` del `efecto` de Unity: `ralentizado`, `trampa_colocada`,
  /// `trampa_disparada`, `puppet_rescatado`, `operador_conectado`,
  /// `operador_perdido`... (o uno nuevo que esta versión aún no conoce).
  final String tipo;

  /// Lo que pasó, en una línea corta.
  final String titulo;

  /// Lo que cambia para el jugador 1 (la multa de la caja, el ritmo de los
  /// animatrónicos...). Null si no hay nada más que contar.
  final String? detalle;

  /// Cuántas veces más rápido se descarga la caja de Puppet desde el rescate;
  /// 0 si el aviso no es de un rescate.
  final double multaDeCaja;

  /// Cuánto dura ahora la caja de Puppet llena (30 s, 15 s, 7,5 s...); 0 si el
  /// aviso no es de un rescate o Unity no lo mandó.
  final double segundosDeCaja;

  final DateTime recibidoEn;

  const AvisoDeCompanero({
    required this.tipo,
    required this.titulo,
    this.detalle,
    this.multaDeCaja = 0,
    this.segundosDeCaja = 0,
    required this.recibidoEn,
  });

  /// Los avisos que dejan al jugador 1 en peor situación (se cayó el
  /// compañero) se pintan distinto a los que lo ayudan.
  bool get esMalaNoticia => tipo == 'operador_perdido';

  /// Interpreta un mensaje `efecto`. Devuelve null si el tipo no se conoce y
  /// Unity tampoco mandó un texto: mejor no mostrar nada que algo sin sentido.
  static AvisoDeCompanero? deEfecto(Map<String, dynamic> mensaje, {DateTime? ahora}) {
    final String tipo = (mensaje['tipo'] as String?) ?? 'ralentizado';
    final String animatronico = _nombre((mensaje['animatronic'] as String?) ?? '');
    final double segundos = ((mensaje['segundos'] as num?) ?? 0).toDouble();
    final double multa = ((mensaje['multa_factor'] as num?) ?? 0).toDouble();
    final double segundosCaja = ((mensaje['segundos_caja'] as num?) ?? 0).toDouble();
    final String texto = ((mensaje['message'] as String?) ?? '').trim();
    final DateTime recibido = ahora ?? DateTime.now();

    AvisoDeCompanero crear(String titulo,
            {String? detalle, double multaDeCaja = 0, double segundosDeCaja = 0}) =>
        AvisoDeCompanero(
          tipo: tipo,
          titulo: titulo,
          detalle: detalle,
          multaDeCaja: multaDeCaja,
          segundosDeCaja: segundosDeCaja,
          recibidoEn: recibido,
        );

    switch (tipo) {
      case 'ralentizado':
        return crear(
          animatronico.isEmpty
              ? 'Tu compañero ralentizó a un animatrónico'
              : 'Tu compañero ralentizó a $animatronico',
          detalle: segundos > 0 ? 'Avanza más lento por ${segundos.round()} s' : null,
        );
      case 'trampa_colocada':
        return crear('Tu compañero puso una trampa');
      case 'trampa_disparada':
        return crear(
          animatronico.isEmpty
              ? 'Trampa: un animatrónico quedó atrapado'
              : 'Trampa: $animatronico quedó atrapado',
          detalle: segundos > 0 ? 'Se queda quieto ${segundos.round()} s' : null,
        );
      case 'puppet_rescatado':
        return crear(
          'Tu compañero te salvó de Puppet',
          detalle: segundosCaja > 0
              ? 'Pero ahora la caja dura ${formatearSegundos(segundosCaja)} s'
              : multa > 1
                  ? 'Pero la caja se descarga ${formatearMulta(multa)} más rápido'
                  : null,
          multaDeCaja: multa,
          segundosDeCaja: segundosCaja,
        );
      case 'operador_conectado':
        return crear('Tu compañero se conectó', detalle: 'Freddy y Vixy avanzan más rápido');
      case 'operador_perdido':
        return crear('Tu compañero se desconectó', detalle: 'Freddy y Vixy vuelven a su ritmo normal');
    }
    if (texto.isEmpty) return null;
    return crear(texto);
  }

  /// "×2", "×4", "×1,5"... sin decimales cuando sobran.
  static String formatearMulta(double factor) {
    final String numero = factor == factor.roundToDouble()
        ? factor.round().toString()
        : factor.toStringAsFixed(1).replaceAll('.', ',');
    return '×$numero';
  }

  /// "15", "7,5", "3,75": los segundos sin ceros de más.
  static String formatearSegundos(double segundos) {
    final String texto = segundos.toStringAsFixed(2).replaceFirst(RegExp(r'\.?0+$'), '');
    return texto.replaceAll('.', ',');
  }

  static String _nombre(String crudo) {
    final String limpio = crudo.trim().toLowerCase();
    if (limpio.isEmpty) return '';
    return limpio[0].toUpperCase() + limpio.substring(1);
  }
}
