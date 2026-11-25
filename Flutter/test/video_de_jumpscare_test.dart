import 'dart:io';

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

  // La cara de respaldo (2026-11-24): sacada del video de cada animatrónico,
  // se ve al instante y queda si el video no se puede reproducir.
  group('rutaDeImagenDeJumpscare', () {
    test('cada animatrónico conocido tiene su cara', () {
      expect(
        rutaDeImagenDeJumpscare('Freddy'),
        'assets/images/jumpscare_freddy.jpg',
      );
      expect(
        rutaDeImagenDeJumpscare(' vixy '),
        'assets/images/jumpscare_vixy.jpg',
      );
      expect(
        rutaDeImagenDeJumpscare('PUPPET'),
        'assets/images/jumpscare_puppet.jpg',
      );
    });

    test('un nombre ausente o desconocido usa la imagen general', () {
      expect(rutaDeImagenDeJumpscare(null), 'assets/images/jumpscare.jpg');
      expect(rutaDeImagenDeJumpscare(''), 'assets/images/jumpscare.jpg');
      expect(
        rutaDeImagenDeJumpscare('Animatrónico'),
        'assets/images/jumpscare.jpg',
      );
      expect(
        rutaDeImagenDeJumpscare('../freddy'),
        'assets/images/jumpscare.jpg',
      );
    });

    test('las cuatro imágenes existen en los assets', () {
      for (final String? quien in <String?>[null, ...animatronicosConVideo]) {
        final String ruta = rutaDeImagenDeJumpscare(quien);
        expect(File(ruta).existsSync(), isTrue, reason: ruta);
      }
    });
  });
}
