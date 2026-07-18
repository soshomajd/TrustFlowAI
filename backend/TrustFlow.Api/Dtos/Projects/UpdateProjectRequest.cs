
using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Projects
{
    public class UpdateProjectRequest
    {
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;
        [Required]
        [MaxLength(3000)]
        public string Description { get; set; } = string.Empty;

        [Range(1, double.MaxValue)]
        public decimal Budget { get; set; }

        public DateTime Deadline { get; set; }

    }
}