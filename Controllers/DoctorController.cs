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

        private int? GetDoctorIdFromUser()
        {
            try
            {
                if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) &&
                    User.FindFirstValue(ClaimTypes.Role) == "Doctor")
                {
                    return _context.Doctors.FirstOrDefault(d => d.UserId == userId)?.DoctorId;
                }

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

            var allAppointments = _context.Appointments
                                          .Include(a => a.Patient)
                                          .Where(a => a.DoctorId == doctorId.Value)
                                          .OrderByDescending(a => a.Date)
                                          .ThenBy(a => a.TimeSlot)
                                          .ToList();

            // ✅ فلترة حسب الحالات
            var grouped = allAppointments
                .GroupBy(a => a.Status)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ✅ إضافة بيانات الدكتور للـ View
            ViewBag.Doctor = doctor;
            ViewBag.DoctorName = doctor.User?.Name ?? "Doctor";
            ViewBag.Specialty = doctor.Specialty.ToString();
            ViewBag.Photo = doctor.Photo ?? "/images/default-doctor.png";
            ViewBag.Error = TempData["Error"];
            ViewBag.Success = TempData["Success"];

            return View(grouped);
        }

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

            // ✅ الشرط الجديد: لو التاريخ هو النهاردة، مينفعش تبدأ قبل الوقت الحالي
            if (date.Date == DateTime.Today && startTime < DateTime.Now.TimeOfDay)
            {
                TempData["Error"] = "You cannot add slots before the current time.";
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
                        Status = AppointmentStatus.Pending, // ✅ يبدأ Pending لحد ما الأدمن يوافق
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

                _logger.LogInformation("{Count} new slots added by Doctor {DoctorId}, {Skipped} skipped.",
                    newSlots.Count, doctorId, skipped);

                var msg = $"{newSlots.Count} slot(s) added successfully.";
                if (skipped > 0) msg += $" ({skipped} skipped)";
                TempData["Success"] = msg;
            }
            else
            {
                TempData["Error"] = "No new slots added (all already exist).";
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
                                                        (a.Status == AppointmentStatus.Pending ||
                                                         a.Status == AppointmentStatus.Confirmed ||
                                                         a.Status == AppointmentStatus.Available))
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
        public IActionResult MarkAsCompleted(int appointmentId)
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return Json(new { success = false, message = "Doctor not found" });

            var appointment = _context.Appointments
                                      .FirstOrDefault(a => a.AppointmentId == appointmentId && a.DoctorId == doctorId.Value);

            if (appointment == null)
                return Json(new { success = false, message = "Appointment not found" });

            if (appointment.Status == AppointmentStatus.Confirmed)
            {
                appointment.Status = AppointmentStatus.Completed;
                _context.SaveChanges();

                _logger.LogInformation("Appointment {Id} marked completed by Doctor {DoctorId}", appointmentId, doctorId);

                return Json(new { success = true, message = "Appointment marked as completed." });
            }

            return Json(new { success = false, message = "Only confirmed appointments can be completed." });
        }

        public IActionResult DeleteAppointment(int id)
        {
            var doctorId = GetDoctorIdFromUser();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var appointment = _context.Appointments.FirstOrDefault(a =>
                a.AppointmentId == id &&
                a.DoctorId == doctorId.Value &&
                (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Available));

            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                _context.SaveChanges();

                _logger.LogInformation("Doctor {DoctorId} deleted slot {AppointmentId}", doctorId, id);

                TempData["Success"] = "Appointment slot deleted.";
            }
            else
            {
                TempData["Error"] = "Appointment not found or cannot be deleted.";
            }

            return RedirectToAction("DoctorDashboard");
        }
    }
}
