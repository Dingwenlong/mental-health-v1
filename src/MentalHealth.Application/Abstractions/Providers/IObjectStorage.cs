namespace MentalHealth.Application.Abstractions.Providers;

public sealed record ObjectWriteRequest(string ObjectKey, string ContentType);

public sealed record StoredObject(string ObjectKey, string Sha256, long Length);

public interface IObjectStorage
{
    Task<StoredObject> PutAsync(
        ObjectWriteRequest request,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
