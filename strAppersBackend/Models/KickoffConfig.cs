namespace strAppersBackend.Models
{
    /// <summary>
    /// Configuration for kickoff logic settings
    /// </summary>
    public class KickoffConfig
    {
        /// <summary>
        /// Minimum number of students required for kickoff
        /// </summary>
        public int MinimumStudents { get; set; } = 2;

        /// <summary>
        /// Whether at least one student must be an admin
        /// </summary>
        public bool RequireAdmin { get; set; } = true;

        /// <summary>
        /// Whether UI/UX Designer (Type=3) is required
        /// </summary>
        public bool RequireUIUXDesigner { get; set; } = true;

        /// <summary>
        /// Whether developer rule is required
        /// </summary>
        public bool RequireDeveloperRule { get; set; } = true;
    }
}


