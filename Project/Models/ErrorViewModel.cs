namespace FabriTrack.Models
{
    // Holds an ID that can help identify what went wrong with a request.
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        // Shows whether there is an ID to display.
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}