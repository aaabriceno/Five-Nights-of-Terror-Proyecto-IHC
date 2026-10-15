import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/widgets/dial_game_widget.dart';

void main() {
  group('diferenciaAngular', () {
    test('toma el camino corto alrededor de 0°/360°', () {
      expect(diferenciaAngular(350, 10), 20);
      expect(diferenciaAngular(10, 350), 20);
      expect(diferenciaAngular(90, 90), 0);
      expect(diferenciaAngular(0, 180), 180);
    });
  });

  group('proximidadAlObjetivo (0 lejos, 1 listo para soltar)', () {
    test('dentro de la tolerancia vale 1', () {
      expect(proximidadAlObjetivo(0, 25), 1);
      expect(proximidadAlObjetivo(25, 25), 1);
    });

    test('a 90° o más vale 0', () {
      expect(proximidadAlObjetivo(90, 25), 0);
      expect(proximidadAlObjetivo(180, 25), 0);
    });

    test('crece de forma continua al acercarse', () {
      final double lejos = proximidadAlObjetivo(80, 25);
      final double medio = proximidadAlObjetivo(57.5, 25);
      final double cerca = proximidadAlObjetivo(30, 25);
      expect(lejos, lessThan(medio));
      expect(medio, closeTo(0.5, 0.01));
      expect(medio, lessThan(cerca));
      expect(cerca, lessThan(1));
    });
  });

  test('la tolerancia por defecto es ±25°', () {
    expect(toleranciaDePerillaPorDefecto, 25);
  });
}
