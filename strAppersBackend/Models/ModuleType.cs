using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models
{
    /// <summary>
    /// Represents a module type (e.g., Frontend, Backend, Database, etc.)
    /// </summary>
    public class ModuleType
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        // Navigation properties
        public virtual ICollection<ProjectModule> ProjectModules { get; set; } = new List<ProjectModule>();
    }

    /// <summary>
    /// Request model for creating a module type
    /// </summary>
    public class CreateModuleTypeRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for updating a module type
    /// </summary>
    public class UpdateModuleTypeRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}






