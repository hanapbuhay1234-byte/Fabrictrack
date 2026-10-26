namespace FabriTrack.Models
{
    // Marks accounts that must change their temporary password before continuing.
    public static class PasswordChangeRequiredClaim
    {
        public const string Type = "fabritrack:password-change-required";
    }
}
