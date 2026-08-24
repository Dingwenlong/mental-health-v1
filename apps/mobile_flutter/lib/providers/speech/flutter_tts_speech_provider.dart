import 'package:flutter_tts/flutter_tts.dart';

import 'speech_provider.dart';

class TtsVoice {
  const TtsVoice({
    required this.name,
    required this.locale,
    required this.networkRequired,
    this.installed = true,
  });

  final String name;
  final String locale;
  final bool networkRequired;
  final bool installed;
}

abstract interface class TtsEngine {
  Future<List<String>> getLanguages();

  Future<List<TtsVoice>> getVoices();

  Future<bool> isLanguageInstalled(String language);

  Future<bool> setLanguage(String language);

  Future<bool> setVoice(TtsVoice voice);

  Future<void> prepareForSpeech();

  Future<bool> speak(String text);

  Future<void> stop();
}

class FlutterTtsEngine implements TtsEngine {
  FlutterTtsEngine({FlutterTts? tts}) : _tts = tts ?? FlutterTts();

  final FlutterTts _tts;

  @override
  Future<List<String>> getLanguages() async {
    final raw = await _tts.getLanguages;
    if (raw is! Iterable) {
      return const <String>[];
    }
    return raw.map((value) => value.toString()).toList(growable: false);
  }

  @override
  Future<List<TtsVoice>> getVoices() async {
    final raw = await _tts.getVoices;
    if (raw is! Iterable) {
      return const <TtsVoice>[];
    }
    return raw
        .whereType<Map>()
        .map((value) {
          final voice = Map<String, dynamic>.from(value);
          final networkValue = voice['network_required'];
          final features = voice['features']
              ?.toString()
              .split('\t')
              .where((feature) => feature.isNotEmpty)
              .toSet();
          return TtsVoice(
            name: voice['name']?.toString() ?? '',
            locale: voice['locale']?.toString() ?? '',
            networkRequired:
                networkValue == true || networkValue?.toString() == '1',
            installed: features?.contains('notInstalled') != true,
          );
        })
        .where((voice) => voice.name.isNotEmpty && voice.locale.isNotEmpty)
        .toList(growable: false);
  }

  @override
  Future<bool> isLanguageInstalled(String language) async =>
      await _tts.isLanguageInstalled(language) == true;

  @override
  Future<void> prepareForSpeech() async {
    await _tts.awaitSpeakCompletion(true);
    await _tts.setSpeechRate(0.45);
    await _tts.setPitch(1.0);
    await _tts.setVolume(1.0);
  }

  @override
  Future<bool> setLanguage(String language) async =>
      _succeeded(await _tts.setLanguage(language));

  @override
  Future<bool> setVoice(TtsVoice voice) async => _succeeded(
    await _tts.setVoice(<String, String>{
      'name': voice.name,
      'locale': voice.locale,
    }),
  );

  @override
  Future<bool> speak(String text) async => _succeeded(await _tts.speak(text));

  @override
  Future<void> stop() async {
    await _tts.stop();
  }

  static bool _succeeded(Object? result) => result == true || result == 1;
}

class FlutterTtsSpeechProvider implements SpeechProvider {
  const FlutterTtsSpeechProvider({
    required this.engine,
    required this.fallback,
  });

  final TtsEngine engine;
  final SpeechProvider fallback;

  @override
  Future<SpeechResult> speak(String text) async {
    final normalized = text.trim();
    if (normalized.isEmpty) {
      throw ArgumentError.value(text, 'text');
    }

    try {
      final languages = await engine.getLanguages();
      final voices = await engine.getVoices();
      final localVoice = _selectLocalChineseVoice(languages, voices);
      if (localVoice == null ||
          !await engine.isLanguageInstalled(localVoice.locale) ||
          !await engine.setLanguage(localVoice.locale) ||
          !await engine.setVoice(localVoice)) {
        return await fallback.speak(normalized);
      }

      await engine.prepareForSpeech();
      if (!await engine.speak(normalized)) {
        await engine.stop();
        return await fallback.speak(normalized);
      }
      return const SpeechResult.spoken();
    } catch (_) {
      try {
        await engine.stop();
      } catch (_) {
        // The text is already visible. Continue with the local chime fallback.
      }
      return await fallback.speak(normalized);
    }
  }

  @override
  Future<void> stop() async {
    Object? firstError;
    StackTrace? firstStack;
    try {
      await engine.stop();
    } catch (error, stack) {
      firstError = error;
      firstStack = stack;
    }
    try {
      await fallback.stop();
    } catch (error, stack) {
      firstError ??= error;
      firstStack ??= stack;
    }
    if (firstError != null) {
      Error.throwWithStackTrace(firstError, firstStack!);
    }
  }

  static TtsVoice? _selectLocalChineseVoice(
    List<String> languages,
    List<TtsVoice> voices,
  ) {
    final available = languages.map(_normalizeLocale).toSet();
    final candidates =
        voices
            .where(
              (voice) =>
                  !voice.networkRequired &&
                  voice.installed &&
                  _isChinese(voice.locale) &&
                  available.contains(_normalizeLocale(voice.locale)),
            )
            .toList(growable: false)
          ..sort((left, right) {
            final leftPriority = _localePriority(left.locale);
            final rightPriority = _localePriority(right.locale);
            return leftPriority != rightPriority
                ? leftPriority.compareTo(rightPriority)
                : left.name.compareTo(right.name);
          });
    return candidates.firstOrNull;
  }

  static bool _isChinese(String locale) {
    final normalized = _normalizeLocale(locale);
    return normalized == 'zh' || normalized.startsWith('zh-');
  }

  static String _normalizeLocale(String value) =>
      value.trim().replaceAll('_', '-').toLowerCase();

  static int _localePriority(String locale) =>
      switch (_normalizeLocale(locale)) {
        'zh-cn' => 0,
        'zh-hans-cn' => 1,
        'zh-hans' => 2,
        'zh' => 3,
        _ => 4,
      };
}
