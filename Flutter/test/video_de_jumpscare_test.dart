import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/utils/video_de_jumpscare.dart';

/// La tablet elige el video del jumpscare con la misma regla que Unity
/// (RutaDeJumpscare.cs): el nombre del animatrónico, sin importar mayúsculas
/// ni espacios, y nada para un nombre desconocido.
void main() {
  group('rutaDeVideoDeJumpscare', () {
    test('cada animatrónico conocido tiene su video', () {
      expect(
        rutaDeVideoDeJumpscare('Freddy'),
        'assets/videos/jumpscare_freddy.webm',
      );
      expect(
        rutaDeVideoDeJumpscare('Vixy'),
        'assets/videos/jumpscare_vixy.webm',
      );
      expect(
        rutaDeVideoDeJumpscare('Puppet'),
        'assets/videos/jumpscare_puppet.webm',
      );
    });

    test('ignora mayúsculas y espacios', () {
      expect(
        rutaDeVideoDeJumpscare('  PUPPET '),
        'assets/videos/jumpscare_puppet.webm',
      );
      expect(
        rutaDeVideoDeJumpscare('vixy'),
        'assets/videos/jumpscare_vixy.webm',
      );
    });

    test('un nombre ausente o desconocido no tiene video', () {
      expect(rutaDeVideoDeJumpscare(null), isNull);
      expect(rutaDeVideoDeJumpscare(''), isNull);
      expect(rutaDeVideoDeJumpscare('Animatrónico'), isNull);
      expect(rutaDeVideoDeJumpscare('../freddy'), isNull);
    });

    test('hay un video por cada animatrónico de la lista', () {
      expect(animatronicosConVideo, <String>['freddy', 'vixy', 'puppet']);
    });
  });
}
