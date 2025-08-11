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
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // ✅ دالة محدثة للتحقق من صلاحية الأدمن
        private bool IsAdmin()
        {
            // ✅ التحقق من الـ Claims أولاً
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Admin")
                return true;

            // ✅ التحقق من Session كنسخة احتياطية
            return HttpContext.Session.GetString("UserRole") == "Admin";
        }

        // ✅ Dashboard موحد فيه الدكاترة والحجوزات
        [Authorize(Roles = "Admin")] // ✅ حماية إضافية للأدمن فقط
        public IActionResult AdminDashboard()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var doctors = _context.Doctors
                                  .Include(d => d.User)
                                  .Include(d => d.Specialty)
                                  .ToList();

            var bookings = _context.Appointments
                                   .Include(a => a.Doctor)
                                   .ThenInclude(d => d.User)
                                   .Include(a => a.Patient)
                                   .Where(a => a.Status == "Pending" || a.Status == "Confirmed")
                                   .OrderByDescending(a => a.Date)
                                   .ToList();

            var specialties = _context.Specialties.ToList();

            // ✅ إرسال بيانات المستخدم للـ View
            ViewBag.AdminName = User.FindFirst(ClaimTypes.Name)?.Value ??
                               HttpContext.Session.GetString("UserName");

            // نرجع كـ Tuple للـ View الحالي
            return View(Tuple.Create(doctors, bookings));
        }

        // ✅ إضافة دكتور جديد - GET
        [Authorize(Roles = "Admin")]
        public IActionResult AddDoctor()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Specialties = _context.Specialties.ToList();
            return View();
        }

        // ✅ إضافة دكتور جديد - POST
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddDoctor(Doctor doctor, string DoctorName, string DoctorEmail, string DoctorPassword)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                try
                {
                    // ✅ التحقق من عدم وجود الإيميل مسبقاً
                    if (_context.Users.Any(u => u.Email == DoctorEmail))
                    {
                        ViewBag.Error = "Email already exists!";
                        ViewBag.Specialties = _context.Specialties.ToList();
                        return View(doctor);
                    }

                    // ✅ إنشاء حساب المستخدم للدكتور
                    var user = new User
                    {
                        Name = DoctorName,
                        Email = DoctorEmail,
                        Password = new Microsoft.AspNetCore.Identity.PasswordHasher<User>()
                                      .HashPassword(null, DoctorPassword),
                        Role = "Doctor"
                    };

                    _context.Users.Add(user);
                    _context.SaveChanges();

                    // ✅ إنشاء ملف الدكتور
                    doctor.UserId = user.UserId;
                    _context.Doctors.Add(doctor);
                    _context.SaveChanges();

                    return RedirectToAction("AdminDashboard");
                }
                catch (Exception ex)
                {
                    ViewBag.Error = "Error adding doctor: " + ex.Message;
                }
            }

            ViewBag.Specialties = _context.Specialties.ToList();
            return View(doctor);
        }

        // ✅ تعديل بيانات دكتور - GET
        [Authorize(Roles = "Admin")]
        public IActionResult EditDoctor(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var doctor = _context.Doctors
                                 .Include(d => d.User)
                                 .FirstOrDefault(d => d.DoctorId == id);

            if (doctor == null)
                return NotFound();

            ViewBag.Specialties = _context.Specialties.ToList();
            return View(doctor);
        }

        // ✅ تعديل بيانات دكتور - POST
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult EditDoctor(Doctor doctor, string DoctorName, string DoctorEmail)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingDoctor = _context.Doctors
                                                 .Include(d => d.User)
                                                 .FirstOrDefault(d => d.DoctorId == doctor.DoctorId);

                    if (existingDoctor != null)
                    {
                        // ✅ تحديث بيانات المستخدم
                        existingDoctor.User.Name = DoctorName;
                        existingDoctor.User.Email = DoctorEmail;

                        // ✅ تحديث بيانات الدكتور
                        existingDoctor.SpecialtyId = doctor.SpecialtyId;
                        existingDoctor.Photo = doctor.Photo;
                        existingDoctor.ShortCV = doctor.ShortCV;

                        _context.SaveChanges();
                    }

                    return RedirectToAction("AdminDashboard");
                }
                catch (Exception ex)
                {
                    ViewBag.Error = "Error updating doctor: " + ex.Message;
                }
            }

            ViewBag.Specialties = _context.Specialties.ToList();
            return View(doctor);
        }

        // ✅ حذف دكتور
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteDoctor(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var doctor = _context.Doctors
                                 .Include(d => d.User)
                                 .FirstOrDefault(d => d.DoctorId == id);

            if (doctor != null)
            {
                // حذف حساب الدكتور من جدول Users (سيحذف الدكتور تلقائياً بسبب Cascade)
                _context.Users.Remove(doctor.User);
                _context.SaveChanges();
            }

            return RedirectToAction("AdminDashboard");
        }

        // ✅ إدارة التخصصات - GET
        [Authorize(Roles = "Admin")]
        public IActionResult ManageSpecialties()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var specialties = _context.Specialties.ToList();
            return View(specialties);
        }

        // ✅ إضافة تخصص جديد - POST
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddSpecialty(string Name)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (!string.IsNullOrWhiteSpace(Name))
            {
                var specialty = new Specialty { Name = Name };
                _context.Specialties.Add(specialty);
                _context.SaveChanges();
            }

            return RedirectToAction("ManageSpecialties");
        }

        // ✅ حذف تخصص
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteSpecialty(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var specialty = _context.Specialties.FirstOrDefault(s => s.SpecialtyId == id);
            if (specialty != null)
            {
                _context.Specialties.Remove(specialty);
                _context.SaveChanges();
            }

            return RedirectToAction("ManageSpecialties");
        }

        // ✅ إدارة المواعيد
        [Authorize(Roles = "Admin")]
        public IActionResult ManageAppointments()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var appointments = _context.Appointments
                                       .Include(a => a.Doctor)
                                       .ThenInclude(d => d.User)
                                       .Include(a => a.Patient)
                                       .Include(a => a.Doctor.Specialty)
                                       .Where(a => a.Status == "Pending")
                                       .OrderBy(a => a.Date)
                                       .ThenBy(a => a.TimeSlot)
                                       .ToList();

            return View(appointments);
        }

        // ✅ تأكيد موعد
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult ConfirmAppointment(int appointmentId)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Unauthorized" });

            var appointment = _context.Appointments.FirstOrDefault(a => a.AppointmentId == appointmentId);
            if (appointment != null && appointment.Status == "Pending")
            {
                appointment.Status = "Confirmed";
                _context.SaveChanges();
                return Json(new { success = true, message = "Appointment confirmed" });
            }

            return Json(new { success = false, message = "Appointment not found or already processed" });
        }

        // ✅ رفض موعد
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult RejectAppointment(int appointmentId)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Unauthorized" });

            var appointment = _context.Appointments.FirstOrDefault(a => a.AppointmentId == appointmentId);
            if (appointment != null && appointment.Status == "Pending")
            {
                appointment.Status = "Rejected";
                _context.SaveChanges();
                return Json(new { success = true, message = "Appointment rejected" });
            }

            return Json(new { success = false, message = "Appointment not found or already processed" });
        }
    }
}