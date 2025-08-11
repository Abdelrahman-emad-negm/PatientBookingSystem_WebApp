using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Data;
using PatientBooking.Models;
using System.Security.Claims;
using System.Linq;

namespace PatientBooking.Controllers
{
    [Authorize] // ✅ حماية كل الـ Controller
    public class PatientController : Controller
    {
        private readonly AppDbContext _context;

        public PatientController(AppDbContext context)
        {
            _context = context;
        }

        // ✅ دالة محدثة للحصول على Patient ID من Claims
        private int? GetPatientIdFromUser()
        {
            // ✅ التحقق من الـ Claims أولاً
            if (User.Identity.IsAuthenticated)
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userRole == "Patient" && int.TryParse(userIdClaim, out int userId))
                {
                    return userId;
                }
            }

            // ✅ التحقق من Session كنسخة احتياطية
            var sessionRole = HttpContext.Session.GetString("UserRole");
            var sessionUserId = HttpContext.Session.GetInt32("UserId");

            if (sessionRole == "Patient" && sessionUserId.HasValue)
            {
                return sessionUserId.Value;
            }

            return null;
        }

        // ✅ التحقق من أن المستخدم مريض
        private bool IsPatient()
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ??
                          HttpContext.Session.GetString("UserRole");
            return userRole == "Patient";
        }

        // ✅ Dashboard المريض
        [Authorize(Roles = "Patient")] // ✅ حماية إضافية للمرضى فقط
        public IActionResult Dashboard()
        {
            if (!IsPatient())
            {
                return RedirectToAction("Login", "Account");
            }

            var patientId = GetPatientIdFromUser();
            if (patientId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // ✅ عرض المواعيد المتاحة
            var availableAppointments = _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor.Specialty)
                .Where(a => a.Status == "Available" && a.Date >= DateTime.Today)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.TimeSlot)
                .ToList();

            // ✅ إرسال بيانات المستخدم للـ View
            ViewBag.PatientName = User.FindFirst(ClaimTypes.Name)?.Value ??
                                 HttpContext.Session.GetString("UserName");
            ViewBag.PatientId = patientId;

            return View(availableAppointments);
        }

        // ✅ صفحة الحجز - اختيار التخصص والدكتور
        [Authorize(Roles = "Patient")]
        public IActionResult Booking()
        {
            if (!IsPatient())
            {
                return RedirectToAction("Login", "Account");
            }

            // ✅ الحصول على جميع التخصصات
            var specialties = _context.Specialties
                .Include(s => s.Doctors)
                    .ThenInclude(d => d.User)
                .ToList();

            return View(specialties);
        }

        // ✅ الحصول على الأطباء حسب التخصص (AJAX)
        [HttpGet]
        [Authorize(Roles = "Patient")]
        public IActionResult GetDoctorsBySpecialty(int specialtyId)
        {
            var doctors = _context.Doctors
                .Include(d => d.User)
                .Where(d => d.SpecialtyId == specialtyId)
                .Select(d => new {
                    DoctorId = d.DoctorId,
                    Name = d.User.Name,
                    ShortCV = d.ShortCV
                })
                .ToList();

            return Json(doctors);
        }

        // ✅ الحصول على المواعيد المتاحة للطبيب (AJAX)
        [HttpGet]
        [Authorize(Roles = "Patient")]
        public IActionResult GetAvailableSlots(int doctorId, DateTime date)
        {
            // ✅ الحصول على ساعات عمل الطبيب
            var workingHours = _context.WorkingHours
                .Where(w => w.DoctorId == doctorId && w.DayOfWeek == date.DayOfWeek.ToString())
                .FirstOrDefault();

            if (workingHours == null)
            {
                return Json(new List<object>());
            }

            // ✅ الحصول على المواعيد المحجوزة
            var bookedAppointments = _context.Appointments
                .Where(a => a.DoctorId == doctorId &&
                           a.Date.Date == date.Date &&
                           a.Status != "Cancelled")
                .Select(a => a.TimeSlot)
                .ToList();

            // ✅ إنشاء قائمة المواعيد المتاحة (كل 30 دقيقة)
            var availableSlots = new List<object>();
            var currentTime = workingHours.StartTime;

            while (currentTime < workingHours.EndTime)
            {
                if (!bookedAppointments.Contains(currentTime))
                {
                    availableSlots.Add(new
                    {
                        TimeSlot = currentTime.ToString(@"hh\:mm"),
                        TimeSpanValue = currentTime
                    });
                }
                currentTime = currentTime.Add(TimeSpan.FromMinutes(30));
            }

            return Json(availableSlots);
        }

        // ✅ حجز موعد
        [HttpPost]
        [Authorize(Roles = "Patient")]
        public IActionResult Book(int id)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var appointment = _context.Appointments
                .FirstOrDefault(a => a.AppointmentId == id && a.Status == "Available");

            if (appointment != null)
            {
                appointment.Status = "Pending"; // في انتظار موافقة الإدارة
                appointment.PatientId = patientId.Value;
                _context.SaveChanges();
            }

            return RedirectToAction("Dashboard");
        }

        // ✅ عرض مواعيد المريض
        [Authorize(Roles = "Patient")]
        public IActionResult MyAppointments()
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var appointments = _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor.Specialty)
                .Where(a => a.PatientId == patientId.Value)
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.TimeSlot)
                .ToList();

            return View(appointments);
        }

        // ✅ إلغاء موعد
        [HttpPost]
        [Authorize(Roles = "Patient")]
        public IActionResult CancelAppointment(int appointmentId)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null)
            {
                return Json(new { success = false, message = "Patient not found" });
            }

            var appointment = _context.Appointments
                .FirstOrDefault(a => a.AppointmentId == appointmentId &&
                               a.PatientId == patientId.Value);

            if (appointment == null)
            {
                return Json(new { success = false, message = "Appointment not found" });
            }

            if (appointment.Date <= DateTime.Today)
            {
                return Json(new { success = false, message = "Cannot cancel past appointments" });
            }

            appointment.Status = "Cancelled";
            _context.SaveChanges();

            return Json(new { success = true, message = "Appointment cancelled successfully" });
        }
    }
}