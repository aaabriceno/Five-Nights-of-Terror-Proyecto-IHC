class Attack {
  final String attackId;
  final String message;
  final DateTime timestamp;

  Attack({
    required this.attackId,
    required this.message,
    required this.timestamp,
  });

  factory Attack.fromJson(Map<String, dynamic> json) {
    return Attack(
      attackId: json['attack_id'] as String,
      message: json['message'] as String,
      timestamp: DateTime.fromMillisecondsSinceEpoch(json['timestamp'] as int),
    );
  }
}
