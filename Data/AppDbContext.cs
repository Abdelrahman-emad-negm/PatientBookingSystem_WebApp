using Microsoft.EntityFrameworkCore;
using PatientBooking.Models;

namespace PatientBooking.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<WorkingHour> WorkingHours { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ✅ جعل الإيميل فريد
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // ✅ Doctor ↔ User (One-to-One)
            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.User)
                .WithOne(u => u.DoctorProfile) // ربطنا بالعلاقة اللي جوه User
                .HasForeignKey<Doctor>(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ❌ ما فيش جدول Specialties (اتحول Enum)

            // ✅ Appointment ↔ Patient (Many-to-One) - Optional
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany() // مريض ممكن يبقى عنده كذا موعد
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict) // ما نلغيهوش لو المريض اتمسح
                .IsRequired(false);

            // ✅ Appointment ↔ Doctor (Many-to-One)
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ نخزن الحالة كـ string ونخلي default Available
            modelBuilder.Entity<Appointment>()
                .Property(a => a.Status)
                .HasConversion<string>()
                .HasDefaultValue(AppointmentStatus.Available);

            // ✅ WorkingHour ↔ Doctor (Many-to-One)
            modelBuilder.Entity<WorkingHour>()
                .HasOne(w => w.Doctor)
                .WithMany(d => d.WorkingHours)
                .HasForeignKey(w => w.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
