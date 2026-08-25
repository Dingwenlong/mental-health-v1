import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';
import 'package:mobile_flutter/core/network/demo_certificate_trust.dart';
import 'package:mobile_flutter/features/consultation/chat_connection.dart';
import 'package:mobile_flutter/features/consultation/video_page.dart';
import 'package:mobile_flutter/providers/realtime_media/flutter_webrtc_media_provider.dart';
import 'package:mobile_flutter/providers/realtime_media/media_recording_uploader.dart';
import 'package:mobile_flutter/providers/realtime_media/realtime_media_provider.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Android and browser complete one same-lan video call', (
    tester,
  ) async {
    const apiBaseUrl = String.fromEnvironment('API_BASE_URL');
    const rtcHubUrl = String.fromEnvironment('RTC_HUB_URL');
    const userAccessToken = String.fromEnvironment('USER_ACCESS_TOKEN');
    const sessionId = String.fromEnvironment('SESSION_ID');
    expect(apiBaseUrl, isNotEmpty);
    expect(rtcHubUrl, isNotEmpty);
    expect(userAccessToken, isNotEmpty);
    expect(sessionId, isNotEmpty);
    await installDemoCertificateTrust();

    final storage = _MemoryTokenStorage()..token = userAccessToken;
    final client = ApiClient(
      baseUrl: apiBaseUrl,
      readAccessToken: storage.readAccessToken,
    );
    debugPrint('TASK11_ANDROID_WAITING');
    await _waitForMarker(client, sessionId, '网页视频端已就绪');

    final media = FlutterWebRtcMediaProvider(
      rtcHubUrl,
      storage.readAccessToken,
      MediaRecordingUploader(client),
    );
    await tester.pumpWidget(
      MaterialApp(
        home: VideoPage(
          sessionId: sessionId,
          media: media,
          onSwitchToText: () {},
        ),
      ),
    );
    await tester.tap(find.byKey(const Key('video-start')));
    await _pumpUntil(
      tester,
      () => media.state == MediaRoomState.connected,
      diagnostics: () => 'Android 视频状态：${media.state}',
    );
    expect(find.text('视频已连接'), findsOneWidget);
    await client.post(
      'consultations/$sessionId/messages',
      data: const <String, dynamic>{
        'text': 'Android 视频已连接',
        'clientMessageId': 'task11-android-connected',
      },
    );
    await _waitForMarker(client, sessionId, '网页已确认连接');
    await tester.pump(const Duration(seconds: 2));

    await media.close();
    await tester.pump();
    expect(media.state, MediaRoomState.closed);
    expect(find.text('视频已结束'), findsOneWidget);
  });
}

Future<void> _waitForMarker(
  ApiClient client,
  String sessionId,
  String marker,
) async {
  final history = ApiChatHistory(client);
  for (var attempt = 0; attempt < 600; attempt += 1) {
    final messages = await history.getMessages(sessionId, afterSequence: 0);
    if (messages.any((message) => message.text == marker)) return;
    await Future<void>.delayed(const Duration(milliseconds: 200));
  }
  fail('等待验收标记超时：$marker');
}

Future<void> _pumpUntil(
  WidgetTester tester,
  bool Function() condition, {
  required String Function() diagnostics,
}) async {
  for (var attempt = 0; attempt < 80; attempt += 1) {
    await tester.pump(const Duration(milliseconds: 250));
    if (condition()) return;
  }
  fail(diagnostics());
}

class _MemoryTokenStorage implements TokenStorage {
  String? token;

  @override
  Future<void> clearAccessToken() async => token = null;

  @override
  Future<String?> readAccessToken() async => token;

  @override
  Future<void> writeAccessToken(String value) async => token = value;
}
