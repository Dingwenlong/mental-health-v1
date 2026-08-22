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
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string InvalidMfaCode = "INVALID_MFA_CODE";
    public const string ConsentTypeDisabled = "CONSENT_TYPE_DISABLED";
    public const string InvalidConsentKind = "INVALID_CONSENT_KIND";
    public const string InvalidConsentTextVersion = "INVALID_CONSENT_TEXT_VERSION";
    public const string ActiveConsentExists = "ACTIVE_CONSENT_EXISTS";
    public const string ForbiddenResource = "FORBIDDEN_RESOURCE";
    public const string PlanCombinationUnsupported = "PLAN_COMBINATION_UNSUPPORTED";
    public const string CatalogValueInvalid = "CATALOG_VALUE_INVALID";
    public const string AvailabilitySlotConflict = "AVAILABILITY_SLOT_CONFLICT";
    public const string PractitionerNotFound = "PRACTITIONER_NOT_FOUND";
    public const string PractitionerRoleLocked = "PRACTITIONER_ROLE_LOCKED";
    public const string PlanNotAvailable = "PLAN_NOT_AVAILABLE";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string IdempotencyKeyInvalid = "IDEMPOTENCY_KEY_INVALID";
    public const string CrisisInProgress = "CRISIS_IN_PROGRESS";
    public const string NoQualifiedSlotBeforeSla = "NO_QUALIFIED_SLOT_BEFORE_SLA";
}
