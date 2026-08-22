import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_webrtc/flutter_webrtc.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../../core/api/api_client.dart';
import 'media_recording_uploader.dart';
import 'realtime_media_provider.dart';

class FlutterWebRtcMediaProvider extends RealtimeMediaProvider {
  FlutterWebRtcMediaProvider(
    this._hubUrl,
    this._readAccessToken,
    this._uploader,
  );

  final String _hubUrl;
  final AccessTokenReader _readAccessToken;
  final MediaRecordingUploader _uploader;
  final List<RTCIceCandidate> _pendingCandidates = <RTCIceCandidate>[];

  MediaRoomState _state = MediaRoomState.idle;
  RTCVideoRenderer? _localRenderer;
  RTCVideoRenderer? _remoteRenderer;
  RTCPeerConnection? _peer;
  MediaStream? _localStream;
  MediaStream? _remoteStream;
  HubConnection? _connection;
  MediaRecorder? _recorder;
  File? _recordingFile;
  String? _recordingKey;
  String? _sessionId;
  Completer<MediaStartResult>? _connected;
  bool _wasConnected = false;
  bool _uploadFailed = false;
  bool _closing = false;
  int _attempt = 0;
  Future<void>? _releaseInProgress;

  @override
  MediaRoomState get state => _state;

  @override
  bool get uploadFailed => _uploadFailed;

  @override
  Future<MediaStartResult> start(String sessionId) async {
    final normalized = sessionId.trim();
    if (normalized.isEmpty) return MediaStartResult.failed;

    final attempt = ++_attempt;
    _closing = false;
    await _release(uploadRecording: false);
    if (attempt != _attempt) return MediaStartResult.failed;
    _wasConnected = false;
    _uploadFailed = false;
    _setState(MediaRoomState.requestingPermissions);

    try {
      final permissions = await <Permission>[
        Permission.camera,
        Permission.microphone,
      ].request();
      if (permissions.values.any((status) => !status.isGranted)) {
        if (attempt == _attempt) _setState(MediaRoomState.idle);
        return MediaStartResult.permissionDenied;
      }
      if (attempt != _attempt) return MediaStartResult.failed;

      _sessionId = normalized;
      _connected = Completer<MediaStartResult>();
      await _initializeRenderers();
      final localStream = await navigator.mediaDevices.getUserMedia(
        <String, dynamic>{
          'audio': true,
          'video': <String, dynamic>{
            'facingMode': 'user',
            'width': <String, int>{'ideal': 640},
            'height': <String, int>{'ideal': 480},
          },
        },
      );
      _localStream = localStream;
      _localRenderer!.srcObject = localStream;

      final peer = await createPeerConnection(<String, dynamic>{
        'iceServers': <dynamic>[],
        'sdpSemantics': 'unified-plan',
      });
      _peer = peer;
      for (final track in localStream.getTracks()) {
        await peer.addTrack(track, localStream);
      }
      _configurePeer(peer, attempt);
      _connection = _buildConnection(attempt);

      await _startRecording(localStream, normalized);
      _setState(MediaRoomState.signaling);
      await _connection!.start();
      await _connection!.invoke('JoinRoom', args: <Object>[normalized]);
      final offer = await peer.createOffer(<String, dynamic>{
        'offerToReceiveAudio': true,
        'offerToReceiveVideo': true,
      });
      await peer.setLocalDescription(offer);
      final sdp = offer.sdp;
      if (sdp == null || sdp.isEmpty) {
        throw StateError('RTC offer is missing.');
      }
      await _connection!.invoke('RelayOffer', args: <Object>[normalized, sdp]);

      final result = await _connected!.future.timeout(
        const Duration(seconds: 15),
        onTimeout: () => MediaStartResult.failed,
      );
      if (attempt != _attempt) return MediaStartResult.failed;
      if (result == MediaStartResult.connected) return result;
      await _release(uploadRecording: false);
      _setState(MediaRoomState.failed);
      return MediaStartResult.failed;
    } catch (_) {
      if (attempt == _attempt) {
        await _release(uploadRecording: false);
        _setState(MediaRoomState.failed);
      }
      return MediaStartResult.failed;
    }
  }

  @override
  Future<void> close() async {
    if (_closing) return;
    _closing = true;
    _attempt += 1;
    if (!(_connected?.isCompleted ?? true)) {
      _connected!.complete(MediaStartResult.failed);
    }
    _setState(MediaRoomState.closed);
    await _release(uploadRecording: _wasConnected);
  }

