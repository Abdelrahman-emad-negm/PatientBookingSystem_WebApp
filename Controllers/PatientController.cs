using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Data;
using PatientBooking.Models;
using System.Security.Claims;
using System.Text.Json;
using System.Net.Http;

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
            {
                return userId;
            }

            var sessionRole = HttpContext.Session.GetString("UserRole");
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            return (sessionRole == "Patient") ? sessionUserId : null;
        }

        public IActionResult Dashboard()
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            var availableAppointments = _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.Status == AppointmentStatus.Available && a.Date >= DateTime.Today)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.TimeSlot)
                .ToList();

            ViewBag.PatientName = User.FindFirstValue(ClaimTypes.Name)
                                  ?? HttpContext.Session.GetString("UserName")
                                  ?? "Patient";
            ViewBag.PatientId = patientId;

            return View(availableAppointments);
        }

        public IActionResult Booking()
        {
            // رجع Enum بدل الجدول القديم
            var specialties = Enum.GetValues(typeof(SpecialtyEnum))
                .Cast<SpecialtyEnum>()
                .Select(s => s.ToString())
                .ToList();

            return View(specialties);
        }

        // Advanced Booking Page
        [HttpGet]
        public IActionResult AdvancedBooking()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AdvancedBooking(string symptoms)
        {
            if (string.IsNullOrWhiteSpace(symptoms))
            {
                ViewBag.Error = "⚠ Please describe your symptoms.";
                return View();
            }

            // جلب التخصصات من Enum
            var specialtiesList = Enum.GetNames(typeof(SpecialtyEnum)).ToList();

            // Prepare GPT OSS prompt
            string prompt = $"Given the following list of medical specialties: {string.Join(", ", specialtiesList)}, " +
                            $"and the patient's symptoms: '{symptoms}', suggest the most relevant specialties.";

            // Call local GPT OSS API
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync("http://localhost:11434/api/generate",
                new StringContent(JsonSerializer.Serialize(new
                {
                    model = "llama3", // Example: llama3 or any local model
                    prompt = prompt,
                    stream = false
                }),
                System.Text.Encoding.UTF8,
                "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "❌ Could not process your request. Please try again later.";
                return View();
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var gptResult = JsonDocument.Parse(jsonResponse).RootElement.GetProperty("response").GetString();

            ViewBag.SuggestedSpecialties = gptResult;
            ViewBag.Symptoms = symptoms;
            return View();
        }

        [HttpGet]
        public IActionResult GetDoctorsBySpecialty(SpecialtyEnum specialty)
        {
            var doctors = _context.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .Where(d => d.Specialty == specialty)
                .Select(d => new
                {
                    d.DoctorId,
                    Name = d.User.Name,
                    d.ShortCV,
                    Photo = d.Photo ?? "/images/default-doctor.png"
                })
                .ToList();

            return Json(doctors);
        }

        [HttpGet]
        public IActionResult GetAvailableSlots(int doctorId, DateTime date)
        {
            var dayEnum = (DayOfWeekEnum)date.DayOfWeek;

            var workingHours = _context.WorkingHours
                .AsNoTracking()
                .FirstOrDefault(w => w.DoctorId == doctorId && w.DayOfWeek == dayEnum);

            if (workingHours == null)
                return Json(new List<object>());

            var startOfDay = date.Date;
            var startOfNextDay = startOfDay.AddDays(1);

            var bookedAppointments = _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId &&
                            a.Date >= startOfDay &&
                            a.Date < startOfNextDay &&
                            a.Status != AppointmentStatus.Cancelled)
                .Select(a => a.TimeSlot)
                .ToList();

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

            if (appointment.Date.Date < DateTime.Today)
            {
                TempData["Error"] = "⚠ You cannot book a past appointment.";
                return RedirectToAction("Dashboard");
            }

            appointment.Status = AppointmentStatus.Pending;
            appointment.PatientId = patientId.Value;
            _context.SaveChanges();

            TempData["BookingSuccess"] = $"✅ Booking request sent successfully for {appointment.Date:dd/MM/yyyy} at {appointment.TimeSlot:hh\\:mm}.";
            return RedirectToAction("Dashboard");
        }

        public IActionResult MyAppointments()
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            var appointments = _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.PatientId == patientId.Value)
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.TimeSlot)
                .ToList();

            return View(appointments);
        }

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

            if (appointment.Date.Date <= DateTime.Today)
                return Json(new { success = false, message = "⚠ Cannot cancel past appointments" });

            appointment.Status = AppointmentStatus.Cancelled;
            _context.SaveChanges();

            return Json(new { success = true, message = "✅ Appointment cancelled successfully" });
        }
    }
}
