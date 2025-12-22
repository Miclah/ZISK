namespace ZISK.Client.Pages.SampleData;

public static class AttendanceSampleData
{
    public class Member
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public string Status { get; set; } = "Prítomný";
        public string Note { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class AttendanceRecord
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public int TrainingId { get; set; }
        public DateTime Date { get; set; }
        public string TrainingName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CoachComment { get; set; }
    }
    public static List<Member> GetMembers()
    {
        return new List<Member>
        {
            new() { Id = 1, Name = "Ján Novák", Team = "a-team", Status = "Prítomný", Email = "jan.novak@example.com" },
            new() { Id = 2, Name = "Peter Horváth", Team = "a-team", Status = "Neprítomný", Email = "peter.horvath@example.com" },
            new() { Id = 3, Name = "Mária Kováčová", Team = "b-team", Status = "Meškanie", Email = "maria.kovacova@example.com" },
            new() { Id = 4, Name = "Lucia Varga", Team = "youth", Status = "Ospravedlnený", Email = "lucia.varga@example.com" },
            new() { Id = 5, Name = "Martin Balog", Team = "a-team", Status = "Prítomný", Email = "martin.balog@example.com" },
            new() { Id = 6, Name = "Eva Tóthová", Team = "b-team", Status = "Prítomný", Email = "eva.tothova@example.com" },
            new() { Id = 7, Name = "Tomáš Németh", Team = "youth", Status = "Meškanie", Email = "tomas.nemeth@example.com" },
            new() { Id = 8, Name = "Zuzana Králová", Team = "a-team", Status = "Prítomný", Email = "zuzana.kralova@example.com" }
        };
    }

    public static List<AttendanceRecord> GetUserAttendance(int userId = 1)
    {
        return new List<AttendanceRecord>
        {
            new()
            {
                Id = 1,
                MemberId = userId,
                TrainingId = 1,
                Date = DateTime.Now.AddDays(-1),
                TrainingName = "Tréning A-tímu - Kondičný",
                Status = "Prítomný",
                CreatedAt = DateTime.Now.AddDays(-1)
            },
            new()
            {
                Id = 2,
                MemberId = userId,
                TrainingId = 2,
                Date = DateTime.Now.AddDays(-3),
                TrainingName = "Tréning A-tímu - Technický",
                Status = "Meškanie",
                Note = "Meškal 10 minút",
                CoachComment = "Príde skôr budúci krát.",
                CreatedAt = DateTime.Now.AddDays(-3)
            },
            new()
            {
                Id = 3,
                MemberId = userId,
                TrainingId = 3,
                Date = DateTime.Now.AddDays(-5),
                TrainingName = "Tréning A-tímu - Herný",
                Status = "Neprítomný",
                Note = "Choroba",
                CoachComment = "Ospravedlnený lekárskou správou.",
                CreatedAt = DateTime.Now.AddDays(-5)
            },
            new()
            {
                Id = 4,
                MemberId = userId,
                TrainingId = 4,
                Date = DateTime.Now.AddDays(-7),
                TrainingName = "Tréning A-tímu - Kondičný",
                Status = "Prítomný",
                CreatedAt = DateTime.Now.AddDays(-7)
            },
            new()
            {
                Id = 5,
                MemberId = userId,
                TrainingId = 5,
                Date = DateTime.Now.AddDays(-10),
                TrainingName = "Tréning A-tímu - Technický",
                Status = "Ospravedlnený",
                Note = "Rodinná udalosť",
                CoachComment = "V poriadku.",
                CreatedAt = DateTime.Now.AddDays(-10)
            },
            new()
            {
                Id = 6,
                MemberId = userId,
                TrainingId = 6,
                Date = DateTime.Now.AddDays(-12),
                TrainingName = "Tréning A-tímu - Herný",
                Status = "Prítomný",
                CreatedAt = DateTime.Now.AddDays(-12)
            }
        };
    }
}