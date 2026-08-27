import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../../core/api/api_client.dart';

enum ChatConnectionStatus { disconnected, connecting, connected, reconnecting }

class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.sessionId,
    required this.senderKind,
    required this.clientMessageId,
    required this.sequence,
    required this.text,
    required this.sentAt,
  });

  final String id;
  final String sessionId;
  final String senderKind;
  final String clientMessageId;
  final int sequence;
  final String text;
  final DateTime sentAt;

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
    id: json['id'].toString(),
    sessionId: json['sessionId'].toString(),
    senderKind: json['senderKind'].toString(),
    clientMessageId: json['clientMessageId'].toString(),
    sequence: (json['sequence'] as num).toInt(),
    text: json['text'].toString(),
    sentAt: DateTime.parse(json['sentAt'].toString()),
  );
}

class ChatPresence {
  const ChatPresence({
    required this.userId,
    required this.kind,
    required this.online,
  });

  final String userId;
  final String kind;
  final bool online;

  factory ChatPresence.fromJson(Map<String, dynamic> json) => ChatPresence(
    userId: json['userId'].toString(),
    kind: json['kind'].toString(),
    online: json['online'] as bool,
  );
}

abstract interface class ChatHistoryApi {
  Future<List<ChatMessage>> getMessages(
    String sessionId, {
    required int afterSequence,
  });
}

abstract interface class ChatTransport {
  void onMessage(void Function(ChatMessage message) handler);

  void onPresence(void Function(ChatPresence presence) handler);

  void onReconnecting(VoidCallback handler);

  void onReconnected(Future<void> Function() handler);

  void onClosed(VoidCallback handler);

  Future<void> start();

  Future<void> stop();

  Future<void> join(String sessionId);

  Future<ChatMessage> send(
    String sessionId,
    String text,
    String clientMessageId,
  );
}

class ChatController extends ChangeNotifier {
  ChatController({required this.history, required this.transport}) {
    transport.onMessage(_mergeMessage);
    transport.onPresence(_updatePresence);
    transport.onReconnecting(() {
      status = ChatConnectionStatus.reconnecting;
      notifyListeners();
    });
    transport.onReconnected(_afterReconnect);
    transport.onClosed(() {
      status = ChatConnectionStatus.disconnected;
      notifyListeners();
    });
  }

  final ChatHistoryApi history;
  final ChatTransport transport;
  final Map<String, ChatMessage> _messages = <String, ChatMessage>{};
  final Map<String, ChatPresence> _presences = <String, ChatPresence>{};
  String? _sessionId;

  ChatConnectionStatus status = ChatConnectionStatus.disconnected;

  List<ChatMessage> get messages {
    final result = _messages.values.toList(growable: false);
    result.sort((left, right) => left.sequence.compareTo(right.sequence));
    return result;
  }

  List<ChatPresence> get presences => _presences.values.toList(growable: false);

  int get lastSequence => _messages.values.fold(
    0,
    (current, message) =>
        message.sequence > current ? message.sequence : current,
  );

  Future<void> connect(String sessionId) async {
    final normalized = sessionId.trim();
    if (normalized.isEmpty) {
      throw ArgumentError.value(sessionId, 'sessionId');
    }
    if (status == ChatConnectionStatus.connected && _sessionId == normalized) {
      return;
    }
    if (_sessionId != null && _sessionId != normalized) {
      _sessionId = null;
      _messages.clear();
      _presences.clear();
      status = ChatConnectionStatus.disconnected;
      notifyListeners();
      await transport.stop();
    }
    _sessionId = normalized;
    status = ChatConnectionStatus.connecting;
    notifyListeners();
    try {
      await transport.start();
      await transport.join(normalized);
      await _backfill();
      status = ChatConnectionStatus.connected;
      notifyListeners();
    } catch (_) {
      status = ChatConnectionStatus.disconnected;
      notifyListeners();
      rethrow;
    }
  }

  Future<void> disconnect() async {
    _sessionId = null;
    await transport.stop();
    status = ChatConnectionStatus.disconnected;
    notifyListeners();
  }

  Future<void> send(String text, String clientMessageId) async {
    final sessionId = _sessionId;
    final normalized = text.trim();
    if (sessionId == null ||
        status != ChatConnectionStatus.connected ||
        normalized.isEmpty) {
      throw StateError('Chat is not ready.');
    }
    final message = await transport.send(
      sessionId,
      normalized,
      clientMessageId,
    );
    _mergeMessage(message);
  }

  Future<void> _afterReconnect() async {
    final sessionId = _sessionId;
    if (sessionId == null) {
      return;
    }
    try {
      await transport.join(sessionId);
      await _backfill();
      status = ChatConnectionStatus.connected;
      notifyListeners();
    } catch (_) {
      status = ChatConnectionStatus.disconnected;
      notifyListeners();
    }
  }

