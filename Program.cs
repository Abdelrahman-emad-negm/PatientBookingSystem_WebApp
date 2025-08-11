using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using PatientBooking.Data;

var builder = WebApplication.CreateBuilder(args);

// ✅ Add services to the container.
builder.Services.AddControllersWithViews();

// ✅ Configure SQL Server connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Configure Session (يفضل تكون مع Authentication)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ✅ Configure Cookie Authentication (هذا المطلوب لحل المشكلة)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";           // إعادة التوجيه للLogin عند عدم المصادقة
        options.LogoutPath = "/Account/Logout";         // مسار تسجيل الخروج
        options.AccessDeniedPath = "/Account/AccessDenied"; // مسار الرفض
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);  // انتهاء صلاحية الكوكيز
        options.SlidingExpiration = true;               // تجديد تلقائي للكوكيز
        options.Cookie.HttpOnly = true;                 // حماية من XSS
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ✅ Configure the HTTP request pipeline - الترتيب مهم جداً!
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ⚠️ الترتيب الصحيح مهم جداً
app.UseSession();           // Session قبل Authentication
app.UseAuthentication();    // Authentication قبل Authorization  
app.UseAuthorization();     // Authorization في النهاية

// ✅ Default route → بدء من Login بدلاً من Register
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();