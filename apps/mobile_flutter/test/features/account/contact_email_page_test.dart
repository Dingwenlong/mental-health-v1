import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/account/contact_email_gateway.dart';
import 'package:mobile_flutter/features/account/contact_email_page.dart';

void main() {
  testWidgets('contact email loads and saves a trimmed value', (tester) async {
    final gateway = _FakeContactEmailGateway('old@example.test');
    await tester.pumpWidget(
      MaterialApp(home: ContactEmailPage(gateway: gateway)),
    );
    await tester.pump();

    expect(find.text('联系邮箱不能用于登录'), findsOneWidget);
    expect(
      tester
          .widget<TextField>(find.byKey(const Key('contact-email')))
          .controller!
          .text,
      'old@example.test',
    );

    await tester.enterText(
      find.byKey(const Key('contact-email')),
      '  next@example.test  ',
    );
    await tester.tap(find.byKey(const Key('contact-email-save')));
    await tester.pump();

    expect(gateway.savedValues, <String?>['next@example.test']);
  });

  testWidgets('contact email can be cleared explicitly', (tester) async {
    final gateway = _FakeContactEmailGateway('old@example.test');
    await tester.pumpWidget(
      MaterialApp(home: ContactEmailPage(gateway: gateway)),
    );
    await tester.pump();

    await tester.tap(find.byKey(const Key('contact-email-clear')));
    await tester.pump();

    expect(gateway.savedValues, <String?>[null]);
    expect(
      tester
          .widget<TextField>(find.byKey(const Key('contact-email')))
          .controller!
          .text,
      isEmpty,
    );
  });

  testWidgets('invalid contact email stays local and is announced', (
    tester,
  ) async {
    final gateway = _FakeContactEmailGateway(null);
    await tester.pumpWidget(
      MaterialApp(home: ContactEmailPage(gateway: gateway)),
    );
    await tester.pump();

    await tester.enterText(
      find.byKey(const Key('contact-email')),
      'not-an-email',
    );
    await tester.tap(find.byKey(const Key('contact-email-save')));
    await tester.pump();

    expect(gateway.savedValues, isEmpty);
    expect(find.text('联系邮箱格式不正确'), findsOneWidget);
    expect(
      tester.getSemantics(find.text('联系邮箱格式不正确')).flagsCollection.isLiveRegion,
      isTrue,
    );
  });
}

class _FakeContactEmailGateway implements ContactEmailGateway {
  _FakeContactEmailGateway(this.value);

  String? value;
  final savedValues = <String?>[];

  @override
  Future<String?> getContactEmail() async => value;

  @override
  Future<void> putContactEmail(String? email) async {
    savedValues.add(email);
    value = email;
  }
}
