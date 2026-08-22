namespace MentalHealth.Contracts.Common;

public static class ApiProblemCodes
{
    public const string ConsentRequired = "CONSENT_REQUIRED";
    public const string InvalidSessionState = "INVALID_SESSION_STATE";
    public const string InvalidObjectKey = "INVALID_OBJECT_KEY";
    public const string TranscriptRequired = "TRANSCRIPT_REQUIRED";
    public const string MediaParseFailed = "MEDIA_PARSE_FAILED";
    public const string MediaHashMismatch = "MEDIA_HASH_MISMATCH";
    public const string InvalidChunkIndex = "INVALID_CHUNK_INDEX";
    public const string MfaRequired = "MFA_REQUIRED";
    public const string CrisisInProgress = "CRISIS_IN_PROGRESS";
    public const string NoQualifiedSlotBeforeSla = "NO_QUALIFIED_SLOT_BEFORE_SLA";
}
