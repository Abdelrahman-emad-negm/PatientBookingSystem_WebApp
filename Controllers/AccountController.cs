using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PatientBooking.Data;
using PatientBooking.Models;
using System.Security.Claims;

namespace PatientBooking.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        private IActionResult RedirectUserByRole(UserRole role)
        {
            return role switch
            {
                UserRole.Admin => RedirectToAction("AdminDashboard", "Admin"),
                UserRole.Doctor => RedirectToAction("DoctorDashboard", "Doctor"),
                UserRole.Patient => RedirectToAction("Dashboard", "Patient"),
                _ => RedirectToAction("Login")
            };
        }

        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(User user)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Please fill all required fields correctly.";
                return View(user);
            }

            // تأكد إن الإيميل فريد
            if (_context.Users.Any(u => u.Email == user.Email))
            {
                ViewBag.Error = "⚠️ Email already exists!";
                return View(user);
            }

            // أي مستخدم يسجل من هنا يبقى Patient
            user.Role = UserRole.Patient;

            // تشفير الباسورد بـ BCrypt
            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

            _context.Users.Add(user);
            _context.SaveChanges();

            await SignInUserAsync(user);
            return RedirectUserByRole(user.Role);
        }

        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ViewBag.Error = "Please enter both email and password.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == Email);
            if (user == null)
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            // ✅ التحقق بباسورد BCrypt
            if (BCrypt.Net.BCrypt.Verify(Password, user.Password))
            {
                await SignInUserAsync(user);
                return RedirectUserByRole(user.Role);
            }

            ViewBag.Error = "Invalid email or password.";
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private async Task SignInUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
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

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserRole", user.Role.ToString());
            HttpContext.Session.SetString("UserName", user.Name);
        }
    }
}
