using MentalHealth.Application.Abstractions.Providers;
using System.Security.Cryptography;
using System.Text;

namespace MentalHealth.ContractTests.Fakes;

internal sealed class FakeTranscriptionProvider : ITranscriptionProvider
{
    public Task<TranscriptDocument?> GetAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.ObjectKey))
        {
            throw new ProviderException("TRANSCRIPT_REQUIRED");
        }

        TranscriptDocument? document = string.IsNullOrWhiteSpace(request.SuppliedText)
            ? null
            : new TranscriptDocument(
                request.SessionId,
                request.Revision ?? 1,
                "ManualUpload",
                request.SuppliedText,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.SuppliedText))),
                "zh-CN",
                IsManual: true,
                Segments: []);
        return Task.FromResult(document);
    }
}
