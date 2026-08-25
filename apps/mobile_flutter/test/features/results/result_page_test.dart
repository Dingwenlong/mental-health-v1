import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/results/result_page.dart';

void main() {
  testWidgets(
    'result shows missing modalities and the full non-diagnostic warning',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: ResultPage(
            result: AssessmentResult(
              sessionId: 'session-1',
              score: 68,
              level: 'L2',
              ruleSetVersion: 'v1',
              missing: const <String>['Video', 'Trend'],
              evidence: const <AssessmentEvidence>[
                AssessmentEvidence(
                  code: 'questionnaire_total',
                  modality: 'Scale',
                  contribution: 30.6,
                  quality: 1,
                  sourceRange: 'DemoWellbeingScaleV1:items[0..6]',
                ),
                AssessmentEvidence(
                  code: 'text_signal',
                  modality: 'Text',
                  contribution: 13.2,
                  quality: 0.8,
                  sourceRange: 'transcript:1',
                ),
                AssessmentEvidence(
                  code: 'speech_signal',
                  modality: 'Audio',
                  contribution: 6.8,
                  quality: 0.5,
                  sourceRange: 'audio:0-60s',
                ),
              ],
              createdAt: DateTime.utc(2026, 8, 24, 8),
            ),
            followUp: FollowUpSummary(
              id: 'follow-up-1',
              status: 'Scheduled',
              dueAt: DateTime(2026, 8, 25, 14, 30),
              assigneeName: '李医生',
            ),
          ),
        ),
      );

      expect(find.text('这次没有使用录像和以前的记录'), findsOneWidget);
      expect(find.text('此结果不能替代诊断。需要医疗帮助时，请联系医生。'), findsOneWidget);
      expect(find.text('问卷'), findsOneWidget);
      expect(find.text('对话文字'), findsOneWidget);
      expect(find.text('录音'), findsOneWidget);
      expect(find.text('李医生'), findsOneWidget);

      await tester.tap(find.text('这个分数怎么来的'));
      await tester.pumpAndSettle();

      expect(find.text('问卷 45%'), findsOneWidget);
      expect(find.text('录像 5%'), findsOneWidget);
      expect(find.text('质量：未提供'), findsNWidgets(2));
    },
  );
}