  Future<void> _backfill() async {
    final sessionId = _sessionId;
    if (sessionId == null) {
      return;
    }
    final missed = await history.getMessages(
      sessionId,
      afterSequence: lastSequence,
    );
    var changed = false;
    for (final message in missed) {
      if (_messages.containsKey(message.id)) {
        continue;
      }
      _messages[message.id] = message;
      changed = true;
    }
    if (changed) {
      notifyListeners();
    }
  }

  void _mergeMessage(ChatMessage message) {
    if (_messages.containsKey(message.id)) {
      return;
    }
    _messages[message.id] = message;
    notifyListeners();
  }

  void _updatePresence(ChatPresence presence) {
    _presences[presence.userId] = presence;
    notifyListeners();
  }
}

class ApiChatHistory implements ChatHistoryApi {
  const ApiChatHistory(this._client);

  final ApiClient _client;

  @override
  Future<List<ChatMessage>> getMessages(
    String sessionId, {
    required int afterSequence,
  }) async {
    final response = await _client.getList(
      'consultations/$sessionId/messages?afterSequence=$afterSequence',
    );
    return response
        .map(
          (item) =>
              ChatMessage.fromJson(Map<String, dynamic>.from(item as Map)),
        )
        .toList(growable: false);
  }
}

class SignalRChatTransport implements ChatTransport {
  SignalRChatTransport({
    required String hubUrl,
    required AccessTokenReader readAccessToken,
  }) : _connection = HubConnectionBuilder()
           .withUrl(
             hubUrl,
             options: HttpConnectionOptions(
               accessTokenFactory: () async {
                 final token = await readAccessToken();
                 if (token == null || token.isEmpty) {
                   throw StateError('Access token is missing.');
                 }
                 return token;
               },
             ),
           )
           .withAutomaticReconnect(retryDelays: <int>[0, 2000, 5000, 10000])
           .build();

  final HubConnection _connection;

  @override
  void onMessage(void Function(ChatMessage message) handler) {
    _connection.on('MessageReceived', (arguments) {
      final value = arguments?.firstOrNull;
      if (value is Map) {
        handler(ChatMessage.fromJson(Map<String, dynamic>.from(value)));
      }
    });
  }

  @override
  void onPresence(void Function(ChatPresence presence) handler) {
    _connection.on('PresenceChanged', (arguments) {
      final value = arguments?.firstOrNull;
      if (value is Map) {
        handler(ChatPresence.fromJson(Map<String, dynamic>.from(value)));
      }
    });
  }

  @override
  void onReconnecting(VoidCallback handler) {
    _connection.onreconnecting(({error}) => handler());
  }

  @override
  void onReconnected(Future<void> Function() handler) {
    _connection.onreconnected(({connectionId}) => unawaited(handler()));
  }

  @override
  void onClosed(VoidCallback handler) {
    _connection.onclose(({error}) => handler());
  }

  @override
  Future<void> start() async {
    await _connection.start();
  }

  @override
  Future<void> stop() async {
    await _connection.stop();
  }

  @override
  Future<void> join(String sessionId) async {
    await _connection.invoke('JoinSession', args: <Object>[sessionId]);
  }

  @override
  Future<ChatMessage> send(
    String sessionId,
    String text,
    String clientMessageId,
  ) async {
    final response = await _connection.invoke(
      'SendMessage',
      args: <Object>[sessionId, text, clientMessageId],
    );
    if (response is! Map) {
      throw StateError('SignalR returned an invalid message.');
    }
    return ChatMessage.fromJson(Map<String, dynamic>.from(response));
  }
}

class StartedChatSession {
  const StartedChatSession({required this.sessionId, required this.controller});

  final String sessionId;
  final ChatController controller;
}

abstract interface class ChatSessionLauncher {
  Future<StartedChatSession> startHumanChat(String orderId);
  Future<StartedChatSession> resumeHumanChat(
    String sessionId, {
    required bool start,
  });
}

class ApiChatSessionLauncher implements ChatSessionLauncher {
  const ApiChatSessionLauncher(this._client, this._hubUrl);

  final ApiClient _client;
  final String _hubUrl;

  @override
  Future<StartedChatSession> startHumanChat(String orderId) async {
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
        'idempotencyKey': 'mobile-session-$orderId',
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
  Future<StartedChatSession> resumeHumanChat(
    String sessionId, {
    required bool start,
  }) async {
    if (start) await _client.post('consultations/$sessionId/start');
    return _existingSession(sessionId);
  }

  StartedChatSession _existingSession(String sessionId) => StartedChatSession(
    sessionId: sessionId,
    controller: ChatController(
      history: ApiChatHistory(_client),
      transport: SignalRChatTransport(
        hubUrl: _hubUrl,
        readAccessToken: _client.readAccessToken,
      ),
    ),
  );
}