  @override
  Widget buildLocalPreview() {
    final renderer = _localRenderer;
    if (renderer == null) return const ColoredBox(color: Colors.black);
    return RTCVideoView(
      renderer,
      mirror: true,
      objectFit: RTCVideoViewObjectFit.RTCVideoViewObjectFitCover,
    );
  }

  @override
  Widget buildRemotePreview() {
    final renderer = _remoteRenderer;
    if (renderer == null) return const ColoredBox(color: Colors.black);
    return RTCVideoView(
      renderer,
      objectFit: RTCVideoViewObjectFit.RTCVideoViewObjectFitCover,
    );
  }

  Future<void> _initializeRenderers() async {
    final local = RTCVideoRenderer();
    final remote = RTCVideoRenderer();
    await local.initialize();
    await remote.initialize();
    _localRenderer = local;
    _remoteRenderer = remote;
  }

  void _configurePeer(RTCPeerConnection peer, int attempt) {
    peer.onTrack = (event) {
      if (attempt != _attempt || event.streams.isEmpty) return;
      _remoteStream = event.streams.first;
      _remoteRenderer?.srcObject = _remoteStream;
      notifyListeners();
    };
    peer.onIceCandidate = (candidate) {
      final connection = _connection;
      final sessionId = _sessionId;
      final value = candidate.candidate;
      if (attempt != _attempt ||
          connection == null ||
          sessionId == null ||
          value == null ||
          value.isEmpty) {
        return;
      }
      unawaited(
        _relayCandidate(
          connection,
          sessionId,
          value,
          candidate.sdpMid ?? '',
          candidate.sdpMLineIndex ?? 0,
          attempt,
        ),
      );
    };
    peer.onConnectionState = (connectionState) {
      if (attempt != _attempt) return;
      if (connectionState ==
          RTCPeerConnectionState.RTCPeerConnectionStateConnected) {
        _wasConnected = true;
        _setState(MediaRoomState.connected);
        if (!(_connected?.isCompleted ?? true)) {
          _connected!.complete(MediaStartResult.connected);
        }
      } else if (connectionState ==
              RTCPeerConnectionState.RTCPeerConnectionStateFailed ||
          connectionState ==
              RTCPeerConnectionState.RTCPeerConnectionStateDisconnected) {
        _fail(attempt);
      }
    };
  }

  HubConnection _buildConnection(int attempt) {
    final connection = HubConnectionBuilder()
        .withUrl(
          _hubUrl,
          options: HttpConnectionOptions(
            accessTokenFactory: () async {
              final token = await _readAccessToken();
              if (token == null || token.isEmpty) {
                throw StateError('Access token is missing.');
              }
              return token;
            },
          ),
        )
        .build();
    connection.on('AnswerReceived', (arguments) {
      final value = arguments?.firstOrNull;
      if (value is Map) {
        unawaited(
          _acceptAnswer(
            Map<String, dynamic>.from(value),
            attempt,
          ).catchError((_) => _fail(attempt)),
        );
      }
    });
    connection.on('IceCandidateReceived', (arguments) {
      final value = arguments?.firstOrNull;
      if (value is Map) {
        unawaited(
          _acceptCandidate(
            Map<String, dynamic>.from(value),
            attempt,
          ).catchError((_) => _fail(attempt)),
        );
      }
    });
    connection.onclose(({error}) {
      if (!_closing) _fail(attempt);
    });
    return connection;
  }

  Future<void> _relayCandidate(
    HubConnection connection,
    String sessionId,
    String candidate,
    String sdpMid,
    int sdpMLineIndex,
    int attempt,
  ) async {
    try {
      await connection.invoke(
        'RelayIceCandidate',
        args: <Object>[sessionId, candidate, sdpMid, sdpMLineIndex],
      );
    } catch (_) {
      _fail(attempt);
    }
  }

  Future<void> _acceptAnswer(Map<String, dynamic> value, int attempt) async {
    if (attempt != _attempt || value['sessionId']?.toString() != _sessionId) {
      return;
    }
    final sdp = value['sdp']?.toString();
    if (sdp == null || sdp.isEmpty || _peer == null) return;
    await _peer!.setRemoteDescription(RTCSessionDescription(sdp, 'answer'));
    for (final candidate in List<RTCIceCandidate>.from(_pendingCandidates)) {
      await _peer!.addCandidate(candidate);
    }
    _pendingCandidates.clear();
  }

