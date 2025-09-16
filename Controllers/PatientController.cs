using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Data;
using PatientBooking.Models;
using System.Security.Claims;
using System.Text.Json;

namespace PatientBooking.Controllers
{
    [Authorize(Roles = "Patient")]
    public class PatientController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public PatientController(AppDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        private int? GetPatientIdFromUser()
        {
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) &&
                User.FindFirstValue(ClaimTypes.Role) == "Patient")
                return userId;

            var sessionRole = HttpContext.Session.GetString("UserRole");
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            return (sessionRole == "Patient") ? sessionUserId : null;
        }

        // ✅ Dashboard: Available Appointments
        public IActionResult Dashboard()
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            var availableAppointments = _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.Status == AppointmentStatus.Available && a.Date >= DateTime.Today)
                .OrderBy(a => a.Date).ThenBy(a => a.TimeSlot)
                .ToList();

            ViewBag.PatientName = User.FindFirstValue(ClaimTypes.Name)
                                   ?? HttpContext.Session.GetString("UserName")
                                   ?? "Patient";
            ViewBag.PatientId = patientId;

            return View(availableAppointments);
        }

        // ✅ Search by Specialty
        public IActionResult Booking()
        {
            var specialties = Enum.GetValues(typeof(SpecialtyEnum))
                .Cast<SpecialtyEnum>()
                .Select(s => s.ToString())
                .ToList();

            return View(specialties);
        }

        // ✅ Advanced Booking (AI-powered)
        // ✅ Advanced Booking
        [HttpGet]
        public IActionResult AdvancedBooking()
        {
            var specialties = Enum.GetValues(typeof(SpecialtyEnum))
                                  .Cast<SpecialtyEnum>()
                                  .ToList();

            return View(specialties); // نرجع Model مش فاضي
        }

        [HttpPost]
        public async Task<IActionResult> AdvancedBooking(string symptoms)
        {
            if (string.IsNullOrWhiteSpace(symptoms))
            {
                ViewBag.Error = "⚠ Please describe your symptoms.";
                // لازم نرجع Model برضو هنا
                var specialties = Enum.GetValues(typeof(SpecialtyEnum))
                                      .Cast<SpecialtyEnum>()
                                      .ToList();
                return View(specialties);
            }

            var specialtiesList = Enum.GetNames(typeof(SpecialtyEnum)).ToList();

            string prompt = $"Given the following list of medical specialties: {string.Join(", ", specialtiesList)}, " +
                            $"and the patient's symptoms: '{symptoms}', suggest the most relevant specialties.";

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync("http://localhost:11434/api/generate",
                    new StringContent(JsonSerializer.Serialize(new
                    {
                        model = "llama3",
                        prompt = prompt,
                        stream = false
                    }),
                    System.Text.Encoding.UTF8,
                    "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = "❌ Could not process your request. Please try again later.";
                    var specialties = Enum.GetValues(typeof(SpecialtyEnum))
                                          .Cast<SpecialtyEnum>()
                                          .ToList();
                    return View(specialties);
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var gptResult = JsonDocument.Parse(jsonResponse).RootElement.GetProperty("response").GetString();

                ViewBag.SuggestedSpecialties = gptResult;
                ViewBag.Symptoms = symptoms;
            }
            catch
            {
                ViewBag.Error = "❌ AI service not available. Please try again later.";
            }

            // برضو هنا لازم نرجع Model
            var model = Enum.GetValues(typeof(SpecialtyEnum))
                            .Cast<SpecialtyEnum>()
                            .ToList();

            return View(model);
        }


        // ✅ Get Doctors by Specialty - Fixed Version
        [HttpGet]
        public IActionResult GetDoctorsBySpecialty(string specialty)
        {
            try
            {
                // التحقق من وجود القيمة
                if (string.IsNullOrEmpty(specialty))
                {
                    return Json(new { error = "Specialty is required" });
                }

                // تحويل الـ string إلى enum
                if (!Enum.TryParse<SpecialtyEnum>(specialty, true, out var specialtyEnum))
                {
                    return Json(new { error = $"Invalid specialty: {specialty}" });
                }

                // جلب الدكاترة
                var doctors = _context.Doctors
                    .AsNoTracking()
                    .Include(d => d.User)
                    .Where(d => d.Specialty == specialtyEnum)
                    .Select(d => new
                    {
                        doctorId = d.DoctorId,  // تأكد من الاسم مطابق للـ JavaScript
                        name = d.User.Name,
                        shortCV = d.ShortCV,
                        photo = d.Photo ?? "/images/default-doctor.png",
                        specialty = d.Specialty.ToString()
                    })
                    .ToList();

                return Json(doctors);
            }
            catch (Exception ex)
            {
                // لو حصل أي خطأ
                return Json(new { error = $"Server error: {ex.Message}" });
            }
        }
        // ✅ Get Available Slots for a Doctor
        [HttpGet]
        [HttpGet]
        public IActionResult GetAvailableSlots(int doctorId, string date)
        {
            try
            {
                // التحقق من الـ parameters
                if (doctorId <= 0)
                {
                    return Json(new { error = "Invalid doctor ID" });
                }

                if (string.IsNullOrWhiteSpace(date))
                {
                    return Json(new { error = "Date is required" });
                }

                // تحويل الـ string إلى DateTime
                if (!DateTime.TryParse(date, out DateTime parsedDate))
                {
                    return Json(new { error = "Invalid date format" });
                }

                // تحقق إن التاريخ مش في الماضي
                if (parsedDate.Date < DateTime.Today)
                {
                    return Json(new { error = "Cannot book appointments in the past" });
                }

                // جلب المواعيد المتاحة
                var slots = _context.Appointments
                    .AsNoTracking()
                    .Where(a => a.DoctorId == doctorId &&
                                a.Date.Date == parsedDate.Date &&
                                a.Status == AppointmentStatus.Available)
                    .OrderBy(a => a.TimeSlot)
                    .Select(a => new
                    {
                        appointmentId = a.AppointmentId, // غير الاسم عشان يتطابق مع الـ JavaScript
                        timeSlot = a.TimeSlot.ToString(@"hh\:mm"), // إزالة الـ backslash الزائد
                        date = a.Date.ToString("yyyy-MM-dd")
                    })
                    .ToList();

                return Json(slots);
            }
            catch (Exception ex)
            {
                // إرجاع JSON response مش HTML error
                return Json(new
                {
                    error = "Server error occurred",
                    details = ex.Message
                });
            }
        }

        // ✅ Book Appointment
        [HttpPost]
        public IActionResult Book(int id)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            var appointment = _context.Appointments
                .FirstOrDefault(a => a.AppointmentId == id && a.Status == AppointmentStatus.Available);

            if (appointment == null)
            {
                TempData["Error"] = "❌ This appointment is no longer available.";
                return RedirectToAction("Dashboard");
            }

            if (appointment.Date.Date < DateTime.Today ||
                (appointment.Date.Date == DateTime.Today && appointment.TimeSlot < DateTime.Now.TimeOfDay))
            {
                TempData["Error"] = "⚠ You cannot book a past or expired appointment.";
                return RedirectToAction("Dashboard");
            }

            appointment.Status = AppointmentStatus.Pending;
            appointment.PatientId = patientId.Value;
            _context.SaveChanges();

            TempData["BookingSuccess"] = $"✅ Booking request sent for {appointment.Date:dd/MM/yyyy} at {appointment.TimeSlot:hh\\:mm}.";
            return RedirectToAction("Dashboard");
        }

        // ✅ My Appointments
        public IActionResult MyAppointments()
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            var appointments = _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.PatientId == patientId.Value)
                .OrderByDescending(a => a.Date).ThenByDescending(a => a.TimeSlot)
                .ToList();

            return View(appointments);
        }

        // ✅ Cancel Appointment
        [HttpPost]
        public IActionResult CancelAppointment(int appointmentId)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null)
                return Json(new { success = false, message = "⚠ Patient not found" });

            var appointment = _context.Appointments
                .FirstOrDefault(a => a.AppointmentId == appointmentId && a.PatientId == patientId.Value);

            if (appointment == null)
                return Json(new { success = false, message = "❌ Appointment not found" });

            if (appointment.Date.Date < DateTime.Today ||
                (appointment.Date.Date == DateTime.Today && appointment.TimeSlot <= DateTime.Now.TimeOfDay))
                return Json(new { success = false, message = "⚠ Cannot cancel past or ongoing appointments" });

            appointment.Status = AppointmentStatus.Cancelled;
            _context.SaveChanges();

            return Json(new { success = true, message = "✅ Appointment cancelled successfully" });
        }
    }
}
