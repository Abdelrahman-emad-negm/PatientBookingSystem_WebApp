using System;
using System.ComponentModel.DataAnnotations;

namespace PatientBooking.Models
{
    public class WorkingHour
    {
        [Key]
        public int WorkingHourId { get; set; }

        [Required]
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }

        [Required(ErrorMessage = "Day of the week is required")]
        [MaxLength(20)]
        [Display(Name = "Day of Week")]
        public string DayOfWeek { get; set; } // مثال: "Monday", "Tuesday"...

        [Required(ErrorMessage = "Start time is required")]
        [DataType(DataType.Time)]
        [Display(Name = "Start Time")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "End time is required")]
        [DataType(DataType.Time)]
        [Display(Name = "End Time")]
        public TimeSpan EndTime { get; set; }
    }
}
