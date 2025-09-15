namespace strAppersBackend.Models
{
    public class SlackTeamCreationRequest
    {
        public int ProjectId { get; set; }
        public bool SendWelcomeMessage { get; set; } = true;
    }

    public class SlackTeamCreationResult
    {
        public bool Success { get; set; }
        public string? ChannelId { get; set; }
        public string? ChannelName { get; set; }
        public string? TeamName { get; set; }
        public string? ErrorMessage { get; set; }
        public List<SlackMemberResult> MemberResults { get; set; } = new List<SlackMemberResult>();
        public bool WelcomeMessageSent { get; set; }
    }

    public class SlackMemberResult
    {
        public int StudentId { get; set; }
        public string StudentEmail { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? SlackUserId { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Message { get; set; }
    }

    public class SlackTeamInfo
    {
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public string? ChannelId { get; set; }
        public string? ChannelName { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int TotalMembers { get; set; }
        public int AdminCount { get; set; }
        public int RegularMemberCount { get; set; }
        public List<SlackMemberInfo> Members { get; set; } = new List<SlackMemberInfo>();
    }

    public class SlackMemberInfo
    {
        public int StudentId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? SlackUserId { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsInSlack { get; set; }
    }
}

