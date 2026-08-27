import 'package:flutter/material.dart';

import '../ai_consultation/ai_consultation_page.dart';
import '../ai_consultation/ai_session_launcher.dart';
import '../consultation/chat_connection.dart';
import '../consultation/chat_page.dart';
import '../consultation/video_page.dart';
import '../consultation/video_session_launcher.dart';
import '../results/analysis_progress_page.dart';
import 'care_repository.dart';
import 'care_widgets.dart';

class ConsultationHistoryPage extends StatefulWidget {
  const ConsultationHistoryPage({
    required this.repository,
    required this.resultGateway,
    this.reports = false,
    this.chatLauncher,
    this.videoLauncher,
    this.aiLauncher,
    super.key,
  });
  final CareRepository repository;
  final ResultGateway resultGateway;
  final bool reports;
  final ChatSessionLauncher? chatLauncher;
  final VideoSessionLauncher? videoLauncher;
  final AiSessionLauncher? aiLauncher;
  @override
  State<ConsultationHistoryPage> createState() =>
      _ConsultationHistoryPageState();
}

class _ConsultationHistoryPageState extends State<ConsultationHistoryPage> {
  int _page = 1;
  String? _status;
  String? _from, _to;
  Future<void> _dates() async {
    final range = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2000),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );
    if (range != null && mounted) {
      setState(() {
        _from = range.start.toIso8601String().substring(0, 10);
        _to = range.end.toIso8601String().substring(0, 10);
        _page = 1;
      });
    }
  }

  bool _opening = false;
  Future<void> _open(CareJson session, VoidCallback reload) async {
    if (_opening) return;
    setState(() => _opening = true);
    try {
      if (session['status'] == 'Completed') {
        if (!mounted) return;
        await Navigator.push(
          context,
          MaterialPageRoute<void>(
            builder: (route) => AnalysisProgressPage(
              sessionId: session['id'] as String,
              gateway: widget.resultGateway,
              onBack: () => Navigator.pop(route),
            ),
          ),
        );
      } else {
        final orderId = session['orderId'] as String?;
        if (orderId == null) return;
        if (session['kind'] == 'AiVirtual' && widget.aiLauncher != null) {
          final ai = await widget.aiLauncher!.startAiChat(orderId);
          if (!mounted) return;
          await Navigator.push(
            context,
            MaterialPageRoute<void>(
              builder: (_) => AiConsultationPage(
                sessionId: ai.sessionId,
                gateway: ai.gateway,
                speech: ai.speech,
              ),
            ),
          );
        } else if (session['channel'] == 'Video' &&
            widget.videoLauncher != null) {
          final video = await widget.videoLauncher!.resumeHumanVideo(
            session['id'] as String,
            start: session['status'] == 'Scheduled',
          );
          if (!mounted) return;
          await Navigator.push(
            context,
            MaterialPageRoute<void>(
              builder: (route) => VideoPage(
                sessionId: video.sessionId,
                media: video.media,
                onSwitchToText: () => Navigator.of(route).pushReplacement(
                  MaterialPageRoute<void>(
                    builder: (_) => ChatPage(
                      sessionId: video.sessionId,
                      controller: video.textController,
                    ),
                  ),
                ),
              ),
            ),
          );
        } else if (widget.chatLauncher != null) {
          final chat = await widget.chatLauncher!.resumeHumanChat(
            session['id'] as String,
            start: session['status'] == 'Scheduled',
          );
          if (!mounted) return;
          await Navigator.push(
            context,
            MaterialPageRoute<void>(
              builder: (_) => ChatPage(
                sessionId: chat.sessionId,
                controller: chat.controller,
              ),
            ),
          );
        }
      }
    } catch (error) {
      if (mounted) careError(context, error);
    } finally {
      if (mounted) {
        setState(() => _opening = false);
        reload();
      }
    }
  }

  @override
  Widget build(BuildContext context) => CarePageFrame<CareJson>(
    key: ValueKey('$_page:$_status:$_from:$_to'),
    title: careCopy(widget.reports ? 'care.reports' : 'care.history'),
    load: () => widget.repository.consultations(
      page: _page,
      reports: widget.reports,
      status: _status,
      from: _from,
      to: _to,
    ),
    builder: (data, reload) => ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Wrap(
          spacing: 8,
          children: [
            OutlinedButton(
              onPressed: _dates,
              child: Text(
                _from == null ? careCopy('care.dateRange') : '$_from ~ $_to',
              ),
            ),
            if (_from != null)
              TextButton(
                onPressed: () => setState(() {
                  _from = null;
                  _to = null;
                  _page = 1;
                }),
                child: Text(careCopy('care.clearFilter')),
              ),
          ],
        ),
        DropdownButtonFormField<String>(
          initialValue: _status ?? '',
          decoration: InputDecoration(labelText: careCopy('care.all')),
          items:
              (widget.reports
                      ? [
                          '',
                          'NotRequested',
                          'Pending',
                          'Ready',
                          'Processing',
                          'NeedsManual',
                          'Completed',
                        ]
                      : [
                          '',
                          'Scheduled',
                          'InProgress',
                          'Completed',
                          'Cancelled',
                        ])
                  .map(
                    (status) => DropdownMenuItem(
                      value: status,
                      child: Text(
                        careCopy(
                          status.isEmpty ? 'care.all' : 'care.status.$status',
                        ),
                      ),
                    ),
                  )
                  .toList(),
          onChanged: (status) => setState(() {
            _status = status == '' ? null : status;
            _page = 1;
          }),
        ),
        if ((data['total'] as num) == 0)
          careNotice(
            widget.reports ? 'care.noReports' : 'care.noConsultations',
          ),
        ...careObjects(data['items']).map(
          (session) => Card(
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    careTime(session['scheduledAt']),
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Text(
                    '${session['practitionerName'] ?? careCopy('home.consultation')} · ${careCopy('care.status.${session['status']}')}',
                  ),
                  if (session['status'] == 'Completed')
                    Text(careCopy('care.status.${session['analysisStatus']}')),
                  if ([
                    'Scheduled',
                    'InProgress',
                    'Completed',
                  ].contains(session['status']))
                    TextButton(
                      onPressed: _opening ? null : () => _open(session, reload),
                      child: Text(
                        careCopy(
                          session['status'] == 'Completed'
                              ? 'care.openReport'
                              : 'care.openConsultation',
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
        CarePagination(
          page: _page,
          total: (data['total'] as num).toInt(),
          onChanged: (page) => setState(() => _page = page),
        ),
      ],
    ),
  );
}
