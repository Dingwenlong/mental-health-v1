import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/ai_consultation/ai_consultation_page.dart';
import 'package:mobile_flutter/features/ai_consultation/ai_session_launcher.dart';
import 'package:mobile_flutter/features/catalog/catalog_repository.dart';
import 'package:mobile_flutter/features/catalog/order_page.dart';
import 'package:mobile_flutter/providers/speech/speech_provider.dart';

void main() {
  testWidgets('shows the AI identity and reads a normal reply', (tester) async {
    final gateway = _FakeAiTurnGateway();
    final speech = _FakeSpeechProvider();

    await tester.pumpWidget(
      MaterialApp(
        home: AiConsultationPage(
          sessionId: 'session-1',
          gateway: gateway,
          speech: speech,
        ),
      ),
    );

    expect(find.text('你正在和 AI 虚拟人对话。它不是真人，也不是医生。'), findsOneWidget);
    expect(find.byKey(const Key('avatar-idle')), findsOneWidget);

    await tester.enterText(find.byKey(const Key('ai-input')), '最近总是睡不好');
    await tester.tap(find.byKey(const Key('ai-send')));
    await tester.pumpAndSettle();

    expect(find.text('最近总是睡不好'), findsOneWidget);
    expect(find.text('我们先从睡眠说起。'), findsOneWidget);
    expect(speech.spokenTexts, <String>['我们先从睡眠说起。']);
    expect(find.byKey(const Key('avatar-idle')), findsOneWidget);
    expect(find.byKey(const Key('ai-input')), findsOneWidget);
  });

  testWidgets(
    'stops an unfinished reply, locks input, and shows crisis numbers',
    (tester) async {
      final gateway = _FakeAiTurnGateway();
      final speech = _FakeSpeechProvider(holdFirstSpeech: true);

      await tester.pumpWidget(
        MaterialApp(
          home: AiConsultationPage(
            sessionId: 'session-1',
            gateway: gateway,
            speech: speech,
          ),
        ),
      );

      await tester.enterText(find.byKey(const Key('ai-input')), '最近有点累');
      await tester.tap(find.byKey(const Key('ai-send')));
      await tester.pump();
      await tester.pump();

      expect(find.byKey(const Key('avatar-speaking')), findsOneWidget);
      expect(speech.spokenTexts, <String>['我们先从睡眠说起。']);

      await tester.enterText(
        find.byKey(const Key('ai-input')),
        '我已经准备好了工具，今晚就要结束生命',
      );
      await tester.tap(find.byKey(const Key('ai-send')));
      await tester.pumpAndSettle();

      expect(speech.stopCount, greaterThanOrEqualTo(2));
      expect(speech.spokenTexts, <String>['我们先从睡眠说起。']);
      expect(find.byKey(const Key('avatar-alert')), findsOneWidget);
      expect(find.text('心理援助：12356'), findsOneWidget);
      expect(find.text('医疗急救：120'), findsOneWidget);
      expect(find.text('报警求助：110'), findsOneWidget);
      expect(find.byType(TextField), findsNothing);
    },
  );

  testWidgets('keeps the reply visible when speech falls back to a chime', (
    tester,
  ) async {
    final speech = _FakeSpeechProvider(
      result: const SpeechResult.textOnlyWithChime(),
    );

    await tester.pumpWidget(
      MaterialApp(
        home: AiConsultationPage(
          sessionId: 'session-1',
          gateway: _FakeAiTurnGateway(),
          speech: speech,
        ),
      ),
    );

    await tester.enterText(find.byKey(const Key('ai-input')), '最近总是睡不好');
    await tester.tap(find.byKey(const Key('ai-send')));
    await tester.pumpAndSettle();

    expect(find.text('我们先从睡眠说起。'), findsOneWidget);
    expect(find.text('没有可用的中文离线语音，回复已显示在屏幕上。'), findsOneWidget);
  });

  testWidgets('confirmed AI chat order opens the AI consultation page', (
    tester,
  ) async {
    final launcher = _FakeAiSessionLauncher();
    await tester.pumpWidget(
      MaterialApp(
        home: OrderPage(
          order: const DemoOrderSummary(
            id: 'order-1',
            status: DemoOrderStatus.confirmed,
            amountInMinorUnits: 0,
            currency: 'CNY',
          ),
          plan: const ServicePlanSummary(
            id: 'plan-ai-chat-free',
            kind: ServiceKind.ai,
            channel: ServiceChannel.chat,
            paymentMode: PaymentMode.free,
            priceInMinorUnits: 0,
            currency: 'CNY',
            durationMinutes: 30,
          ),
          aiLauncher: launcher,
        ),
      ),
    );

    expect(find.text('进入 AI 咨询'), findsOneWidget);
    await tester.tap(find.byKey(const Key('start-ai-chat')));
    await tester.pumpAndSettle();

    expect(launcher.orderIds, <String>['order-1']);
    expect(find.text('AI 咨询'), findsOneWidget);
    expect(find.text('你正在和 AI 虚拟人对话。它不是真人，也不是医生。'), findsOneWidget);
  });
}

class _FakeAiTurnGateway implements AiTurnGateway {
  @override
  Future<AiTurnResult> sendTurn({
    required String sessionId,
    required String text,
    required String clientMessageId,
  }) async {
    if (text.contains('结束生命')) {
      return const AiTurnResult(
        replyText: '我很担心你现在的安全。请立即拨打 12356；危险正在发生时请拨打 120 或 110。',
        isCrisis: true,
      );
    }
    return const AiTurnResult(replyText: '我们先从睡眠说起。', isCrisis: false);
  }
}

class _FakeSpeechProvider implements SpeechProvider {
  _FakeSpeechProvider({
    this.holdFirstSpeech = false,
    this.result = const SpeechResult.spoken(),
  });

  final bool holdFirstSpeech;
  final SpeechResult result;
  final List<String> spokenTexts = <String>[];
  Completer<SpeechResult>? _heldSpeech;
  int stopCount = 0;

  @override
  Future<SpeechResult> speak(String text) {
    spokenTexts.add(text);
    if (holdFirstSpeech && spokenTexts.length == 1) {
      _heldSpeech = Completer<SpeechResult>();
      return _heldSpeech!.future;
    }
    return Future<SpeechResult>.value(result);
  }

  @override
  Future<void> stop() async {
    stopCount += 1;
    if (_heldSpeech case final held? when !held.isCompleted) {
      held.complete(result);
    }
  }
}

class _FakeAiSessionLauncher implements AiSessionLauncher {
  final List<String> orderIds = <String>[];

  @override
  Future<StartedAiSession> startAiChat(String orderId) async {
    orderIds.add(orderId);
    return StartedAiSession(
      sessionId: 'session-1',
      gateway: _FakeAiTurnGateway(),
      speech: _FakeSpeechProvider(),
    );
  }
}
