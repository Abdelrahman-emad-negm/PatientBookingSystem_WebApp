using System;
using System.ComponentModel.DataAnnotations;

namespace PatientBooking.Models
{
    public class Appointment
    {
        public int AppointmentId { get; set; }

        [Required]
        public int PatientId { get; set; }
        public User Patient { get; set; }

        [Required]
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }

        [Required(ErrorMessage = "Appointment date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Appointment Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? Date { get; set; } // Nullable لحل مشكلة الإدخال الفارغ

        [Required(ErrorMessage = "Time slot is required")]
        [DataType(DataType.Time)]
        [Display(Name = "Time Slot")]
        [DisplayFormat(DataFormatString = "{0:hh\\:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan? TimeSlot { get; set; } // Nullable لحل مشكلة الإدخال الفارغ

        [Required]
        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";
    }
}
