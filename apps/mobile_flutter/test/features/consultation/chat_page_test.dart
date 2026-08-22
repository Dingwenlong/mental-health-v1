import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/consultation/chat_connection.dart';
import 'package:mobile_flutter/features/consultation/chat_page.dart';

void main() {
  testWidgets(
    'history, live events, and reconnects do not duplicate messages',
    (tester) async {
      final history = _FakeHistoryApi();
      final transport = _FakeChatTransport();
      final controller = ChatController(history: history, transport: transport);

      await tester.pumpWidget(
        MaterialApp(
          home: ChatPage(sessionId: 'session-1', controller: controller),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('历史合成消息'), findsOneWidget);
      expect(history.afterSequences, <int>[0]);
      transport.emitMessage(_message(id: 'history-session-1', sequence: 1));
      await tester.pump();
      expect(find.text('历史合成消息'), findsOneWidget);

      await tester.enterText(find.byKey(const Key('chat-input')), '新的合成消息');
      await tester.pump();
      await tester.tap(find.byKey(const Key('chat-send')));
      await tester.pump();
      expect(transport.sentTexts, <String>['新的合成消息']);
      expect(find.text('新的合成消息'), findsOneWidget);

      transport.beginReconnect();
      await tester.pump();
      transport.finishReconnect();
      await tester.pumpAndSettle();
      expect(history.afterSequences, <int>[0, 2]);
      expect(find.text('重连补回消息'), findsOneWidget);
    },
  );

  test('switching sessions disconnects and removes the old history', () async {
    final history = _FakeHistoryApi();
    final transport = _FakeChatTransport();
    final controller = ChatController(history: history, transport: transport);

    await controller.connect('session-1');
    expect(controller.messages.single.sessionId, 'session-1');

    await controller.connect('session-2');

    expect(transport.stopCount, 1);
    expect(controller.messages, hasLength(1));
    expect(controller.messages.single.sessionId, 'session-2');
  });
}

ChatMessage _message({
  required String id,
  required int sequence,
  String sessionId = 'session-1',
}) => ChatMessage(
  id: id,
  sessionId: sessionId,
  senderKind: 'User',
  clientMessageId: 'client-$sequence',
  sequence: sequence,
  text: '历史合成消息',
  sentAt: DateTime.utc(2026, 8, 22, 12, sequence),
);

class _FakeHistoryApi implements ChatHistoryApi {
  final List<int> afterSequences = <int>[];

  @override
  Future<List<ChatMessage>> getMessages(
    String sessionId, {
    required int afterSequence,
  }) async {
    afterSequences.add(afterSequence);
    if (afterSequence == 0) {
      return <ChatMessage>[
        _message(id: 'history-$sessionId', sequence: 1, sessionId: sessionId),
      ];
    }
    return <ChatMessage>[
      ChatMessage(
        id: 'history-3',
        sessionId: sessionId,
        senderKind: 'Practitioner',
        clientMessageId: 'client-3',
        sequence: 3,
        text: '重连补回消息',
        sentAt: DateTime.utc(2026, 8, 22, 12, 3),
      ),
    ];
  }
}

class _FakeChatTransport implements ChatTransport {
  void Function(ChatMessage message)? _messageHandler;
  VoidCallback? _reconnectingHandler;
  Future<void> Function()? _reconnectedHandler;
  final List<String> sentTexts = <String>[];
  var _sequence = 1;
  var stopCount = 0;

  @override
  void onMessage(void Function(ChatMessage message) handler) {
    _messageHandler = handler;
  }

  @override
  void onPresence(void Function(ChatPresence presence) handler) {}

  @override
  void onReconnecting(VoidCallback handler) {
    _reconnectingHandler = handler;
  }

  @override
  void onReconnected(Future<void> Function() handler) {
    _reconnectedHandler = handler;
  }

  @override
  void onClosed(VoidCallback handler) {}

  @override
  Future<void> start() async {}

  @override
  Future<void> stop() async {
    stopCount += 1;
  }

  @override
  Future<void> join(String sessionId) async {}

  @override
  Future<ChatMessage> send(
    String sessionId,
    String text,
    String clientMessageId,
  ) async {
    sentTexts.add(text);
    _sequence += 1;
    return ChatMessage(
      id: 'sent-$_sequence',
      sessionId: sessionId,
      senderKind: 'User',
      clientMessageId: clientMessageId,
      sequence: _sequence,
      text: text,
      sentAt: DateTime.utc(2026, 8, 22, 12, _sequence),
    );
  }

  void emitMessage(ChatMessage message) => _messageHandler?.call(message);

  void beginReconnect() => _reconnectingHandler?.call();

  void finishReconnect() {
    _reconnectedHandler?.call();
  }
}
