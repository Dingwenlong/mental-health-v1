enum SpeechDelivery { spoken, textOnlyWithChime }

class SpeechResult {
  const SpeechResult.spoken() : delivery = SpeechDelivery.spoken;

  const SpeechResult.textOnlyWithChime()
    : delivery = SpeechDelivery.textOnlyWithChime;

  final SpeechDelivery delivery;

  bool get usedTextOnlyFallback => delivery == SpeechDelivery.textOnlyWithChime;

  @override
  bool operator ==(Object other) =>
      other is SpeechResult && other.delivery == delivery;

  @override
  int get hashCode => delivery.hashCode;
}

abstract interface class SpeechProvider {
  Future<SpeechResult> speak(String text);

  Future<void> stop();
}
