import '../../core/api/api_client.dart';
import '../../providers/realtime_media/flutter_webrtc_media_provider.dart';
import '../../providers/realtime_media/media_recording_uploader.dart';
import '../../providers/realtime_media/realtime_media_provider.dart';
import 'chat_connection.dart';

class StartedVideoSession {
  const StartedVideoSession({
    required this.sessionId,
    required this.media,
    required this.textController,
  });

  final String sessionId;
  final RealtimeMediaProvider media;
  final ChatController textController;
}

abstract interface class VideoSessionLauncher {
  Future<StartedVideoSession> startHumanVideo(String orderId);
  Future<StartedVideoSession> resumeHumanVideo(
    String sessionId, {
    required bool start,
  });
}

class ApiVideoSessionLauncher implements VideoSessionLauncher {
  const ApiVideoSessionLauncher(
    this._client,
    this._rtcHubUrl,
    this._chatHubUrl,
  );

  final ApiClient _client;
  final String _rtcHubUrl;
  final String _chatHubUrl;

  @override
  Future<StartedVideoSession> startHumanVideo(String orderId) async {
    final practitioners = await _client.getList('catalog/practitioners');
    final counselor = practitioners
        .cast<Map>()
        .map(Map<String, dynamic>.from)
        .firstWhere(
          (item) => item['role'] == 'Counselor' && item['active'] == true,
          orElse: () => throw const ApiFailure(
            code: 'PRACTITIONER_NOT_AVAILABLE',
            message: 'No counselor is available.',
          ),
        );
    final created = await _client.post(
      'consultations',
      data: <String, dynamic>{
        'orderId': orderId,
        'assignedPractitionerId': counselor['id'],
        'scheduledAt': DateTime.now()
            .toUtc()
            .add(const Duration(minutes: 1))
            .toIso8601String(),
        'idempotencyKey': 'mobile-video-session-$orderId',
      },
    );
    final sessionId = created['id'].toString();
    final status = created['status'].toString();
    if (status == 'Scheduled') {
      await _client.post('consultations/$sessionId/start');
    } else if (status != 'InProgress') {
      throw const ApiFailure(
        code: 'INVALID_SESSION_STATE',
        message: 'The consultation cannot be opened.',
      );
    }

    return _existingSession(sessionId);
  }

  @override
  Future<StartedVideoSession> resumeHumanVideo(
    String sessionId, {
    required bool start,
  }) async {
    if (start) await _client.post('consultations/$sessionId/start');
    return _existingSession(sessionId);
  }

  StartedVideoSession _existingSession(String sessionId) => StartedVideoSession(
    sessionId: sessionId,
    media: FlutterWebRtcMediaProvider(
      _rtcHubUrl,
      _client.readAccessToken,
      MediaRecordingUploader(_client),
    ),
    textController: ChatController(
      history: ApiChatHistory(_client),
      transport: SignalRChatTransport(
        hubUrl: _chatHubUrl,
        readAccessToken: _client.readAccessToken,
      ),
    ),
  );
}
