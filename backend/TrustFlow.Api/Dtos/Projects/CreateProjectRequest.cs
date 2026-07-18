
using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Projects
{
    public class CreateProjectRequest
    {
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(5000)]
        public string Description { get; set; } = string.Empty;

        [Range(1, double.MaxValue)]
        public decimal Budget { get; set; }

        public DateTime DeadLine { get; set; }
    }
}