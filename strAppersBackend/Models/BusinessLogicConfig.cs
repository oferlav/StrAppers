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

        /// <summary>
        /// Maximum number of projects a student can select in their priority fields
        /// </summary>
        public int MaxProjectsSelection { get; set; } = 4;
    }
}





