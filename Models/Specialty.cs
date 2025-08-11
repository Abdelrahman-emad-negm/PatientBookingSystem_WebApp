using System.ComponentModel.DataAnnotations;

namespace PatientBooking.Models
{
    public class Specialty
    {
        [Key]
        public int SpecialtyId { get; set; }

        [Required(ErrorMessage = "Specialty name is required")]
        [StringLength(100, ErrorMessage = "Specialty name cannot be longer than 100 characters")]
        [Display(Name = "Specialty Name")]
        public string Name { get; set; }

        public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
    }
}
