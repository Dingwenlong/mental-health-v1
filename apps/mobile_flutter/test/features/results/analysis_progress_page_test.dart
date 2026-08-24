import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/results/analysis_progress_page.dart';
import 'package:mobile_flutter/features/results/result_page.dart';

void main() {
  testWidgets('pending analysis can be refreshed into a result', (
    tester,
  ) async {
    final gateway = _FakeResultGateway();
    await tester.pumpWidget(
      MaterialApp(
        home: AnalysisProgressPage(sessionId: 'session-1', gateway: gateway),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('正在整理这次分析'), findsOneWidget);
    expect(find.text('分析结果'), findsNothing);

    await tester.tap(find.byKey(const Key('analysis-refresh')));
    await tester.pumpAndSettle();

    expect(find.text('分析结果'), findsOneWidget);
    expect(find.text('42'), findsOneWidget);
    expect(gateway.requestCount, 2);
  });
}

class _FakeResultGateway implements ResultGateway {
  int requestCount = 0;

  @override
  Future<AssessmentResult?> getResult(String sessionId) async {
    requestCount += 1;
    if (requestCount == 1) return null;
    return AssessmentResult(
      sessionId: sessionId,
      score: 42,
      level: 'L1',
      ruleSetVersion: 'v1',
      missing: const <String>['Audio', 'Video', 'Trend'],
      evidence: const <AssessmentEvidence>[
        AssessmentEvidence(
          code: 'questionnaire_total',
          modality: 'Scale',
          contribution: 30,
          quality: 1,
          sourceRange: 'questionnaire',
        ),
        AssessmentEvidence(
          code: 'text_signal',
          modality: 'Text',
          contribution: 12,
          quality: 0.8,
          sourceRange: 'transcript',
        ),
      ],
      createdAt: DateTime.utc(2026, 8, 24),
    );
  }
}
