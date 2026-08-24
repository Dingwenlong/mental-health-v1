import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import '../../generated/ui_copy.g.dart';
import 'result_page.dart';

abstract interface class ResultGateway {
  Future<AssessmentResult?> getResult(String sessionId);
}

class ApiResultGateway implements ResultGateway {
  const ApiResultGateway(this._client);

  final ApiClient _client;

  @override
  Future<AssessmentResult?> getResult(String sessionId) async {
    try {
      final json = await _client.get('results/$sessionId');
      return _readResult(json);
    } on ApiFailure catch (error) {
      if (error.code == 'RESULT_NOT_FOUND' || error.status == 404) return null;
      rethrow;
    }
  }

  static AssessmentResult _readResult(Map<String, dynamic> json) {
    final rawEvidence = json['evidence'];
    return AssessmentResult(
      sessionId: json['sessionId'].toString(),
      score: (json['score'] as num).toDouble(),
      level: json['level'].toString(),
      ruleSetVersion: json['ruleSetVersion'].toString(),
      missing: (json['missing'] as List<dynamic>? ?? const <dynamic>[])
          .map((value) => value.toString())
          .toList(),
      evidence: (rawEvidence as List<dynamic>? ?? const <dynamic>[]).map((raw) {
        final item = Map<String, dynamic>.from(raw as Map);
        return AssessmentEvidence(
          code: item['code'].toString(),
          modality: item['modality'].toString(),
          contribution: (item['contribution'] as num).toDouble(),
          quality: (item['quality'] as num).toDouble(),
          sourceRange: item['sourceRange'].toString(),
        );
      }).toList(),
      createdAt: DateTime.parse(json['createdAt'].toString()),
    );
  }
}

class AnalysisProgressPage extends StatefulWidget {
  const AnalysisProgressPage({
    required this.sessionId,
    required this.gateway,
    this.onBack,
    super.key,
  });

  final String sessionId;
  final ResultGateway gateway;
  final VoidCallback? onBack;

  @override
  State<AnalysisProgressPage> createState() => _AnalysisProgressPageState();
}

class _AnalysisProgressPageState extends State<AnalysisProgressPage> {
  AssessmentResult? _result;
  bool _loading = true;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  Widget build(BuildContext context) {
    if (_result case final result?) {
      return ResultPage(result: result, onBack: widget.onBack);
    }
    return Scaffold(
      appBar: AppBar(
        leading: widget.onBack == null
            ? null
            : IconButton(
                onPressed: widget.onBack,
                icon: const Icon(Icons.arrow_back),
              ),
        title: Text(UiCopy.get('analysis.title')),
      ),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              if (_loading) const CircularProgressIndicator(),
              const SizedBox(height: 18),
              Text(
                _failed
                    ? UiCopy.get('error.retry')
                    : UiCopy.get('analysis.processing'),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 16),
              OutlinedButton(
                key: const Key('analysis-refresh'),
                onPressed: _loading ? null : _load,
                child: Text(UiCopy.get('common.refresh')),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _failed = false;
    });
    try {
      final result = await widget.gateway.getResult(widget.sessionId);
      if (!mounted) return;
      setState(() {
        _result = result;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _failed = true;
      });
    }
  }
}
