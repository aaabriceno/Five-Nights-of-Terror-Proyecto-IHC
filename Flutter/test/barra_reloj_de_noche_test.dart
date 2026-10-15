import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/widgets/barra_reloj_de_noche.dart';

void main() {
  Future<void> montar(WidgetTester tester, int noche, String hora) =>
      tester.pumpWidget(MaterialApp(
        home: Scaffold(
          body: BarraRelojDeNoche(nocheActual: noche, horaEnJuego: hora),
        ),
      ));

  testWidgets('el total de noches es 6 (igual que Unity)', (tester) async {
    await montar(tester, 1, '12:00 AM');
    expect(find.text('Noche 1/6'), findsOneWidget);
  });

  testWidgets('la sexta noche se ve como 6/6, sin "(Extra)"', (tester) async {
    await montar(tester, 6, '2:00 AM');
    expect(find.text('Noche 6/6'), findsOneWidget);
    expect(find.textContaining('Extra'), findsNothing);
  });

  testWidgets('la hora muestra solo la hora', (tester) async {
    await montar(tester, 2, '3:47 AM');
    expect(find.text('3 AM'), findsOneWidget);
  });

  testWidgets('noche y hora se leen grandes (≥ 28 px) y en negrita', (tester) async {
    await montar(tester, 1, '1:00 AM');
    for (final String texto in <String>['Noche 1/6', '1 AM']) {
      final TextStyle estilo = tester.widget<Text>(find.text(texto)).style!;
      expect(estilo.fontSize, greaterThanOrEqualTo(28), reason: texto);
      expect(estilo.fontWeight, FontWeight.bold, reason: texto);
    }
  });
}
