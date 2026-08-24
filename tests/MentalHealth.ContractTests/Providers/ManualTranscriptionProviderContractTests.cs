using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Analysis;
using MentalHealth.Domain.Analysis;
using MentalHealth.Infrastructure.Providers;

namespace MentalHealth.ContractTests.Providers;

public sealed class ManualTranscriptionProviderContractTests
{
    private static readonly Guid SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Get_returns_the_requested_revision_and_source()
    {
        var store = new TranscriptReaderStub([
            ManualTranscript.Create(
                SessionId,
                revision: 1,
                TranscriptSource.ManualUpload,
                "第一版人工转写。",
                DateTimeOffset.Parse("2026-08-24T01:00:00+00:00")),
            ManualTranscript.Create(
                SessionId,
                revision: 2,
                TranscriptSource.ManualCorrection,
                "第二版人工校对。",
                DateTimeOffset.Parse("2026-08-24T01:05:00+00:00"))
        ]);
        var provider = new ManualTranscriptionProvider(store);

        var document = await provider.GetAsync(
            new TranscriptionRequest(SessionId, "manual", null, Revision: 1),
            CancellationToken.None);

        Assert.NotNull(document);
        Assert.Equal(1, document.Revision);
        Assert.Equal("ManualUpload", document.Source);
        Assert.Equal("第一版人工转写。", document.Text);
        Assert.Equal(64, document.Sha256.Length);
    }

    [Fact]
    public async Task Get_returns_null_when_no_manual_transcript_exists()
    {
        var provider = new ManualTranscriptionProvider(new TranscriptReaderStub([]));

        var document = await provider.GetAsync(
            new TranscriptionRequest(SessionId, "manual", null),
            CancellationToken.None);

        Assert.Null(document);
    }

    [Fact]
    public async Task Get_honors_a_pre_cancelled_token()
    {
        var provider = new ManualTranscriptionProvider(new TranscriptReaderStub([]));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetAsync(
            new TranscriptionRequest(SessionId, "manual", null),
            new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Get_rejects_a_blank_object_key()
    {
        var provider = new ManualTranscriptionProvider(new TranscriptReaderStub([]));

        var exception = await Assert.ThrowsAsync<ProviderException>(() => provider.GetAsync(
            new TranscriptionRequest(SessionId, " ", null),
            CancellationToken.None));

        Assert.Equal("TRANSCRIPT_REQUIRED", exception.Code);
    }

    private sealed class TranscriptReaderStub(IReadOnlyCollection<ManualTranscript> documents)
        : IManualTranscriptReader
    {
        public Task<ManualTranscript?> FindAsync(
            Guid sessionId,
            int? revision,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matches = documents.Where(item => item.SessionId == sessionId);
            var document = revision is { } exact
                ? matches.SingleOrDefault(item => item.Revision == exact)
                : matches.OrderByDescending(item => item.Revision).FirstOrDefault();
            return Task.FromResult(document);
        }
    }
}
