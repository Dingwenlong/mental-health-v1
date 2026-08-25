import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';
import 'package:mobile_flutter/features/ai_consultation/ai_session_launcher.dart';
import 'package:mobile_flutter/features/catalog/catalog_repository.dart';
import 'package:mobile_flutter/features/catalog/service_plan_card.dart';
import 'package:mobile_flutter/generated/ui_copy.g.dart';
import 'package:mobile_flutter/main.dart';
import 'package:mobile_flutter/providers/speech/chime_fallback_speech_provider.dart';
import 'package:mobile_flutter/providers/speech/flutter_tts_speech_provider.dart';
import 'package:mobile_flutter/providers/speech/speech_provider.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Android completes offline AI speech and the crisis safety gate', (
    tester,
  ) async {
    const apiBaseUrl = String.fromEnvironment('API_BASE_URL');
    const userAccessToken = String.fromEnvironment('USER_ACCESS_TOKEN');
    expect(apiBaseUrl, isNotEmpty);
    expect(userAccessToken, isNotEmpty);

    final storage = _MemoryTokenStorage()..token = userAccessToken;
    final client = ApiClient(
      baseUrl: apiBaseUrl,
      readAccessToken: storage.readAccessToken,
    );
    final auth = AuthStore(gateway: ApiAuthGateway(client), storage: storage);
    await auth.restore();
    expect(auth.isAuthenticated, isTrue);
    final ttsEngine = FlutterTtsEngine();
    final ttsLanguages = await ttsEngine.getLanguages();
    final chineseVoices = (await ttsEngine.getVoices())
        .where((voice) => voice.locale.toLowerCase().startsWith('zh'))
        .toList(growable: false);
    final localChineseVoices = chineseVoices
        .where((voice) => !voice.networkRequired)
        .toList(growable: false);
    var installedLocalVoiceCount = 0;
    for (final voice in localChineseVoices) {
      if (voice.installed &&
          await ttsEngine.isLanguageInstalled(voice.locale)) {
        installedLocalVoiceCount += 1;
      }
    }
    debugPrint(
      'TASK13_TTS chineseLanguages='
      '${ttsLanguages.where((value) => value.toLowerCase().startsWith('zh')).join(',')};'
      'localVoiceCount=${localChineseVoices.length};'
      'installedLocalVoiceCount=$installedLocalVoiceCount',
    );
    final speech = _RecordingSpeechProvider(
      FlutterTtsSpeechProvider(
        engine: ttsEngine,
        fallback: ChimeFallbackSpeechProvider(player: AssetChimePlayer()),
      ),
    );
    final aiLauncher = ApiAiSessionLauncher(
      client,
      speechFactory: () => speech,
    );

    await tester.pumpWidget(
      MentalHealthApp(
        authStore: auth,
        catalogRepository: ApiCatalogRepository(client),
        aiLauncher: aiLauncher,
      ),
    );
    await _pumpUntil(tester, find.byType(ServicePlanCard));

    final freeAiChat = find.byWidgetPredicate(
      (widget) =>
          widget is ServicePlanCard &&
          widget.plan.kind == ServiceKind.ai &&
          widget.plan.channel == ServiceChannel.chat &&
          widget.plan.paymentMode == PaymentMode.free,
    );
    expect(freeAiChat, findsOneWidget);
    await tester.ensureVisible(freeAiChat);
    await tester.tap(freeAiChat);
    await tester.pumpAndSettle();
    for (final key in <Key>[
      const Key('consent-service'),
      const Key('consent-recording'),
      const Key('consent-analysis'),
    ]) {
      final consent = find.byKey(key);
      await tester.ensureVisible(consent);
      await tester.tap(consent);
      await tester.pump();
    }
    await tester.tap(find.byKey(const Key('create-order')));
    await _pumpUntil(tester, find.text(UiCopy.get('order.freeConfirmed')));
    await tester.tap(find.byKey(const Key('start-ai-chat')));
    await _pumpUntil(tester, find.byKey(const Key('ai-input')));

    await _submitAiMessage(tester, '最近很难过');
    await _pumpUntil(tester, find.text('这几天不好过。哪件事最让你难受？'));
    await _pumpUntilCondition(
      tester,
      () => speech.lastResult != null,
      diagnostics: () => '中文离线朗读没有在 30 秒内结束。',
    );
    final delivery = speech.lastResult!.delivery;
    expect(
      delivery,
      installedLocalVoiceCount > 0
          ? SpeechDelivery.spoken
          : SpeechDelivery.textOnlyWithChime,
    );
    if (delivery == SpeechDelivery.textOnlyWithChime) {
      await _pumpUntil(tester, find.byKey(const Key('ai-speech-fallback')));
    } else {
      expect(find.byKey(const Key('ai-speech-fallback')), findsNothing);
    }

    await _submitAiMessage(tester, '我已经准备好了工具，今晚就要结束生命');
    await _pumpUntil(tester, find.byKey(const Key('crisis-help')));

    expect(find.text('心理援助：12356'), findsOneWidget);
    expect(find.text('医疗急救：120'), findsOneWidget);
    expect(find.text('报警求助：110'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
    expect(speech.spokenTexts, <String>['这几天不好过。哪件事最让你难受？']);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump(const Duration(milliseconds: 200));
  });
}

Future<void> _submitAiMessage(WidgetTester tester, String text) async {
  final input = find.byKey(const Key('ai-input'));
  await _pumpUntilCondition(
    tester,
    () => tester.widget<TextField>(input).enabled == true,
    diagnostics: () => 'AI 输入框没有恢复为可输入状态。',
  );
  await tester.ensureVisible(input);
  await tester.tap(input);
  await tester.pump();
  await tester.enterText(input, text);
  await tester.pump();
  expect(tester.widget<TextField>(input).controller?.text, text);

  final send = find.byKey(const Key('ai-send'));
  await tester.ensureVisible(send);
  await tester.pump();
  expect(tester.widget<IconButton>(send).onPressed, isNotNull);
  await tester.tap(send);
  await tester.pump();
}

Future<void> _pumpUntil(
  WidgetTester tester,
  Finder finder, {
  int attempts = 120,
}) async {
  for (var attempt = 0; attempt < attempts; attempt += 1) {
    await tester.pump(const Duration(milliseconds: 250));
    if (finder.evaluate().isNotEmpty) return;
  }
  expect(finder, findsWidgets);
}

Future<void> _pumpUntilCondition(
  WidgetTester tester,
  bool Function() condition, {
  required String Function() diagnostics,
  int attempts = 120,
}) async {
  for (var attempt = 0; attempt < attempts; attempt += 1) {
    await tester.pump(const Duration(milliseconds: 250));
    if (condition()) return;
  }
  fail(diagnostics());
}

class _RecordingSpeechProvider implements SpeechProvider {
  _RecordingSpeechProvider(this.delegate);

  final SpeechProvider delegate;
  final List<String> spokenTexts = <String>[];
  SpeechResult? lastResult;

  @override
  Future<SpeechResult> speak(String text) async {
    spokenTexts.add(text);
    lastResult = await delegate.speak(text);
    return lastResult!;
  }

  @override
  Future<void> stop() => delegate.stop();
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
