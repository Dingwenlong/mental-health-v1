namespace MentalHealth.Infrastructure.Storage;

public sealed class LocalObjectStorageOptions
{
    public const string SectionName = "LocalObjectStorage";

    public string RootPath { get; set; } = string.Empty;
}
