import 'package:flutter/material.dart';

enum AvatarState { idle, listening, speaking, alert }

class AvatarPainter extends CustomPainter {
  const AvatarPainter(this.state);

  final AvatarState state;

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final radius = size.shortestSide * 0.44;
    final alert = state == AvatarState.alert;
    final ring = Paint()
      ..color = alert ? const Color(0xFFB3261E) : const Color(0xFF2E6F68)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 4;
    final background = Paint()
      ..color = alert ? const Color(0xFFFFEDEA) : const Color(0xFFE5F1EF);
    final face = Paint()..color = const Color(0xFFFFDFC7);
    final line = Paint()
      ..color = const Color(0xFF263332)
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round
      ..strokeWidth = 3;

    canvas.drawCircle(center, radius, background);
    canvas.drawCircle(center, radius, ring);
    canvas.drawOval(
      Rect.fromCenter(
        center: center.translate(0, 4),
        width: radius * 1.28,
        height: radius * 1.5,
      ),
      face,
    );

    final eyeY = center.dy - radius * 0.15;
    if (state == AvatarState.listening) {
      canvas.drawArc(
        Rect.fromCenter(
          center: Offset(center.dx - radius * 0.28, eyeY),
          width: 14,
          height: 8,
        ),
        0,
        3.14,
        false,
        line,
      );
      canvas.drawArc(
        Rect.fromCenter(
          center: Offset(center.dx + radius * 0.28, eyeY),
          width: 14,
          height: 8,
        ),
        0,
        3.14,
        false,
        line,
      );
    } else {
      canvas.drawCircle(
        Offset(center.dx - radius * 0.27, eyeY),
        3.5,
        line..style = PaintingStyle.fill,
      );
      canvas.drawCircle(Offset(center.dx + radius * 0.27, eyeY), 3.5, line);
      line.style = PaintingStyle.stroke;
    }

    final mouthCenter = center.translate(0, radius * 0.26);
    if (state == AvatarState.speaking) {
      canvas.drawOval(
        Rect.fromCenter(center: mouthCenter, width: 20, height: 13),
        line,
      );
    } else if (alert) {
      canvas.drawLine(
        mouthCenter.translate(-9, 2),
        mouthCenter.translate(9, 2),
        line,
      );
    } else {
      canvas.drawArc(
        Rect.fromCenter(center: mouthCenter, width: 24, height: 14),
        0.15,
        2.84,
        false,
        line,
      );
    }

    if (state == AvatarState.listening) {
      final wave = Paint()
        ..color = const Color(0xFF2E6F68)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2;
      canvas.drawArc(
        Rect.fromCenter(
          center: center.translate(radius * 0.89, 0),
          width: 15,
          height: 28,
        ),
        -1.15,
        2.3,
        false,
        wave,
      );
    }
  }

  @override
  bool shouldRepaint(AvatarPainter oldDelegate) => oldDelegate.state != state;
}
