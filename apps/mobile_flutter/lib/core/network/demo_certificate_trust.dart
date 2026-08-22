import 'dart:io';

import 'package:flutter/services.dart';

Future<void> installDemoCertificateTrust() async {
  final certificate = await rootBundle.load('assets/certs/demo_root.crt');
  final bytes = certificate.buffer.asUint8List(
    certificate.offsetInBytes,
    certificate.lengthInBytes,
  );
  final context = SecurityContext(withTrustedRoots: true)
    ..setTrustedCertificatesBytes(bytes);
  HttpOverrides.global = _DemoCertificateHttpOverrides(context);
}

class _DemoCertificateHttpOverrides extends HttpOverrides {
  _DemoCertificateHttpOverrides(this._context);

  final SecurityContext _context;

  @override
  HttpClient createHttpClient(SecurityContext? context) =>
      super.createHttpClient(context ?? _context);
}
