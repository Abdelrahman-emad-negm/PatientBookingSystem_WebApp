using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PatientBooking.Models
{
    public enum AppointmentStatus
    {
        Available,   // الموعد متاح للحجز
        Pending,     // تم الحجز وينتظر موافقة الأدمن
        Confirmed,   // الأدمن وافق
        Cancelled,   // تم الإلغاء
        Rejected     // الأدمن رفض الحجز
    }

    public class Appointment
    {
        [Key]
        public int AppointmentId { get; set; }

        // ✅ جعل PatientId nullable لأن الموعد ممكن يكون لسه متحجزش
        public int? PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public User? Patient { get; set; }

        [Required]
        public int DoctorId { get; set; }

        [ForeignKey(nameof(DoctorId))]
        public Doctor Doctor { get; set; } = null!;

        [Required(ErrorMessage = "Appointment date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Appointment Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Time slot is required")]
        [DataType(DataType.Time)]
        [Display(Name = "Time Slot")]
        [DisplayFormat(DataFormatString = "{0:hh\\:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan TimeSlot { get; set; }

        [Required]
        [Display(Name = "Status")]
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Available;

        [StringLength(500)]
        [Display(Name = "Admin Notes")]
        public string? AdminNotes { get; set; }
    }
}
