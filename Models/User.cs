using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PatientBooking.Models
{
    public enum UserRole
    {
        Patient,
        Doctor,
        Admin
    }

    [Index(nameof(Email), IsUnique = true)] // جعل الإيميل فريد
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number format")]
        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required")]
        public UserRole Role { get; set; } = UserRole.Patient; // افتراضي مريض

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // علاقة One-to-One مع Doctor (اختياري)
        public Doctor? DoctorProfile { get; set; }
    }
}
