using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Data;
using PatientBooking.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PatientBooking.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public AdminController(AppDbContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }

        // ✅ Dashboard
        public IActionResult AdminDashboard()
        {
            var doctors = _context.Doctors
                .Include(d => d.User)
                .ToList();

            var bookings = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Patient)
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.TimeSlot)
                .ToList();

            var specialties = Enum.GetValues(typeof(SpecialtyEnum)).Cast<SpecialtyEnum>().ToList();

            ViewBag.AdminName = User.FindFirst(ClaimTypes.Name)?.Value;

            return View(Tuple.Create(doctors, bookings, specialties));
        }

        // ✅ Manage Doctors Page
        public IActionResult ManageDoctors()
        {
            var doctors = _context.Doctors
                .Include(d => d.User)
                .ToList();

            ViewBag.Specialties = Enum.GetValues(typeof(SpecialtyEnum)).Cast<SpecialtyEnum>().ToList();
            return View(doctors); // ↔️ هيتوجه لملف ManageDoctors.cshtml
        }

        // ✅ Save Doctor (Add / Edit)
        [HttpPost]
        public async Task<IActionResult> SaveDoctor(int DoctorId, string DoctorName, string DoctorEmail, string DoctorPassword, SpecialtyEnum Specialty, string ShortCV)
        {
            if (DoctorId == 0)
            {
                // ➕ Add Doctor
                if (_context.Users.Any(u => u.Email == DoctorEmail))
                {
                    TempData["Error"] = "Email already exists!";
                    return RedirectToAction("ManageDoctors");
                }

                var user = new User
                {
                    Name = DoctorName,
                    Email = DoctorEmail,
                    Password = BCrypt.Net.BCrypt.HashPassword(DoctorPassword),
                    Role = UserRole.Doctor
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var doctor = new Doctor
                {
                    UserId = user.UserId,
                    Specialty = Specialty,
                    ShortCV = ShortCV
                };

                // Upload photo
                if (Request.Form.Files.Count > 0)
                {
                    var file = Request.Form.Files[0];
                    if (file.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads/doctors");
                        if (!Directory.Exists(uploadsFolder))
                            Directory.CreateDirectory(uploadsFolder);

                        var uniqueFileName = $"{user.UserId}_{Path.GetFileName(file.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        doctor.Photo = $"/uploads/doctors/{uniqueFileName}";
                    }
                }

                if (string.IsNullOrEmpty(doctor.Photo))
                    doctor.Photo = "/images/default-doctor.png";

                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();
            }
            else
            {
                // ✏️ Edit Doctor
                var existingDoctor = _context.Doctors
                    .Include(d => d.User)
                    .FirstOrDefault(d => d.DoctorId == DoctorId);

                if (existingDoctor == null) return NotFound();

                existingDoctor.User.Name = DoctorName;
                existingDoctor.User.Email = DoctorEmail;
                existingDoctor.Specialty = Specialty;
                existingDoctor.ShortCV = ShortCV;

                if (!string.IsNullOrEmpty(DoctorPassword))
                    existingDoctor.User.Password = BCrypt.Net.BCrypt.HashPassword(DoctorPassword);

                // Update Photo
                if (Request.Form.Files.Count > 0)
                {
                    var file = Request.Form.Files[0];
                    if (file.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads/doctors");
                        if (!Directory.Exists(uploadsFolder))
                            Directory.CreateDirectory(uploadsFolder);

                        if (!string.IsNullOrEmpty(existingDoctor.Photo) && !existingDoctor.Photo.Contains("default-doctor.png"))
                        {
                            var oldFilePath = Path.Combine(_hostingEnvironment.WebRootPath, existingDoctor.Photo.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                                System.IO.File.Delete(oldFilePath);
                        }

                        var uniqueFileName = $"{existingDoctor.UserId}_{Path.GetFileName(file.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        existingDoctor.Photo = $"/uploads/doctors/{uniqueFileName}";
                    }
                }

                if (string.IsNullOrEmpty(existingDoctor.Photo))
                    existingDoctor.Photo = "/images/default-doctor.png";

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("ManageDoctors");
        }

        // ✅ Delete Doctor
        [HttpPost]
        public async Task<IActionResult> DeleteDoctor(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DoctorId == id);

            if (doctor != null)
            {
                if (!string.IsNullOrEmpty(doctor.Photo) && !doctor.Photo.Contains("default-doctor.png"))
                {
                    var filePath = Path.Combine(_hostingEnvironment.WebRootPath, doctor.Photo.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }

                _context.Users.Remove(doctor.User);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("ManageDoctors");
        }

        // ✅ Manage Appointments
        [HttpPost]
        public IActionResult ConfirmBooking(int id) => UpdateBookingStatus(id, AppointmentStatus.Confirmed);

        [HttpPost]
        public IActionResult RejectBooking(int id) => UpdateBookingStatus(id, AppointmentStatus.Rejected);

        [HttpPost]
        public IActionResult CancelBooking(int id) => UpdateBookingStatus(id, AppointmentStatus.Cancelled);

        private IActionResult UpdateBookingStatus(int id, AppointmentStatus status)
        {
            var appointment = _context.Appointments.FirstOrDefault(a => a.AppointmentId == id);
            if (appointment != null)
            {
                appointment.Status = status;
                _context.SaveChanges();
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        // ✅ Export Appointments (CSV)
        public IActionResult ExportAppointments()
        {
            var appointments = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Patient)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Date,Time,Doctor,Patient,Status");

            foreach (var a in appointments)
            {
                sb.AppendLine($"{a.Date:yyyy-MM-dd},{a.TimeSlot},{a.Doctor?.User?.Name},{a.Patient?.Name},{a.Status}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "appointments.csv");
        }
    }
}
