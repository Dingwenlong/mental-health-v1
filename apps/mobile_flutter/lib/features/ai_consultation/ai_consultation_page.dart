import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import '../../generated/ui_copy.g.dart';
import '../../providers/speech/speech_provider.dart';
import 'avatar_painter.dart';
import 'crisis_help_sheet.dart';

class AiTurnResult {
  const AiTurnResult({required this.replyText, required this.isCrisis});

  final String replyText;
  final bool isCrisis;
}

abstract interface class AiTurnGateway {
  Future<AiTurnResult> sendTurn({
    required String sessionId,
    required String text,
    required String clientMessageId,
  });
}

class ApiAiTurnGateway implements AiTurnGateway {
  const ApiAiTurnGateway(this._client);

  final ApiClient _client;

  @override
  Future<AiTurnResult> sendTurn({
    required String sessionId,
    required String text,
    required String clientMessageId,
  }) async {
    final response = await _client.post(
      'consultations/$sessionId/ai-turns',
      data: <String, dynamic>{'text': text, 'clientMessageId': clientMessageId},
    );
    final rawReply = response['reply'];
    if (rawReply is! Map ||
        rawReply['text']?.toString().trim().isEmpty != false) {
      throw const ApiFailure(
        code: 'INVALID_RESPONSE',
        message: 'The AI reply is missing.',
      );
    }
    return AiTurnResult(
      replyText: rawReply['text'].toString(),
      isCrisis: response['isCrisis'] == true,
    );
  }
}

class AiConsultationPage extends StatefulWidget {
  const AiConsultationPage({
    required this.sessionId,
    required this.gateway,
    required this.speech,
    super.key,
  });

  final String sessionId;
  final AiTurnGateway gateway;
  final SpeechProvider speech;

  @override
  State<AiConsultationPage> createState() => _AiConsultationPageState();
}

