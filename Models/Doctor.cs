using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PatientBooking.Models
{
    [Index(nameof(UserId), IsUnique = true)] // ✅ كل User يربط بدكتور واحد فقط
    public class Doctor
    {
        [Key]
        public int DoctorId { get; set; }

        // 🔑 الربط مع جدول المستخدمين (إجباري)
        [Required(ErrorMessage = "User ID is required")]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        // 🩺 التخصص باستخدام Enum
        [Required(ErrorMessage = "Specialty is required")]
        public SpecialtyEnum Specialty { get; set; }

        [StringLength(255)]
        [Display(Name = "Profile Photo")]
        public string? Photo { get; set; }  // مسار الصورة (URL أو Local Path)

        [StringLength(2000)]
        [Display(Name = "Short CV")]
        public string? ShortCV { get; set; }

        // ⏰ جدول مواعيد العمل
        public ICollection<WorkingHour> WorkingHours { get; set; } = new List<WorkingHour>();

        // 📅 جدول الحجوزات
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
