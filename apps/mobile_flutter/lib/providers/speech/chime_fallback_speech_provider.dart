import 'package:audioplayers/audioplayers.dart';

import 'speech_provider.dart';

abstract interface class ChimePlayer {
  Future<void> play();

  Future<void> stop();
}

class AssetChimePlayer implements ChimePlayer {
  AssetChimePlayer({AudioPlayer? player}) : _player = player ?? AudioPlayer();

  final AudioPlayer _player;

  @override
  Future<void> play() async {
    await _player.stop();
    await _player.play(AssetSource('audio/fallback-chime.wav'));
  }

  @override
  Future<void> stop() => _player.stop();
}

class ChimeFallbackSpeechProvider implements SpeechProvider {
  const ChimeFallbackSpeechProvider({required this.player});

  final ChimePlayer player;

  @override
  Future<SpeechResult> speak(String text) async {
    if (text.trim().isEmpty) {
      throw ArgumentError.value(text, 'text');
    }
    await player.play();
    return const SpeechResult.textOnlyWithChime();
  }

  @override
  Future<void> stop() => player.stop();
}
