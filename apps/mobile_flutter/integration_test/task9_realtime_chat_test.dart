import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';
import 'package:mobile_flutter/features/catalog/catalog_repository.dart';
import 'package:mobile_flutter/features/catalog/service_plan_card.dart';
import 'package:mobile_flutter/features/consultation/chat_connection.dart';
import 'package:mobile_flutter/features/consultation/chat_page.dart';
import 'package:mobile_flutter/generated/ui_copy.g.dart';
import 'package:mobile_flutter/main.dart';
import 'package:signalr_netcore/signalr_client.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Android user and counselor exchange twenty persisted messages', (
    tester,
  ) async {
    const apiBaseUrl = String.fromEnvironment('API_BASE_URL');
    const chatHubUrl = String.fromEnvironment('CHAT_HUB_URL');
    const userEmail = String.fromEnvironment('DEMO_USER_EMAIL');
    const counselorEmail = String.fromEnvironment('DEMO_COUNSELOR_EMAIL');
    const password = String.fromEnvironment('DEMO_PASSWORD');
    expect(apiBaseUrl, isNotEmpty);
    expect(chatHubUrl, isNotEmpty);
    expect(userEmail, isNotEmpty);
    expect(counselorEmail, isNotEmpty);
    expect(password, isNotEmpty);

    final storage = _MemoryTokenStorage();
    final userClient = ApiClient(
      baseUrl: apiBaseUrl,
      readAccessToken: storage.readAccessToken,
    );
    final auth = AuthStore(
      gateway: ApiAuthGateway(userClient),
      storage: storage,
    );
    final catalog = ApiCatalogRepository(userClient);
    final launcher = ApiChatSessionLauncher(userClient, chatHubUrl);

    await tester.pumpWidget(
      MentalHealthApp(
        authStore: auth,
        catalogRepository: catalog,
        chatLauncher: launcher,
      ),
    );
    await tester.enterText(find.byKey(const Key('login-email')), userEmail);
    await tester.enterText(find.byKey(const Key('login-password')), password);
    await tester.tap(find.byKey(const Key('login-submit')));
    await _pumpUntil(tester, find.byType(ServicePlanCard));

    final humanChat = find.byWidgetPredicate(
      (widget) =>
          widget is ServicePlanCard &&
          widget.plan.kind == ServiceKind.human &&
          widget.plan.channel == ServiceChannel.chat &&
          widget.plan.paymentMode == PaymentMode.free,
    );
    expect(humanChat, findsOneWidget);
    await tester.ensureVisible(humanChat);
    await tester.tap(humanChat);
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
    final createOrder = find.byKey(const Key('create-order'));
    await tester.ensureVisible(createOrder);
    await tester.tap(createOrder);
    await _pumpUntil(tester, find.text(UiCopy.get('order.freeConfirmed')));
    await tester.tap(find.byKey(const Key('start-chat')));
    await _pumpUntil(tester, find.byType(ChatPage), attempts: 160);
    await _pumpUntil(
      tester,
      find.byKey(const Key('chat-input')),
      attempts: 160,
    );
    await _pumpUntilCondition(
      tester,
      () =>
          tester
              .widget<TextField>(find.byKey(const Key('chat-input')))
              .enabled ==
          true,
      attempts: 160,
    );

    final chatPage = tester.widget<ChatPage>(find.byType(ChatPage));
    final counselorClient = ApiClient(
      baseUrl: apiBaseUrl,
      readAccessToken: () async => null,
    );
    final counselorToken = await ApiAuthGateway(counselorClient)
        .login(email: counselorEmail, password: password);
    final counselor = HubConnectionBuilder()
        .withUrl(
          chatHubUrl,
          options: HttpConnectionOptions(
            accessTokenFactory: () async => counselorToken,
          ),
        )
        .build();
    final receivedByCounselor = <ChatMessage>[];
    counselor.on('MessageReceived', (arguments) {
      final value = arguments?.firstOrNull;
      if (value is Map) {
        receivedByCounselor.add(
          ChatMessage.fromJson(Map<String, dynamic>.from(value)),
        );
      }
    });

    try {
      await counselor.start();
      await counselor.invoke('JoinSession', args: <Object>[chatPage.sessionId]);
      for (var index = 1; index <= 10; index += 1) {
        final userText = '用户合成消息 $index';
        await _pumpUntilCondition(
          tester,
          () =>
              tester
                  .widget<TextField>(find.byKey(const Key('chat-input')))
                  .enabled ==
              true,
          diagnostics: () =>
              '第 $index 轮输入框没有恢复；状态=${chatPage.controller.status}',
        );
        final input = find.byKey(const Key('chat-input'));
        await tester.showKeyboard(input);
        tester.testTextInput.enterText(userText);
        await tester.pump();
        expect(tester.widget<TextField>(input).controller?.text, userText);
        await _pumpUntilCondition(
          tester,
          () =>
              tester
                  .widget<FilledButton>(find.byKey(const Key('chat-send')))
                  .onPressed !=
              null,
          diagnostics: () => '第 $index 轮发送按钮没有启用。',
        );
        await tester.tap(find.byKey(const Key('chat-send')));
        await _waitUntil(() => receivedByCounselor.length >= index);
        await _pumpUntilCondition(
          tester,
          () => chatPage.controller.messages.any(
            (message) => message.text == userText,
          ),
          diagnostics: () =>
              '用户消息未合并；状态=${chatPage.controller.status}；'
              '现有消息=${chatPage.controller.messages.map((message) => message.text).join('|')}',
        );

        final counselorText = '咨询师合成消息 $index';
        await counselor.invoke(
          'SendMessage',
          args: <Object>[chatPage.sessionId, counselorText, 'counselor-$index'],
        );
        await _pumpUntilCondition(
          tester,
          () => chatPage.controller.messages.any(
            (message) => message.text == counselorText,
          ),
          diagnostics: () =>
              '咨询师消息未到达；状态=${chatPage.controller.status}；'
              '现有消息=${chatPage.controller.messages.map((message) => message.text).join('|')}',
        );
      }

      expect(chatPage.controller.messages, hasLength(20));
      final history = await ApiChatHistory(userClient)
          .getMessages(chatPage.sessionId, afterSequence: 0);
      expect(history, hasLength(20));
      expect(
        history.map((message) => message.sequence),
        List<int>.generate(20, (index) => index + 1),
      );
      expect(history.map((message) => message.id).toSet(), hasLength(20));
    } finally {
      await counselor.stop();
    }
  });
}

Future<void> _pumpUntil(
  WidgetTester tester,
  Finder finder, {
  int attempts = 80,
}) async {
  for (var attempt = 0; attempt < attempts; attempt += 1) {
    await tester.pump(const Duration(milliseconds: 250));
    if (finder.evaluate().isNotEmpty) {
      return;
    }
  }
  expect(finder, findsWidgets);
}

Future<void> _waitUntil(bool Function() condition) async {
  for (var attempt = 0; attempt < 80; attempt += 1) {
    if (condition()) {
      return;
    }
    await Future<void>.delayed(const Duration(milliseconds: 100));
  }
  expect(condition(), isTrue);
}

Future<void> _pumpUntilCondition(
  WidgetTester tester,
  bool Function() condition, {
  int attempts = 80,
  String Function()? diagnostics,
}) async {
  for (var attempt = 0; attempt < attempts; attempt += 1) {
    await tester.pump(const Duration(milliseconds: 250));
    if (condition()) {
      return;
    }
  }
  fail(diagnostics?.call() ?? '等待条件超时。');
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
