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
        /// Whether UI/UX Designer (Type=3) is required (exactly 1)
        /// </summary>
        public bool RequireUIUXDesigner { get; set; } = true;

        /// <summary>
        /// Whether Product Manager (Type=4) is required (exactly 1)
        /// </summary>
        public bool RequireProductManager { get; set; } = false;

        /// <summary>
        /// Whether developer rule is required
        /// </summary>
        public bool RequireDeveloperRule { get; set; } = true;

        /// <summary>
        /// Max time in hours a student can remain in pending state
        /// </summary>
        public int MaxPendingTime { get; set; } = 96;
    }
}


