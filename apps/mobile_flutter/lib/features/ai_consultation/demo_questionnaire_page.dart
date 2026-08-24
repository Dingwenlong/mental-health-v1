import 'package:flutter/material.dart';

import '../../generated/ui_copy.g.dart';

class QuestionnaireSubmission {
  const QuestionnaireSubmission({required this.answers, required this.score});

  final List<int> answers;
  final double score;
}

class DemoQuestionnairePage extends StatefulWidget {
  const DemoQuestionnairePage({required this.onSubmitted, super.key});

  final ValueChanged<QuestionnaireSubmission> onSubmitted;

  @override
  State<DemoQuestionnairePage> createState() => _DemoQuestionnairePageState();
}

class _DemoQuestionnairePageState extends State<DemoQuestionnairePage> {
  static final _questions = <String>[
    for (var index = 1; index <= 7; index += 1)
      UiCopy.get('questionnaire.question.$index'),
  ];
  static final _answers = <String>[
    UiCopy.get('questionnaire.answer.none'),
    UiCopy.get('questionnaire.answer.sometimes'),
    UiCopy.get('questionnaire.answer.often'),
    UiCopy.get('questionnaire.answer.daily'),
  ];
  final List<int?> _values = List<int?>.filled(_questions.length, null);

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(UiCopy.get('questionnaire.title'))),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 28),
        children: <Widget>[
          Text(
            UiCopy.get('questionnaire.heading'),
            style: Theme.of(context).textTheme.headlineSmall
                ?.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          Text(UiCopy.get('questionnaire.notice')),
          const SizedBox(height: 20),
          for (
            var questionIndex = 0;
            questionIndex < _questions.length;
            questionIndex += 1
          )
            _QuestionBlock(
              index: questionIndex,
              text: _questions[questionIndex],
              labels: _answers,
              value: _values[questionIndex],
              onChanged: (value) =>
                  setState(() => _values[questionIndex] = value),
            ),
          const SizedBox(height: 8),
          FilledButton(
            key: const Key('questionnaire-submit'),
            onPressed: _values.every((value) => value != null) ? _submit : null,
            child: Text(UiCopy.get('questionnaire.submit')),
          ),
        ],
      ),
    ),
  );

  void _submit() {
    final answers = _values.cast<int>();
    final total = answers.fold<int>(0, (sum, value) => sum + value);
    widget.onSubmitted(
      QuestionnaireSubmission(
        answers: List<int>.unmodifiable(answers),
        score: total / 21 * 100,
      ),
    );
  }
}

class _QuestionBlock extends StatelessWidget {
  const _QuestionBlock({
    required this.index,
    required this.text,
    required this.labels,
    required this.value,
    required this.onChanged,
  });

  final int index;
  final String text;
  final List<String> labels;
  final int? value;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.only(bottom: 20),
    padding: const EdgeInsets.only(bottom: 18),
    decoration: BoxDecoration(
      border: Border(bottom: BorderSide(color: Theme.of(context).dividerColor)),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          '${index + 1}. $text',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 10),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: <Widget>[
            for (var answer = 0; answer < labels.length; answer += 1)
              ChoiceChip(
                key: Key('question-$index-answer-$answer'),
                label: Text(labels[answer]),
                selected: value == answer,
                onSelected: (_) => onChanged(answer),
              ),
          ],
        ),
      ],
    ),
  );
}
