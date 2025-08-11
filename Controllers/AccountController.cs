using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PatientBooking.Models;
using PatientBooking.Data;
using System.Security.Claims;
using System.Linq;

namespace PatientBooking.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // ✅ دالة مساعدة لتوجيه المستخدم حسب دوره
        private IActionResult RedirectUserByRole(string role)
        {
            return role switch
            {
                "Admin" => RedirectToAction("AdminDashboard", "Admin"),
                "Doctor" => RedirectToAction("DoctorDashboard", "Doctor"),
                "Patient" => RedirectToAction("Dashboard", "Patient"),
                _ => RedirectToAction("Login")
            };
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public async Task<IActionResult> Register(User user)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // ✅ تحقق من وجود الإيميل مسبقاً
                    if (_context.Users.Any(u => u.Email == user.Email))
                    {
                        ViewBag.Error = "⚠️ Email already exists!";
                        return View(user);
                    }

                    // ✅ تشفير كلمة المرور
                    var hasher = new PasswordHasher<User>();
                    user.Password = hasher.HashPassword(user, user.Password);

                    // ✅ تعيين Role افتراضي للمريض
                    if (string.IsNullOrEmpty(user.Role))
                    {
                        user.Role = "Patient";
                    }

                    // ✅ إضافة المستخدم في جدول Users
                    _context.Users.Add(user);
                    _context.SaveChanges();

                    // ✅ لو الدور Doctor أضف له سجل في جدول Doctors
                    if (string.Equals(user.Role, "Doctor", StringComparison.OrdinalIgnoreCase))
                    {
                        var doctor = new Doctor
                        {
                            UserId = user.UserId,
                            SpecialtyId = null, // دلوقتي ممكن تبقى null
                            Photo = null,
                            ShortCV = null
                        };
                        _context.Doctors.Add(doctor);
                        _context.SaveChanges();
                    }

                    // ✅ تسجيل الدخول تلقائياً باستخدام Cookie Authentication
                    await SignInUserAsync(user);

                    return RedirectUserByRole(user.Role);
                }
                catch (Exception ex)
                {
                    ViewBag.Error = "⚠️ Error saving user: " + ex.Message;
                }
            }
            else
            {
                ViewBag.Error = "Model is not valid.";
            }
            return View(user);
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ViewBag.Error = "Please enter both email and password.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == Email);
            if (user != null)
            {
                var hasher = new PasswordHasher<User>();
                var result = hasher.VerifyHashedPassword(user, user.Password, Password);

                if (result == PasswordVerificationResult.Success)
                {
                    // ✅ تسجيل الدخول باستخدام Cookie Authentication
                    await SignInUserAsync(user);

                    return RedirectUserByRole(user.Role);
                }
            }

            ViewBag.Error = "Invalid email or password.";
            return View();
        }

        // GET: /Account/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ✅ دالة مساعدة لتسجيل الدخول باستخدام Claims
        private async Task SignInUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
                });

            // ✅ حفظ البيانات في Session كنسخة احتياطية
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("UserName", user.Name);
        }
    }
}
