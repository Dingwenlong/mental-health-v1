import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/providers/speech/chime_fallback_speech_provider.dart';
import 'package:mobile_flutter/providers/speech/flutter_tts_speech_provider.dart';
import 'package:mobile_flutter/providers/speech/speech_provider.dart';

void main() {
  test(
    'uses an installed Chinese voice that does not need the network',
    () async {
      final engine = _FakeTtsEngine(
        languages: const <String>['en-US', 'zh-CN'],
        voices: const <TtsVoice>[
          TtsVoice(name: 'online-zh', locale: 'zh-CN', networkRequired: true),
          TtsVoice(name: 'local-zh', locale: 'zh-CN', networkRequired: false),
        ],
        installedLanguages: const <String>{'zh-CN'},
      );
      final fallback = _FakeSpeechProvider();
      final provider = FlutterTtsSpeechProvider(
        engine: engine,
        fallback: fallback,
      );

      final result = await provider.speak('你好');

      expect(result, const SpeechResult.spoken());
      expect(engine.selectedLanguage, 'zh-CN');
      expect(engine.selectedVoice?.name, 'local-zh');
      expect(engine.spokenTexts, <String>['你好']);
      expect(fallback.spokenTexts, isEmpty);
    },
  );

  test('does not use a Chinese voice that requires the network', () async {
    final engine = _FakeTtsEngine(
      languages: const <String>['zh-CN'],
      voices: const <TtsVoice>[
        TtsVoice(name: 'online-zh', locale: 'zh-CN', networkRequired: true),
      ],
      installedLanguages: const <String>{'zh-CN'},
    );
    final fallback = _FakeSpeechProvider(
      result: const SpeechResult.textOnlyWithChime(),
    );
    final provider = FlutterTtsSpeechProvider(
      engine: engine,
      fallback: fallback,
    );

    final result = await provider.speak('你好');

    expect(result, const SpeechResult.textOnlyWithChime());
    expect(engine.spokenTexts, isEmpty);
    expect(fallback.spokenTexts, <String>['你好']);
  });

  test('does not select a local Chinese voice that is not installed', () async {
    final engine = _FakeTtsEngine(
      languages: const <String>['zh-CN'],
      voices: const <TtsVoice>[
        TtsVoice(
          name: 'missing-local-zh',
          locale: 'zh-CN',
          networkRequired: false,
          installed: false,
        ),
      ],
      installedLanguages: const <String>{'zh-CN'},
    );
    final fallback = _FakeSpeechProvider(
      result: const SpeechResult.textOnlyWithChime(),
    );
    final provider = FlutterTtsSpeechProvider(
      engine: engine,
      fallback: fallback,
    );

    final result = await provider.speak('你好');

    expect(result, const SpeechResult.textOnlyWithChime());
    expect(engine.selectedVoice, isNull);
    expect(engine.spokenTexts, isEmpty);
    expect(fallback.spokenTexts, <String>['你好']);
  });

  test('falls back when no installed Chinese language exists', () async {
    final fallback = _FakeSpeechProvider(
      result: const SpeechResult.textOnlyWithChime(),
    );
    final provider = FlutterTtsSpeechProvider(
      engine: _FakeTtsEngine(
        languages: const <String>['en-US'],
        voices: const <TtsVoice>[
          TtsVoice(name: 'local-en', locale: 'en-US', networkRequired: false),
        ],
        installedLanguages: const <String>{'en-US'},
      ),
      fallback: fallback,
    );

    final result = await provider.speak('你好');

    expect(result, const SpeechResult.textOnlyWithChime());
    expect(fallback.spokenTexts, <String>['你好']);
  });

  test('the fallback provider plays and stops the bundled chime', () async {
    final player = _FakeChimePlayer();
    final provider = ChimeFallbackSpeechProvider(player: player);

    expect(
      await provider.speak('文字仍然显示'),
      const SpeechResult.textOnlyWithChime(),
    );
    await provider.stop();

    expect(player.playCount, 1);
    expect(player.stopCount, 1);
  });
}

class _FakeTtsEngine implements TtsEngine {
  _FakeTtsEngine({
    required this.languages,
    required this.voices,
    required this.installedLanguages,
  });

  final List<String> languages;
  final List<TtsVoice> voices;
  final Set<String> installedLanguages;
  final List<String> spokenTexts = <String>[];
  String? selectedLanguage;
  TtsVoice? selectedVoice;

  @override
  Future<List<String>> getLanguages() async => languages;

  @override
  Future<List<TtsVoice>> getVoices() async => voices;

  @override
  Future<bool> isLanguageInstalled(String language) async =>
      installedLanguages.contains(language);

  @override
  Future<bool> setLanguage(String language) async {
    selectedLanguage = language;
    return true;
  }

  @override
  Future<bool> setVoice(TtsVoice voice) async {
    selectedVoice = voice;
    return true;
  }

  @override
  Future<void> prepareForSpeech() async {}

  @override
  Future<bool> speak(String text) async {
    spokenTexts.add(text);
    return true;
  }

  @override
  Future<void> stop() async {}
}

class _FakeSpeechProvider implements SpeechProvider {
  _FakeSpeechProvider({this.result = const SpeechResult.spoken()});

  final SpeechResult result;
  final List<String> spokenTexts = <String>[];

  @override
  Future<SpeechResult> speak(String text) async {
    spokenTexts.add(text);
    return result;
  }

  @override
  Future<void> stop() async {}
}

class _FakeChimePlayer implements ChimePlayer {
  int playCount = 0;
  int stopCount = 0;

  @override
  Future<void> play() async {
    playCount += 1;
  }

  @override
  Future<void> stop() async {
    stopCount += 1;
  }
}
