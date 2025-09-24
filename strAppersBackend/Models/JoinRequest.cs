using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models
{
    public class JoinRequest
    {
        public int Id { get; set; }
        
        [Required]
        public string ChannelName { get; set; } = string.Empty;
        
        [Required]
        public string ChannelId { get; set; } = string.Empty;
        
        public int StudentId { get; set; }
        
        [Required]
        public string StudentEmail { get; set; } = string.Empty;
        
        public string? StudentFirstName { get; set; }
        
        public string? StudentLastName { get; set; }
        
        public int ProjectId { get; set; }
        
        public string? ProjectTitle { get; set; }
        
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;
        
        public bool Added { get; set; } = false;
        
        public DateTime? AddedDate { get; set; }
        
        public string? Notes { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        // Navigation properties
        public Student? Student { get; set; }
        public Project? Project { get; set; }
    }
}



