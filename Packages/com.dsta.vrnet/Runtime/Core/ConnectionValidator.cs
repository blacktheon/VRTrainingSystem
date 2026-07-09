namespace DSTA.VRNet.Core
{
    public struct ValidationResult
    {
        public bool Approved;
        public string Reason;
        public ConnectionPayload Payload;
    }

    /// <summary>Pure server-side approval logic — no Unity/network dependencies so it is unit-testable.</summary>
    public static class ConnectionValidator
    {
        public static ValidationResult Validate(byte[] payloadBytes, string serverTeamId, string serverAppVersion)
        {
            if (!ConnectionPayload.TryParse(payloadBytes, out var payload))
                return new ValidationResult { Approved = false, Reason = "Malformed connection payload" };

            if (payload.teamId != serverTeamId)
                return new ValidationResult
                {
                    Approved = false,
                    Reason = $"Wrong team: client is '{payload.teamId}', server is '{serverTeamId}'"
                };

            if (payload.appVersion != serverAppVersion)
                return new ValidationResult
                {
                    Approved = false,
                    Reason = $"App version mismatch: client {payload.appVersion}, server {serverAppVersion}"
                };

            return new ValidationResult { Approved = true, Reason = "", Payload = payload };
        }
    }
}
