using System.ComponentModel.DataAnnotations;

namespace FormDemo.Models
{
    public class UserFormModel
    {
        [Required(ErrorMessage = "Please enter your name")]
        [StringLength(100, ErrorMessage = "Name cannot be longer than 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot be longer than 100 characters")]
        public string Email { get; set; } = string.Empty;
    }
}