using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Data;
using PatientBooking.Models;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace PatientBooking.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DoctorController> _logger;

        public DoctorController(AppDbContext context, ILogger<DoctorController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int? GetDoctorIdFromUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                var doctor = _context.Doctors.FirstOrDefault(d => d.UserId == userId);
                if (doctor != null) return doctor.DoctorId;
            }

            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId.HasValue)
            {
                var doctor = _context.Doctors.FirstOrDefault(d => d.UserId == sessionUserId.Value);
                if (doctor != null) return doctor.DoctorId;
            }

            _logger.LogWarning("DoctorId not found for current user");
            return null;
        }

        public IActionResult DoctorDashboard()
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var doctor = _context.Doctors
                                 .Include(d => d.User)
                                 .Include(d => d.Specialty)
                                 .FirstOrDefault(d => d.DoctorId == doctorId.Value);

            if (doctor == null) return RedirectToAction("Login", "Account");

            var appointments = _context.Appointments
                                       .Include(a => a.Patient)
                                       .Where(a => a.DoctorId == doctorId.Value)
                                       .OrderByDescending(a => a.Date)
                                       .ThenBy(a => a.TimeSlot)
                                       .ToList();

            ViewBag.Doctor = doctor;
            ViewBag.DoctorName = doctor.User?.Name ?? "Doctor";
            ViewBag.Specialty = doctor.Specialty?.Name ?? "No Specialty";
            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            var appointmentForm = new Appointment();
            return View(Tuple.Create(appointmentForm, appointments));
        }

        [HttpPost]
        public IActionResult AddAppointment(Appointment appointment)
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid appointment data.";
                return RedirectToAction("DoctorDashboard");
            }

            var exists = _context.Appointments.FirstOrDefault(a =>
                a.DoctorId == doctorId.Value &&
                a.Date.Date == appointment.Date.Date &&
                a.TimeSlot == appointment.TimeSlot);

            if (exists != null)
            {
                TempData["Error"] = "There is already an appointment at this time!";
                return RedirectToAction("DoctorDashboard");
            }

            appointment.DoctorId = doctorId.Value;
            appointment.Status = "Available";
            appointment.PatientId = 0;

            _context.Appointments.Add(appointment);
            _context.SaveChanges();

            TempData["Success"] = "Appointment added successfully!";
            return RedirectToAction("DoctorDashboard");
        }

        public IActionResult TodayAppointments()
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var todayAppointments = _context.Appointments
                                            .Include(a => a.Patient)
                                            .Where(a => a.DoctorId == doctorId.Value &&
                                                   a.Date.Date == DateTime.Today &&
                                                   a.Status == "Confirmed")
                                            .OrderBy(a => a.TimeSlot)
                                            .ToList();

            return View(todayAppointments);
        }

        public IActionResult WeeklySchedule()
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7);

            var weeklyAppointments = _context.Appointments
                                             .Include(a => a.Patient)
                                             .Where(a => a.DoctorId == doctorId.Value &&
                                                    a.Date >= startOfWeek &&
                                                    a.Date < endOfWeek)
                                             .OrderBy(a => a.Date)
                                             .ThenBy(a => a.TimeSlot)
                                             .ToList();

            ViewBag.StartOfWeek = startOfWeek;
            ViewBag.EndOfWeek = endOfWeek;

            return View(weeklyAppointments);
        }

        [HttpPost]
        public IActionResult UpdateAppointmentStatus(int appointmentId, string status)
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return Json(new { success = false, message = "Doctor not found" });

            var validStatuses = new[] { "Completed", "Cancelled", "No Show", "Confirmed" };
            if (!validStatuses.Contains(status)) return Json(new { success = false, message = "Invalid status" });

            var appointment = _context.Appointments.FirstOrDefault(a =>
                a.AppointmentId == appointmentId && a.DoctorId == doctorId.Value);

            if (appointment == null) return Json(new { success = false, message = "Appointment not found" });

            appointment.Status = status;
            _context.SaveChanges();

            return Json(new { success = true, message = "Appointment status updated successfully" });
        }

        public IActionResult DeleteAppointment(int id)
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var appointment = _context.Appointments.FirstOrDefault(a =>
                a.AppointmentId == id && a.DoctorId == doctorId.Value && a.Status == "Available");

            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                _context.SaveChanges();
            }

            return RedirectToAction("DoctorDashboard");
        }
    }
}
