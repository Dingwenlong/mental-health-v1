import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/features/ai_consultation/ai_consultation_page.dart';
import 'package:mobile_flutter/features/ai_consultation/ai_session_launcher.dart';
import 'package:mobile_flutter/providers/speech/speech_provider.dart';

void main() {
  test(
    'creates and starts an AI chat session without a practitioner',
    () async {
      final requests = <RequestOptions>[];
      final dio = Dio(BaseOptions(baseUrl: 'https://example.test/api/v1/'))
        ..interceptors.add(
          InterceptorsWrapper(
            onRequest: (options, handler) {
              requests.add(options);
              if (options.path == 'consultations') {
                handler.resolve(
                  Response<Map<String, dynamic>>(
                    requestOptions: options,
                    statusCode: 201,
                    data: const <String, dynamic>{
                      'id': 'session-1',
                      'status': 'Scheduled',
                    },
                  ),
                );
                return;
              }
              handler.resolve(
                Response<Map<String, dynamic>>(
                  requestOptions: options,
                  statusCode: 200,
                  data: const <String, dynamic>{},
                ),
              );
            },
          ),
        );
      final speech = _FakeSpeechProvider();
      final launcher = ApiAiSessionLauncher(
        ApiClient(
          baseUrl: 'https://example.test/api/v1/',
          readAccessToken: () async => 'token',
          dio: dio,
        ),
        speechFactory: () => speech,
      );

      final started = await launcher.startAiChat('order-1');

      expect(started.sessionId, 'session-1');
      expect(started.gateway, isA<ApiAiTurnGateway>());
      expect(started.speech, same(speech));
      expect(requests.map((request) => request.path), <String>[
        'consultations',
        'consultations/session-1/start',
      ]);
      final createData = Map<String, dynamic>.from(requests.first.data as Map);
      expect(createData['orderId'], 'order-1');
      expect(createData['assignedPractitionerId'], isNull);
      expect(createData['idempotencyKey'], 'mobile-ai-session-order-1');
    },
  );
}

class _FakeSpeechProvider implements SpeechProvider {
  @override
  Future<SpeechResult> speak(String text) async => const SpeechResult.spoken();

  @override
  Future<void> stop() async {}
}
