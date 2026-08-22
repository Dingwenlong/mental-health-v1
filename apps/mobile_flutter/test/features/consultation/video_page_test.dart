import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/consultation/video_page.dart';
import 'package:mobile_flutter/providers/realtime_media/realtime_media_provider.dart';

void main() {
  testWidgets('camera denial keeps the text fallback available', (
    tester,
  ) async {
    final media = _FakeRealtimeMediaProvider(MediaStartResult.permissionDenied);
    var switchedToText = false;
    await tester.pumpWidget(
      MaterialApp(
        home: VideoPage(
          sessionId: 'session-1',
          media: media,
          onSwitchToText: () => switchedToText = true,
        ),
      ),
    );

    await tester.tap(find.byKey(const Key('video-start')));
    await tester.pump();

    expect(find.text('没有相机或麦克风权限。你可以改用文字咨询。'), findsOneWidget);
    expect(find.text('转为文字咨询'), findsOneWidget);
    expect(find.text('视频已连接'), findsNothing);
    await tester.tap(find.byKey(const Key('video-switch-to-text')));
    expect(switchedToText, isTrue);
  });

  testWidgets('connection failure is shown and resources close on dispose', (
    tester,
  ) async {
    final media = _FakeRealtimeMediaProvider(MediaStartResult.failed);
    await tester.pumpWidget(
      MaterialApp(
        home: VideoPage(
          sessionId: 'session-1',
          media: media,
          onSwitchToText: () {},
        ),
      ),
    );

    await tester.tap(find.byKey(const Key('video-start')));
    await tester.pump();
    expect(find.text('视频没有连上'), findsOneWidget);
    expect(find.text('视频已连接'), findsNothing);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
    expect(media.closeCount, 1);
  });

  testWidgets('recording upload failure is shown after the call ends', (
    tester,
  ) async {
    final media = _FakeRealtimeMediaProvider(MediaStartResult.connected)
      ..failUploadOnClose = true;
    await tester.pumpWidget(
      MaterialApp(
        home: VideoPage(
          sessionId: 'session-1',
          media: media,
          onSwitchToText: () {},
        ),
      ),
    );

    await tester.tap(find.byKey(const Key('video-start')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('video-end')));
    await tester.pump();

    expect(find.text('录像没有上传成功。请联系工作人员。'), findsOneWidget);
  });
}

class _FakeRealtimeMediaProvider extends RealtimeMediaProvider {
  _FakeRealtimeMediaProvider(this.result);

  final MediaStartResult result;
  var closeCount = 0;
  bool failUploadOnClose = false;

  @override
  bool uploadFailed = false;

  @override
  MediaRoomState state = MediaRoomState.idle;

  @override
  Widget buildLocalPreview() => const ColoredBox(color: Colors.blue);

  @override
  Widget buildRemotePreview() => const ColoredBox(color: Colors.green);

  @override
  Future<MediaStartResult> start(String sessionId) async {
    state = switch (result) {
      MediaStartResult.connected => MediaRoomState.connected,
      MediaStartResult.permissionDenied => MediaRoomState.idle,
      MediaStartResult.failed => MediaRoomState.failed,
    };
    notifyListeners();
    return result;
  }

  @override
  Future<void> close() async {
    closeCount += 1;
    uploadFailed = failUploadOnClose;
    state = MediaRoomState.closed;
    notifyListeners();
  }
}
