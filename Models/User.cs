using System.ComponentModel.DataAnnotations;

namespace PatientBooking.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [MaxLength(150)]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format")]
        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Role is required")]
        [MaxLength(50)]
        public string Role { get; set; }

        // علاقة One-to-One مع Doctor (لو المستخدم دكتور)
        public Doctor? DoctorProfile { get; set; }
    }
}
