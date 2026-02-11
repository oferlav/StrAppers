using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class StakeholderStatus
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Stakeholder> Stakeholders { get; set; } = new List<Stakeholder>();
}
