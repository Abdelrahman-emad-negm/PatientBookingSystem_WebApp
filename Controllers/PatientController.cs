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

        private string GetPatientName()
        {
            return User.FindFirstValue(ClaimTypes.Name)
                   ?? HttpContext.Session.GetString("UserName")
                   ?? "Patient";
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

            ViewBag.PatientName = GetPatientName();
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
        [HttpGet]
        public IActionResult AdvancedBooking(int? selectedDoctorId = null)
        {
            var specialties = Enum.GetValues(typeof(SpecialtyEnum))
                                  .Cast<SpecialtyEnum>()
                                  .ToList();

            if (selectedDoctorId.HasValue)
            {
                ViewBag.SelectedDoctorId = selectedDoctorId.Value;
            }

            return View(specialties);
        }

        [HttpPost]
        public async Task<IActionResult> AdvancedBooking(string symptoms)
        {
            if (string.IsNullOrWhiteSpace(symptoms))
            {
                ViewBag.Error = "⚠ Please describe your symptoms.";
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

            var model = Enum.GetValues(typeof(SpecialtyEnum))
                            .Cast<SpecialtyEnum>()
                            .ToList();

            return View(model);
        }

        // ✅ Get Doctors by Specialty (Fixed for enum values)
        [HttpGet]
        public IActionResult GetDoctorsBySpecialty(int specialty)
        {
            try
            {
                if (!Enum.IsDefined(typeof(SpecialtyEnum), specialty))
                {
                    return Json(new { error = $"Invalid specialty value: {specialty}" });
                }

                var specialtyEnum = (SpecialtyEnum)specialty;

                var doctors = _context.Doctors
                    .AsNoTracking()
                    .Include(d => d.User)
                    .Where(d => d.Specialty == specialtyEnum)
                    .Select(d => new
                    {
                        doctorId = d.DoctorId,
                        name = d.User.Name,
                        shortCV = d.ShortCV ?? "No CV provided",
                        photo = d.Photo ?? "/images/default-doctor.png",
                        specialty = d.Specialty.ToString()
                    })
                    .ToList();

                return Json(doctors);
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Server error: {ex.Message}" });
            }
        }

        // ✅ Get Available Slots for a Doctor (Fixed TimeSpan formatting)
        [HttpGet]
        public IActionResult GetAvailableSlots(int doctorId, string date)
        {
            try
            {
                if (doctorId <= 0)
                {
                    return Json(new { error = "Invalid doctor ID" });
                }

                if (string.IsNullOrWhiteSpace(date))
                {
                    return Json(new { error = "Date is required" });
                }

                if (!DateTime.TryParse(date, out DateTime parsedDate))
                {
                    return Json(new { error = "Invalid date format" });
                }

                if (parsedDate.Date < DateTime.Today)
                {
                    return Json(new { error = "Cannot book appointments in the past" });
                }

                var slots = _context.Appointments
                    .AsNoTracking()
                    .Where(a => a.DoctorId == doctorId &&
                                a.Date.Date == parsedDate.Date &&
                                a.Status == AppointmentStatus.Available)
                    .OrderBy(a => a.TimeSlot)
                    .Select(a => new
                    {
                        appointmentId = a.AppointmentId,
                        timeSlot = string.Format("{0:D2}:{1:D2}", a.TimeSlot.Hours, a.TimeSlot.Minutes),
                        date = a.Date.ToString("yyyy-MM-dd")
                    })
                    .ToList();

                return Json(slots);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Server error occurred", details = ex.Message });
            }
        }

        // ✅ Book Appointment (Enhanced with better messages)
        [HttpPost]
        public IActionResult Book(int id)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            var appointment = _context.Appointments
                .Include(a => a.Doctor)
                .ThenInclude(d => d.User)
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

            // Check if patient already has appointment with this doctor on same day
            var existingAppointment = _context.Appointments
                .Any(a => a.PatientId == patientId.Value &&
                         a.DoctorId == appointment.DoctorId &&
                         a.Date.Date == appointment.Date.Date &&
                         (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed));

            if (existingAppointment)
            {
                TempData["Error"] = "⚠ You already have an appointment with this doctor on the same day.";
                return RedirectToAction("Dashboard");
            }

            appointment.Status = AppointmentStatus.Pending;
            appointment.PatientId = patientId.Value;
            _context.SaveChanges();

            var doctorName = appointment.Doctor?.User?.Name ?? "Doctor";
            var timeFormat = string.Format("{0:D2}:{1:D2}", appointment.TimeSlot.Hours, appointment.TimeSlot.Minutes);

            TempData["BookingSuccess"] = $"✅ Booking request sent for Dr. {doctorName} on {appointment.Date:dd/MM/yyyy} at {timeFormat}.";
            return RedirectToAction("MyAppointments");
        }

        // ✅ My Appointments (Enhanced)
        public IActionResult MyAppointments(int page = 1, int pageSize = 10)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            var appointmentsQuery = _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.PatientId == patientId.Value)
                .OrderByDescending(a => a.Date).ThenByDescending(a => a.TimeSlot);

            // pagination
            var totalCount = appointmentsQuery.Count();
            var appointments = appointmentsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.PatientName = GetPatientName();

            return View(appointments);
        }

        // ✅ Cancel Appointment (Enhanced with redirect)
        [HttpPost]
        public IActionResult CancelAppointment(int appointmentId)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null)
            {
                TempData["Error"] = "⚠ Patient not found";
                return RedirectToAction("MyAppointments");
            }

            var appointment = _context.Appointments
                .Include(a => a.Doctor)
                .ThenInclude(d => d.User)
                .FirstOrDefault(a => a.AppointmentId == appointmentId && a.PatientId == patientId.Value);

            if (appointment == null)
            {
                TempData["Error"] = "❌ Appointment not found";
                return RedirectToAction("MyAppointments");
            }

            var appointmentDateTime = appointment.Date.Add(appointment.TimeSlot);

            if (appointmentDateTime <= DateTime.Now.AddHours(24))
            {
                TempData["Error"] = "⚠ You can only cancel appointments at least 24 hours in advance";
                return RedirectToAction("MyAppointments");
            }

            if (appointment.Status != AppointmentStatus.Confirmed)
            {
                TempData["Error"] = "⚠ Only confirmed appointments can be cancelled";
                return RedirectToAction("MyAppointments");
            }

            appointment.Status = AppointmentStatus.Cancelled;
            appointment.PatientId = null; // Make slot available again
            _context.SaveChanges();

            var doctorName = appointment.Doctor?.User?.Name ?? "Doctor";
            TempData["Success"] = $"✅ Appointment with Dr. {doctorName} cancelled successfully";
            return RedirectToAction("MyAppointments");
        }

        // ✅ Rate Appointment (Show Rating Form)
        [HttpGet]
        public IActionResult RateAppointment(int appointmentId)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            var appointment = _context.Appointments
                .Include(a => a.Doctor)
                .ThenInclude(d => d.User)
                .FirstOrDefault(a => a.AppointmentId == appointmentId
                    && a.PatientId == patientId.Value
                    && a.Status == AppointmentStatus.Completed);

            if (appointment == null)
            {
                TempData["Error"] = "❌ Appointment not found or not completed";
                return RedirectToAction("MyAppointments");
            }

            if (appointment.Rating.HasValue)
            {
                TempData["Info"] = "⭐ You have already rated this appointment";
                return RedirectToAction("MyAppointments");
            }

            ViewBag.AppointmentId = appointmentId;
            ViewBag.DoctorName = appointment.Doctor?.User?.Name ?? "Doctor";
            ViewBag.AppointmentDate = appointment.Date.ToString("dd/MM/yyyy");
            ViewBag.AppointmentTime = string.Format("{0:D2}:{1:D2}", appointment.TimeSlot.Hours, appointment.TimeSlot.Minutes);

            return View();
        }

        // ✅ Submit Rating
        [HttpPost]
        public IActionResult SubmitRating(int appointmentId, int rating, string reviewComment)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            if (rating < 1 || rating > 5)
            {
                TempData["Error"] = "❌ Rating must be between 1 and 5 stars";
                return RedirectToAction("RateAppointment", new { appointmentId });
            }

            var appointment = _context.Appointments
                .Include(a => a.Doctor)
                .ThenInclude(d => d.User)
                .FirstOrDefault(a => a.AppointmentId == appointmentId
                    && a.PatientId == patientId.Value
                    && a.Status == AppointmentStatus.Completed);

            if (appointment == null)
            {
                TempData["Error"] = "❌ Appointment not found or not completed";
                return RedirectToAction("MyAppointments");
            }

            if (appointment.Rating.HasValue)
            {
                TempData["Error"] = "⚠️ You have already rated this appointment";
                return RedirectToAction("MyAppointments");
            }

            // Save rating
            appointment.Rating = rating;
            appointment.ReviewComment = reviewComment?.Trim();
            appointment.RatedAt = DateTime.Now;
            _context.SaveChanges();

            var doctorName = appointment.Doctor?.User?.Name ?? "Doctor";
            TempData["Success"] = $"⭐ Thank you for rating Dr. {doctorName}! Your feedback helps us improve.";
            return RedirectToAction("MyAppointments");
        }

        // ✅ Book Again (New Action)
        [HttpPost]
        public IActionResult BookAgain(int doctorId)
        {
            var patientId = GetPatientIdFromUser();
            if (patientId == null) return RedirectToAction("Login", "Account");

            var doctor = _context.Doctors
                .Include(d => d.User)
                .FirstOrDefault(d => d.DoctorId == doctorId);

            if (doctor == null)
            {
                TempData["Error"] = "❌ Doctor not found";
                return RedirectToAction("MyAppointments");
            }

            TempData["Info"] = $"🔄 Redirecting to book another appointment with Dr. {doctor.User?.Name}";
            return RedirectToAction("AdvancedBooking", new { selectedDoctorId = doctorId });
        }
    }
}