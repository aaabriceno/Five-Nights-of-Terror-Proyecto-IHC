import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/widgets/encabezado_de_minijuego.dart';

void main() {
  testWidgets('título y tiempo restante se leen grandes', (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        body: EncabezadoDeMinijuego(titulo: 'Conectar Cables', segundosRestantes: 42),
      ),
    ));
    final TextStyle titulo = tester.widget<Text>(find.text('Conectar Cables')).style!;
    final TextStyle tiempo = tester.widget<Text>(find.text('Tiempo restante: 42 s')).style!;
    expect(titulo.fontSize, greaterThanOrEqualTo(34));
    expect(titulo.fontWeight, FontWeight.bold);
    expect(tiempo.fontSize, greaterThanOrEqualTo(26));
  });

  test('estilos compartidos: estado y detalle grandes', () {
    expect(EstilosDeMinijuego.estado.fontSize, greaterThanOrEqualTo(26));
    expect(EstilosDeMinijuego.detalle.fontSize, greaterThanOrEqualTo(24));
  });
}
