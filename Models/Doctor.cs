using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PatientBooking.Models
{
    public class Doctor
    {
        [Key]
        public int DoctorId { get; set; }

        // الربط مع جدول المستخدمين
        [Required(ErrorMessage = "User ID is required")]
        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; }

        // الربط مع جدول التخصصات (اختياري)
        [ForeignKey("Specialty")]
        [Display(Name = "Specialty")]
        public int? SpecialtyId { get; set; }
        public Specialty? Specialty { get; set; }

        [StringLength(255)]
        [Display(Name = "Profile Photo")]
        public string? Photo { get; set; }

        [StringLength(1000)]
        [Display(Name = "Short CV")]
        public string? ShortCV { get; set; }

        public ICollection<WorkingHour> WorkingHours { get; set; } = new List<WorkingHour>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
