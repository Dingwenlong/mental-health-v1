import '../../core/api/api_client.dart';
import '../../providers/speech/speech_provider.dart';
import 'ai_consultation_page.dart';

typedef SpeechProviderFactory = SpeechProvider Function();

class StartedAiSession {
  const StartedAiSession({
    required this.sessionId,
    required this.gateway,
    required this.speech,
  });

  final String sessionId;
  final AiTurnGateway gateway;
  final SpeechProvider speech;
}

abstract interface class AiSessionLauncher {
  Future<StartedAiSession> startAiChat(String orderId);
}

class ApiAiSessionLauncher implements AiSessionLauncher {
  const ApiAiSessionLauncher(this._client, {required this.speechFactory});

  final ApiClient _client;
  final SpeechProviderFactory speechFactory;

  @override
  Future<StartedAiSession> startAiChat(String orderId) async {
    final created = await _client.post(
      'consultations',
      data: <String, dynamic>{
        'orderId': orderId,
        'assignedPractitionerId': null,
        'scheduledAt': DateTime.now()
            .toUtc()
            .add(const Duration(minutes: 1))
            .toIso8601String(),
        'idempotencyKey': 'mobile-ai-session-$orderId',
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

    return StartedAiSession(
      sessionId: sessionId,
      gateway: ApiAiTurnGateway(_client),
      speech: speechFactory(),
    );
  }
}