class _AiConsultationPageState extends State<AiConsultationPage> {
  final TextEditingController _input = TextEditingController();
  final List<_DisplayedMessage> _messages = <_DisplayedMessage>[
    _DisplayedMessage.assistant(UiCopy.get('ai.fallback')),
  ];
  AvatarState _avatarState = AvatarState.idle;
  bool _requestInFlight = false;
  bool _isCrisis = false;
  bool _speechFallbackVisible = false;
  bool _sendFailed = false;
  int _messageCounter = 0;
  int _speechGeneration = 0;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(UiCopy.get('ai.title'))),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
          child: Column(
            children: <Widget>[
              Card(
                margin: EdgeInsets.zero,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      const Icon(Icons.info_outline),
                      const SizedBox(width: 10),
                      Expanded(child: Text(UiCopy.get('ai.identity'))),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 12),
              _Avatar(state: _avatarState),
              const SizedBox(height: 8),
              Expanded(
                child: ListView.builder(
                  key: const Key('ai-messages'),
                  itemCount: _messages.length,
                  itemBuilder: (context, index) =>
                      _MessageBubble(message: _messages[index]),
                ),
              ),
              if (_speechFallbackVisible) ...<Widget>[
                Text(
                  UiCopy.get('ai.speechUnavailable'),
                  key: const Key('ai-speech-fallback'),
                ),
                const SizedBox(height: 8),
              ],
              if (_sendFailed) ...<Widget>[
                Text(
                  UiCopy.get('ai.sendFailed'),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
                const SizedBox(height: 8),
              ],
              if (_isCrisis)
                const CrisisHelpSheet()
              else
                Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: <Widget>[
                    Expanded(
                      child: TextField(
                        key: const Key('ai-input'),
                        controller: _input,
                        enabled: !_requestInFlight,
                        maxLength: 4000,
                        minLines: 1,
                        maxLines: 4,
                        textInputAction: TextInputAction.send,
                        onSubmitted: (_) => _send(),
                        decoration: InputDecoration(
                          hintText: UiCopy.get('ai.messageHint'),
                          counterText: '',
                          border: const OutlineInputBorder(),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    IconButton.filled(
                      key: const Key('ai-send'),
                      tooltip: UiCopy.get('ai.send'),
                      onPressed: _requestInFlight ? null : _send,
                      icon: const Icon(Icons.send),
                    ),
                  ],
                ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _send() async {
    final text = _input.text.trim();
    if (text.isEmpty || _requestInFlight || _isCrisis) {
      return;
    }

    final generation = ++_speechGeneration;
    setState(() {
      _requestInFlight = true;
      _sendFailed = false;
      _speechFallbackVisible = false;
      _avatarState = AvatarState.listening;
      _messages.add(_DisplayedMessage.user(text));
      _input.clear();
    });
    await _stopSpeechSilently();

    try {
      final result = await widget.gateway.sendTurn(
        sessionId: widget.sessionId,
        text: text,
        clientMessageId:
            'mobile-ai-${DateTime.now().microsecondsSinceEpoch}-${++_messageCounter}',
      );
      if (!mounted || generation != _speechGeneration) {
        return;
      }
      setState(() {
        _messages.add(_DisplayedMessage.assistant(result.replyText));
        _requestInFlight = false;
        if (result.isCrisis) {
          _isCrisis = true;
          _avatarState = AvatarState.alert;
        } else {
          _avatarState = AvatarState.speaking;
        }
      });
      if (!result.isCrisis) {
        unawaited(_readReply(result.replyText, generation));
      }
    } catch (_) {
      if (!mounted || generation != _speechGeneration) {
        return;
      }
      setState(() {
        _requestInFlight = false;
        _sendFailed = true;
        _avatarState = AvatarState.idle;
      });
    }
  }

  Future<void> _readReply(String text, int generation) async {
    try {
      final speechResult = await widget.speech.speak(text);
      if (!mounted || generation != _speechGeneration || _isCrisis) {
        return;
      }
      setState(() {
        _speechFallbackVisible = speechResult.usedTextOnlyFallback;
        _avatarState = AvatarState.idle;
      });
    } catch (_) {
      if (!mounted || generation != _speechGeneration || _isCrisis) {
        return;
      }
      setState(() {
        _speechFallbackVisible = true;
        _avatarState = AvatarState.idle;
      });
    }
  }

  Future<void> _stopSpeechSilently() async {
    try {
      await widget.speech.stop();
    } catch (_) {
      // Sending and crisis handling must continue even if audio cleanup fails.
    }
  }

  @override
  void dispose() {
    _speechGeneration += 1;
    unawaited(_stopSpeechSilently());
    _input.dispose();
    super.dispose();
  }
}

class _Avatar extends StatelessWidget {
  const _Avatar({required this.state});

  final AvatarState state;

  @override
  Widget build(BuildContext context) {
    final stateName = state.name;
    return Semantics(
      label: UiCopy.get('ai.state.$stateName'),
      child: Column(
        children: <Widget>[
          SizedBox.square(
            key: Key('avatar-$stateName'),
            dimension: 112,
            child: CustomPaint(painter: AvatarPainter(state)),
          ),
          Text(UiCopy.get('ai.state.$stateName')),
        ],
      ),
    );
  }
}

class _DisplayedMessage {
  const _DisplayedMessage({required this.text, required this.isUser});

  factory _DisplayedMessage.user(String text) =>
      _DisplayedMessage(text: text, isUser: true);

  factory _DisplayedMessage.assistant(String text) =>
      _DisplayedMessage(text: text, isUser: false);

  final String text;
  final bool isUser;
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({required this.message});

  final _DisplayedMessage message;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Align(
      alignment: message.isUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        constraints: const BoxConstraints(maxWidth: 320),
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        decoration: BoxDecoration(
          color: message.isUser
              ? colors.primaryContainer
              : colors.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Text(message.text),
      ),
    );
  }
}
