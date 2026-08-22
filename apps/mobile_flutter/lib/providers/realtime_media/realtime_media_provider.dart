import 'package:flutter/material.dart';

enum MediaRoomState {
  idle,
  requestingPermissions,
  signaling,
  connected,
  failed,
  closed,
}

enum MediaStartResult { connected, permissionDenied, failed }

abstract class RealtimeMediaProvider extends ChangeNotifier {
  MediaRoomState get state;

  bool get uploadFailed => false;

  Future<MediaStartResult> start(String sessionId);

  Future<void> close();

  Widget buildLocalPreview();

  Widget buildRemotePreview();
}
