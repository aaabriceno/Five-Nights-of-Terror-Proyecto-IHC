class Attack {
  final String attackId;
  final String message;
  final DateTime timestamp;

  /// Quién atacó (Freddy, Vixy o Puppet). Unity lo manda siempre; es opcional
  /// por si llega un mensaje de una versión que no lo trae.
  final String? animatronic;

  Attack({
    required this.attackId,
    required this.message,
    required this.timestamp,
    this.animatronic,
  });

  factory Attack.fromJson(Map<String, dynamic> json) {
    return Attack(
      attackId: json['attack_id'] as String,
      message: json['message'] as String,
      timestamp: DateTime.fromMillisecondsSinceEpoch(json['timestamp'] as int),
      animatronic: json['animatronic'] as String?,
    );
  }
}
