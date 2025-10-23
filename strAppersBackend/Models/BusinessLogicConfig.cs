namespace strAppersBackend.Models
{
    /// <summary>
    /// Configuration for business logic settings
    /// </summary>
    public class BusinessLogicConfig
    {
        /// <summary>
        /// Default project length in weeks
        /// </summary>
        public int ProjectLengthInWeeks { get; set; } = 12;

        /// <summary>
        /// Default sprint length in weeks
        /// </summary>
        public int SprintLengthInWeeks { get; set; } = 1;
    }
}





