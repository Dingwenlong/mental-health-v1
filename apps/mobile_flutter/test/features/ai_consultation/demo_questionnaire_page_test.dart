import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/ai_consultation/demo_questionnaire_page.dart';

void main() {
  testWidgets(
    'questionnaire submits only after all seven answers are selected',
    (tester) async {
      QuestionnaireSubmission? submitted;
      await tester.pumpWidget(
        MaterialApp(
          home: DemoQuestionnairePage(
            onSubmitted: (value) => submitted = value,
          ),
        ),
      );

      expect(find.text('最近 7 天的情况'), findsOneWidget);
      final submit = find.byKey(const Key('questionnaire-submit'));
      await tester.scrollUntilVisible(submit, 320);
      expect(tester.widget<FilledButton>(submit).onPressed, isNull);
      await tester.drag(find.byType(ListView), const Offset(0, 2000));
      await tester.pumpAndSettle();

      for (var index = 0; index < 7; index += 1) {
        final answer = find.byKey(Key('question-$index-answer-3'));
        while (answer.evaluate().isEmpty) {
          await tester.drag(find.byType(ListView), const Offset(0, -260));
          await tester.pumpAndSettle();
        }
        await tester.ensureVisible(answer);
        await tester.tap(answer);
        await tester.pump();
      }

      await tester.scrollUntilVisible(submit, 240);
      expect(tester.widget<FilledButton>(submit).onPressed, isNotNull);
      await tester.tap(submit);
      await tester.pump();

      expect(submitted?.score, 100);
      expect(submitted?.answers, List<int>.filled(7, 3));
    },
  );
}
