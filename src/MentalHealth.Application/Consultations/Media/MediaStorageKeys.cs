namespace MentalHealth.Application.Consultations.Media;

internal static class MediaStorageKeys
{
    public static string Chunk(Guid assetId, int index) =>
        $"pending-media/{assetId:N}/chunks/{index:D6}";

    public static string Final(Guid subjectId, Guid assetId) =>
        $"demo/{subjectId:N}/media/{assetId:N}.media";
}
