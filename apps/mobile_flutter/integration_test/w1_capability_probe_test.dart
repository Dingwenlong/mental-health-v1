import 'dart:async';

import 'package:flutter_tts/flutter_tts.dart';
import 'package:flutter_webrtc/flutter_webrtc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:signalr_netcore/signalr_client.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('development SignalR hub echoes a value', (tester) async {
    const hubUrl = String.fromEnvironment(
      'PROBE_HUB_URL',
      defaultValue: 'http://10.0.2.2:5165/hubs/development-probe',
    );
    final connection = HubConnectionBuilder().withUrl(hubUrl).build();

    try {
      await connection.start();
      final result = await connection.invoke(
        'Echo',
        args: <Object>['w1-probe'],
      );

      expect(result, 'w1-probe');
    } finally {
      await connection.stop();
    }
  });

  testWidgets('two local WebRTC peers complete offer and answer', (
    tester,
  ) async {
    final first = await createPeerConnection({'iceServers': <Object>[]});
    final second = await createPeerConnection({'iceServers': <Object>[]});
    RTCDataChannel? dataChannel;

    try {
      first.onIceCandidate = (candidate) {
        second.addCandidate(candidate);
      };
      second.onIceCandidate = (candidate) {
        first.addCandidate(candidate);
      };
      dataChannel = await first.createDataChannel(
        'w1-probe',
        RTCDataChannelInit(),
      );

      final offer = await first.createOffer();
      await first.setLocalDescription(offer);
      await second.setRemoteDescription(offer);
      final answer = await second.createAnswer();
      await second.setLocalDescription(answer);
      await first.setRemoteDescription(answer);

      expect((await first.getLocalDescription())?.type, 'offer');
      expect((await first.getRemoteDescription())?.type, 'answer');
      expect((await second.getRemoteDescription())?.type, 'offer');
      expect((await second.getLocalDescription())?.type, 'answer');
    } finally {
      await dataChannel?.close();
      await first.close();
      await second.close();
    }
  });

  testWidgets('camera capture returns an error when Android blocks it', (
    tester,
  ) async {
    Object? captureError;
    MediaStream? stream;

    try {
      stream = await navigator.mediaDevices
          .getUserMedia(<String, Object>{
            'audio': false,
            'video': <String, Object>{'facingMode': 'user'},
          })
          .timeout(const Duration(seconds: 10));
    } catch (error) {
      captureError = error;
    } finally {
      for (final track in stream?.getTracks() ?? <MediaStreamTrack>[]) {
        await track.stop();
      }
      await stream?.dispose();
    }

    expect(captureError, isNotNull);
    expect(captureError, isNot(isA<TimeoutException>()));
    expect(captureError.toString(), contains('NotAllowedError'));
  });

  testWidgets('offline Chinese TTS capability is available', (tester) async {
    final tts = FlutterTts();

    try {
      final engines = List<Object?>.from(await tts.getEngines as List);
      final languages = List<Object?>.from(await tts.getLanguages as List);
      final chineseAvailable = await tts.isLanguageAvailable('zh-CN');

      expect(
        engines.map((engine) => engine.toString()),
        contains('com.google.android.tts'),
      );
      expect(chineseAvailable, isTrue);
      expect(
        languages.any(
          (language) => language.toString().toLowerCase().startsWith('zh'),
        ),
        isTrue,
      );
    } finally {
      await tts.stop();
    }
  });
}