  Future<void> _acceptCandidate(Map<String, dynamic> value, int attempt) async {
    if (attempt != _attempt || value['sessionId']?.toString() != _sessionId) {
      return;
    }
    final rawIndex = value['sdpMLineIndex'];
    final candidate = RTCIceCandidate(
      value['candidate']?.toString(),
      value['sdpMid']?.toString(),
      rawIndex is num ? rawIndex.toInt() : null,
    );
    if (candidate.candidate == null || candidate.candidate!.isEmpty) return;
    if (await _peer?.getRemoteDescription() == null) {
      _pendingCandidates.add(candidate);
      return;
    }
    await _peer!.addCandidate(candidate);
  }

  Future<void> _startRecording(
    MediaStream localStream,
    String sessionId,
  ) async {
    final videoTrack = localStream.getVideoTracks().firstOrNull;
    if (videoTrack == null) return;
    final createdAt = DateTime.now().toUtc().microsecondsSinceEpoch;
    final file = File(
      '${Directory.systemTemp.path}${Platform.pathSeparator}'
      'mh-video-$sessionId-$createdAt.mp4',
    );
    final recorder = MediaRecorder(albumName: 'MentalHealthDemo');
    try {
      await recorder.start(
        file.path,
        videoTrack: videoTrack,
        audioChannel: RecorderAudioChannel.INPUT,
      );
      _recorder = recorder;
      _recordingFile = file;
      _recordingKey = 'video-${sessionId.replaceAll('-', '')}-$createdAt';
    } catch (_) {
      if (await file.exists()) await file.delete();
    }
  }

  void _fail(int attempt) {
    if (attempt != _attempt || _closing || _state == MediaRoomState.failed) {
      return;
    }
    _setState(MediaRoomState.failed);
    if (!(_connected?.isCompleted ?? true)) {
      _connected!.complete(MediaStartResult.failed);
    }
    unawaited(_release(uploadRecording: _wasConnected));
  }

  Future<void> _release({required bool uploadRecording}) {
    final existing = _releaseInProgress;
    if (existing != null) return existing;
    late final Future<void> operation;
    operation = _releaseResources(uploadRecording: uploadRecording)
        .whenComplete(() {
          if (identical(_releaseInProgress, operation)) {
            _releaseInProgress = null;
          }
        });
    _releaseInProgress = operation;
    return operation;
  }

  Future<void> _releaseResources({required bool uploadRecording}) async {
    final connection = _connection;
    final peer = _peer;
    final localStream = _localStream;
    final remoteStream = _remoteStream;
    final localRenderer = _localRenderer;
    final remoteRenderer = _remoteRenderer;
    final recorder = _recorder;
    final recordingFile = _recordingFile;
    final recordingKey = _recordingKey;
    final sessionId = _sessionId;

    _connection = null;
    _peer = null;
    _localStream = null;
    _remoteStream = null;
    _localRenderer = null;
    _remoteRenderer = null;
    _recorder = null;
    _recordingFile = null;
    _recordingKey = null;
    _sessionId = null;
    _pendingCandidates.clear();

    if (recorder != null) {
      try {
        await recorder.stop();
      } catch (_) {}
    }
    if (connection?.state == HubConnectionState.Connected &&
        sessionId != null) {
      try {
        await connection!.invoke('LeaveRoom', args: <Object>[sessionId]);
      } catch (_) {}
    }
    try {
      await peer?.close();
    } catch (_) {}
    for (final track
        in localStream?.getTracks() ?? const <MediaStreamTrack>[]) {
      try {
        await track.stop();
      } catch (_) {}
    }
    for (final track
        in remoteStream?.getTracks() ?? const <MediaStreamTrack>[]) {
      try {
        await track.stop();
      } catch (_) {}
    }
    try {
      await localStream?.dispose();
    } catch (_) {}
    try {
      await remoteStream?.dispose();
    } catch (_) {}
    try {
      await connection?.stop();
    } catch (_) {}
    try {
      await localRenderer?.dispose();
    } catch (_) {}
    try {
      await remoteRenderer?.dispose();
    } catch (_) {}

    if (recordingFile != null) {
      try {
        if (uploadRecording &&
            sessionId != null &&
            recordingKey != null &&
            await recordingFile.exists()) {
          await _uploader.upload(
            sessionId: sessionId,
            file: recordingFile,
            idempotencyKey: recordingKey,
          );
        }
      } catch (_) {
        _uploadFailed = true;
        notifyListeners();
      } finally {
        try {
          if (await recordingFile.exists()) await recordingFile.delete();
        } catch (_) {
          _uploadFailed = true;
          notifyListeners();
        }
      }
    }
  }

  void _setState(MediaRoomState value) {
    _state = value;
    notifyListeners();
  }
}
