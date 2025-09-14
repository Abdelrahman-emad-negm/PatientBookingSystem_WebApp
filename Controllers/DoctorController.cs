using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Data;
using PatientBooking.Models;
using System.Security.Claims;

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

        /// <summary>
        /// استرجاع رقم الدكتور الحالي من الـ Claims أو الـ Session
        /// </summary>
        private int? GetDoctorIdFromUser()
        {
            try
            {
                // من الـ Claims
                if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) &&
                    User.FindFirstValue(ClaimTypes.Role) == "Doctor")
                {
                    return _context.Doctors.FirstOrDefault(d => d.UserId == userId)?.DoctorId;
                }

                // من الـ Session (fallback)
                var sessionUserId = HttpContext.Session.GetInt32("UserId");
                var sessionRole = HttpContext.Session.GetString("UserRole");
                if (sessionUserId.HasValue && sessionRole == "Doctor")
                {
                    return _context.Doctors.FirstOrDefault(d => d.UserId == sessionUserId.Value)?.DoctorId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DoctorId");
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
                                 .FirstOrDefault(d => d.DoctorId == doctorId.Value);

            if (doctor == null)
            {
                _logger.LogWarning("Doctor record not found for DoctorId: {DoctorId}", doctorId);
                return RedirectToAction("Login", "Account");
            }

            var appointments = _context.Appointments
                                       .Include(a => a.Patient)
                                       .Where(a => a.DoctorId == doctorId.Value)
                                       .OrderByDescending(a => a.Date)
                                       .ThenBy(a => a.TimeSlot)
                                       .ToList();

            ViewBag.Doctor = doctor;
            ViewBag.DoctorName = doctor.User?.Name ?? "Doctor";
            ViewBag.Specialty = doctor.Specialty.ToString();
            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(Tuple.Create(new Appointment(), appointments));
        }

        /// <summary>
        /// يبني slots كل 30 دقيقة بين startTime و endTime في التاريخ المحدد
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAppointmentsRange(DateTime date, TimeSpan startTime, TimeSpan endTime)
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null)
                return RedirectToAction("Login", "Account");

            startTime = new TimeSpan(startTime.Hours, startTime.Minutes, 0);
            endTime = new TimeSpan(endTime.Hours, endTime.Minutes, 0);

            if (endTime <= startTime)
            {
                TempData["Error"] = "End time must be after start time.";
                return RedirectToAction("DoctorDashboard");
            }

            if (date.Date < DateTime.Today)
            {
                TempData["Error"] = "You cannot add slots in the past.";
                return RedirectToAction("DoctorDashboard");
            }

            var slotLength = TimeSpan.FromMinutes(30);
            var newSlots = new List<Appointment>();
            int skipped = 0;

            for (var time = startTime; time < endTime; time = time.Add(slotLength))
            {
                bool exists = _context.Appointments.Any(a =>
                    a.DoctorId == doctorId.Value &&
                    a.Date == date.Date &&
                    a.TimeSlot == time);

                if (!exists)
                {
                    newSlots.Add(new Appointment
                    {
                        DoctorId = doctorId.Value,
                        Date = date.Date,
                        TimeSlot = time,
                        Status = AppointmentStatus.Available,
                        PatientId = null
                    });
                }
                else
                {
                    skipped++;
                }
            }

            if (newSlots.Any())
            {
                _context.Appointments.AddRange(newSlots);
                _context.SaveChanges();

                var msg = $"{newSlots.Count} appointment slot(s) added successfully.";
                if (skipped > 0) msg += $" ({skipped} existing slot(s) skipped)";
                TempData["Success"] = msg;
            }
            else
            {
                TempData["Error"] = "No new slots added (all selected slots already exist).";
            }

            return RedirectToAction("DoctorDashboard");
        }

        public IActionResult TodayAppointments()
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var today = DateTime.Today;
            var todayAppointments = _context.Appointments
                                            .Include(a => a.Patient)
                                            .Where(a => a.DoctorId == doctorId.Value &&
                                                        a.Date == today &&
                                                        (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
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
        [ValidateAntiForgeryToken]
        public IActionResult UpdateAppointmentStatus(int appointmentId, string status)
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return Json(new { success = false, message = "Doctor not found" });

            if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
                return Json(new { success = false, message = "Invalid status" });

            var appointment = _context.Appointments.FirstOrDefault(a =>
                a.AppointmentId == appointmentId && a.DoctorId == doctorId.Value);

            if (appointment == null)
                return Json(new { success = false, message = "Appointment not found" });

            appointment.Status = parsedStatus;
            _context.SaveChanges();

            return Json(new { success = true, message = "Appointment status updated successfully" });
        }

        public IActionResult DeleteAppointment(int id)
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var appointment = _context.Appointments.FirstOrDefault(a =>
                a.AppointmentId == id &&
                a.DoctorId == doctorId.Value &&
                a.Status == AppointmentStatus.Available);

            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                _context.SaveChanges();
                TempData["Success"] = "Appointment deleted.";
            }
            else
            {
                TempData["Error"] = "Appointment not found or cannot be deleted.";
            }

            return RedirectToAction("DoctorDashboard");
        }
    }
}
