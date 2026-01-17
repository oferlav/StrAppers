namespace strAppersBackend.Models
{
    /// <summary>
    /// Configuration for deployment error handling and AI summarization
    /// </summary>
    public class DeploymentsConfig
    {
        /// <summary>
        /// Configuration for runtime errors
        /// </summary>
        public ErrorTypeConfig RuntimeErrors { get; set; } = new ErrorTypeConfig();

        /// <summary>
        /// Configuration for build errors
        /// </summary>
        public ErrorTypeConfig BuildErrors { get; set; } = new ErrorTypeConfig { SendToAISummary = true };

        /// <summary>
        /// Configuration for frontend errors
        /// </summary>
        public ErrorTypeConfig FrontendErrors { get; set; } = new ErrorTypeConfig();
    }

    /// <summary>
    /// Configuration for a specific error type
    /// </summary>
    public class ErrorTypeConfig
    {
        /// <summary>
        /// Whether to send errors of this type to AI for summarization
        /// Default: false
        /// </summary>
        public bool SendToAISummary { get; set; } = false;
    }
}
