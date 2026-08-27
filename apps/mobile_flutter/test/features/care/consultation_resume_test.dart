import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/features/care/care_repository.dart';
import 'package:mobile_flutter/features/consultation/chat_connection.dart';
import 'package:mobile_flutter/features/consultation/video_session_launcher.dart';

void main() {
  test(
    'report filters preserve the UTC+8 date range and report status',
    () async {
      final requests = <RequestOptions>[];
      final dio = Dio(BaseOptions(baseUrl: 'https://example.test/api/v1/'));
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            requests.add(options);
            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: {'items': [], 'total': 0},
              ),
            );
          },
        ),
      );
      final client = ApiClient(
        baseUrl: 'https://example.test/api/v1/',
        readAccessToken: () async => null,
        dio: dio,
      );
      await CareRepository(client).consultations(
        page: 2,
        reports: true,
        status: 'NeedsManual',
        from: '2026-08-01',
        to: '2026-08-27',
      );
      expect(requests.single.uri.path, '/api/v1/results');
      expect(requests.single.uri.queryParameters, {
        'page': '2',
        'pageSize': '15',
        'status': 'NeedsManual',
        'from': '2026-08-01T00:00:00+08:00',
        'to': '2026-08-27T23:59:59.9999999+08:00',
      });
    },
  );
  test('history resumes the selected session without choosing another counselor or creating a consultation', () async {
    final requests = <String>[];
    final dio = Dio(BaseOptions(baseUrl: 'https://example.test/api/v1/'));
    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) {
          requests.add(options.path);
          handler.resolve(
            Response(
              requestOptions: options,
              statusCode: 200,
              data: <String, dynamic>{},
            ),
          );
        },
      ),
    );
    final client = ApiClient(
      baseUrl: 'https://example.test/api/v1/',
      readAccessToken: () async => 'synthetic',
      dio: dio,
    );
    final chat = await ApiChatSessionLauncher(
      client,
      'https://example.test/hubs/chat',
    ).resumeHumanChat('selected-chat', start: false);
    final video = await ApiVideoSessionLauncher(
      client,
      'https://example.test/hubs/rtc',
      'https://example.test/hubs/chat',
    ).resumeHumanVideo('selected-video', start: true);
    expect(chat.sessionId, 'selected-chat');
    expect(video.sessionId, 'selected-video');
    expect(requests, ['consultations/selected-video/start']);
  });
}
