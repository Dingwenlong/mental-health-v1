import 'dart:async';

import 'package:flutter/material.dart';

import '../../generated/ui_copy.g.dart';
import '../../providers/realtime_media/realtime_media_provider.dart';

class VideoPage extends StatefulWidget {
  const VideoPage({
    required this.sessionId,
    required this.media,
    required this.onSwitchToText,
    super.key,
  });

  final String sessionId;
  final RealtimeMediaProvider media;
  final VoidCallback onSwitchToText;

  @override
  State<VideoPage> createState() => _VideoPageState();
}

class _VideoPageState extends State<VideoPage> {
  bool _starting = false;
  bool _permissionDenied = false;

  @override
  void initState() {
    super.initState();
    widget.media.addListener(_mediaChanged);
  }

  @override
  void dispose() {
    widget.media.removeListener(_mediaChanged);
    unawaited(widget.media.close());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = widget.media.state;
    final showingVideo =
        state == MediaRoomState.signaling || state == MediaRoomState.connected;
    final canStart =
        !_starting &&
        (state == MediaRoomState.idle ||
            state == MediaRoomState.failed ||
            state == MediaRoomState.closed);

    return Scaffold(
      appBar: AppBar(title: Text(UiCopy.get('video.title'))),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: <Widget>[
            if (showingVideo) ...<Widget>[
              AspectRatio(
                aspectRatio: 16 / 9,
                child: Stack(
                  fit: StackFit.expand,
                  children: <Widget>[
                    Semantics(
                      label: UiCopy.get('video.remote'),
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(12),
                        child: widget.media.buildRemotePreview(),
                      ),
                    ),
                    Positioned(
                      right: 12,
                      bottom: 12,
                      width: 112,
                      height: 150,
                      child: Semantics(
                        label: UiCopy.get('video.local'),
                        child: ClipRRect(
                          borderRadius: BorderRadius.circular(10),
                          child: widget.media.buildLocalPreview(),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
            ],
            Text(
              _statusText(state),
              key: const Key('video-status'),
              style: Theme.of(context).textTheme.titleMedium,
            ),
            if (widget.media.uploadFailed) ...<Widget>[
              const SizedBox(height: 8),
              Text(
                UiCopy.get('video.uploadFailed'),
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            const SizedBox(height: 16),
            if (canStart)
              FilledButton(
                key: const Key('video-start'),
                onPressed: _start,
                child: Text(UiCopy.get('video.start')),
              ),
            if (state == MediaRoomState.signaling ||
                state == MediaRoomState.connected)
              OutlinedButton(
                key: const Key('video-end'),
                onPressed: widget.media.close,
                child: Text(UiCopy.get('video.end')),
              ),
            if (_permissionDenied ||
                state == MediaRoomState.failed) ...<Widget>[
              const SizedBox(height: 8),
              TextButton(
                key: const Key('video-switch-to-text'),
                onPressed: widget.onSwitchToText,
                child: Text(UiCopy.get('video.switchToText')),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _start() async {
    setState(() {
      _starting = true;
      _permissionDenied = false;
    });
    final result = await widget.media.start(widget.sessionId);
    if (!mounted) return;
    setState(() {
      _starting = false;
      _permissionDenied = result == MediaStartResult.permissionDenied;
    });
  }

  void _mediaChanged() {
    if (mounted) setState(() {});
  }

  String _statusText(MediaRoomState state) {
    if (_permissionDenied) return UiCopy.get('video.permissionDenied');
    final key = switch (state) {
      MediaRoomState.idle => 'video.start',
      MediaRoomState.requestingPermissions => 'video.requestingPermissions',
      MediaRoomState.signaling => 'video.signaling',
      MediaRoomState.connected => 'video.connected',
      MediaRoomState.failed => 'video.failed',
      MediaRoomState.closed => 'video.closed',
    };
    return UiCopy.get(key);
  }
}
