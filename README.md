# Patient Booking System v2.0 🏥

A comprehensive Hospital Information System (HIS) for managing patient appointments, doctor schedules, and healthcare administration.

[![.NET Core](https://img.shields.io/badge/.NET%20Core-6.0-blue.svg)](https://dotnet.microsoft.com/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2019-red.svg)](https://www.microsoft.com/en-us/sql-server)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.0-purple.svg)](https://getbootstrap.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## 📋 Table of Contents
- [Overview](#overview)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [System Architecture](#system-architecture)
- [Installation](#installation)
- [Usage](#usage)
- [Database Schema](#database-schema)
- [API Endpoints](#api-endpoints)
- [Screenshots](#screenshots)
- [Contributing](#contributing)
- [License](#license)
- [Acknowledgments](#acknowledgments)

## 🎯 Overview

The Patient Booking System is a web-based healthcare management application that streamlines the appointment booking process between patients and doctors. The system features three distinct user roles with comprehensive functionality for healthcare administration.

### Key Objectives
- Simplify appointment scheduling for patients
- Provide doctors with efficient schedule management
- Enable administrators to oversee system operations
- Ensure secure and reliable healthcare data management

## ✨ Features

### 👨‍💼 Admin Panel
- **Doctor Management**: Add, edit, and remove doctors with photos and CV uploads
- **Slot Approval**: Review and approve/reject doctor availability slots
- **Appointment Control**: Manage patient bookings (confirm, cancel, complete)
- **System Analytics**: Dashboard with comprehensive statistics
- **Data Export**: Export appointment data to CSV format

### 👨‍⚕️ Doctor Portal
- **Schedule Management**: Set available time slots with automatic 30-minute interval generation
- **Daily View**: Monitor today's appointments and patient information
- **Weekly Calendar**: Comprehensive weekly schedule overview
- **Patient History**: Access to patient booking history

### 👤 Patient Interface
- **Easy Booking**: Browse available doctors and time slots
- **Appointment History**: Track past and upcoming appointments
- **Doctor Reviews**: Rate and review doctors after consultations
- **Specialty Search**: Find doctors by medical specialty
- **Status Tracking**: Real-time appointment status updates

## 🛠 Tech Stack

### Backend
- **Framework**: ASP.NET Core 6.0 MVC
- **Database**: SQL Server 2019
- **ORM**: Entity Framework Core
- **Authentication**: ASP.NET Core Identity
- **Password Hashing**: BCrypt.Net

### Frontend
- **UI Framework**: Bootstrap 5.0
- **JavaScript**: Vanilla JS with AJAX
- **Icons**: Font Awesome
- **Styling**: Custom CSS with responsive design

### Tools & Libraries
- **File Upload**: IFormFile for image handling
- **Data Validation**: ASP.NET Core Model Validation
- **Security**: Anti-forgery tokens, CSRF protection

## 🏗 System Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Presentation  │    │    Business     │    │   Data Access   │
│      Layer      │◄──►│     Layer       │◄──►│      Layer      │
│                 │    │                 │    │                 │
│ • Controllers   │    │ • Services      │    │ • DbContext     │
│ • Views         │    │ • Models        │    │ • Repositories  │
│ • ViewModels    │    │ • Validation    │    │ • Migrations    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Database Entities
- **Users**: System authentication and authorization
- **Doctors**: Doctor profiles and specialties
- **Patients**: Patient information and preferences
- **Appointments**: Booking records and status tracking

## 🚀 Installation

### Prerequisites
- .NET 6.0 SDK or later
- SQL Server 2019 or SQL Server Express
- Visual Studio 2022 or VS Code
- Git

### Setup Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/patient-booking-system.git
   cd patient-booking-system
   ```

2. **Configure Database Connection**
   ```json
   // appsettings.json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PatientBookingDB;Trusted_Connection=true;MultipleActiveResultSets=true"
     }
   }
   ```

3. **Install Dependencies**
   ```bash
   dotnet restore
   ```

4. **Update Database**
   ```bash
   dotnet ef database update
   ```

5. **Run the Application**
   ```bash
   dotnet run
   ```

6. **Access the Application**
   - Open browser and navigate to `https://localhost:5001`
   - Default admin credentials will be seeded in the database

## 📊 Database Schema

```sql
Users
├── UserId (PK)
├── Name
├── Email (Unique Index)
├── Password (BCrypt Hashed)
└── Role (Admin/Doctor/Patient)

Doctors
├── DoctorId (PK)
├── UserId (FK) - One-to-One with Users
├── Specialty (Enum: Cardiology, Dermatology, etc.)
├── ShortCV
└── Photo

Appointments
├── AppointmentId (PK)
├── DoctorId (FK) - Many-to-One with Doctors
├── PatientId (FK) - Optional, Many-to-One with Users
├── Date
├── TimeSlot (TimeSpan)
├── Status (Available/Pending/Confirmed/Cancelled/Completed)
└── CreatedAt (DateTime)

WorkingHours
├── WorkingHourId (PK)
├── DoctorId (FK) - Many-to-One with Doctors
├── DayOfWeek
├── StartTime
└── EndTime
```

### Entity Relationships
- **User ↔ Doctor**: One-to-One relationship
- **Doctor ↔ Appointments**: One-to-Many relationship
- **User (Patient) ↔ Appointments**: One-to-Many relationship (Optional)
- **Doctor ↔ WorkingHours**: One-to-Many relationship

### Key Constraints
- Email addresses are unique across all users
- Appointment status defaults to "Available"
- Cascade delete on Doctor removes all related appointments and working hours
- Patient deletion is restricted to preserve appointment history

## 🔌 API Endpoints

### Admin Routes
```
POST /Admin/SaveDoctor          - Create/Update doctor
POST /Admin/DeleteDoctor        - Remove doctor
POST /Admin/ApproveSlot         - Approve time slot
POST /Admin/RejectSlot          - Reject time slot
GET  /Admin/ExportAppointments  - Export data to CSV
```

### Doctor Routes
```
POST /Doctor/CreateSlots        - Generate time slots
GET  /Doctor/TodayAppointments  - Get daily schedule
GET  /Doctor/WeeklySchedule     - Get weekly calendar
```

### Patient Routes
```
POST /Patient/BookAppointment   - Create booking
GET  /Patient/MyAppointments    - Get booking history
POST /Patient/RateDoctor        - Submit doctor review
```

## 📱 Screenshots

### Admin Dashboard
![Admin Dashboard](screenshots/admin-dashboard.png)

### Doctor Schedule
![Doctor Schedule](screenshots/doctor-schedule.png)

### Patient Booking
![Patient Booking](screenshots/patient-booking.png)

## 🤝 Contributing

We welcome contributions! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Development Guidelines
- Follow C# coding conventions
- Write clear commit messages
- Add unit tests for new features
- Update documentation for API changes

## 🔮 Roadmap

- [ ] **AI Integration**: Symptom-based specialty recommendations
- [ ] **Mobile App**: React Native companion app
- [ ] **Telemedicine**: Video consultation integration
- [ ] **Payment Gateway**: Online payment processing
- [ ] **SMS Notifications**: Appointment reminders
- [ ] **Multi-language**: Arabic and English support

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **[Paxera](https://paxera.com)** - For providing the training opportunity and development environment
- **Eng. Kareem** - Technical mentorship and project guidance
- **Eng. Enas** - Valuable advice and motivation throughout development
- **Open Source Community** - For the amazing tools and libraries used in this project

## 👨‍💻 Author

**Your Name**
- GitHub: [@yourusername](https://github.com/yourusername)
- LinkedIn: [Your LinkedIn](https://linkedin.com/in/yourprofile)
- Email: your.email@example.com

---

⭐ If you found this project helpful, please give it a star!

## 📞 Support

If you have any questions or need support, please:
- Open an issue on GitHub
- Contact me via email
- Connect on LinkedIn

**Built with ❤️ in Egypt**
