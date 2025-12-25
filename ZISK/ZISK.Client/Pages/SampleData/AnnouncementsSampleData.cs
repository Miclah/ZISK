namespace ZISK.Client.Pages.SampleData;
public static class AnnouncementsSampleData
{
    public class Announcement
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string AuthorId { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public string Priority { get; set; } = "Stredná";
        public bool IsPinned { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ValidUntil { get; set; }  
        public int ViewCount { get; set; }

        public string TargetAudience { get; set; } = "Všetci";  
        public List<Attachment> Attachments { get; set; } = new();

        public bool IsRead { get; set; } = false;
    }

    public class Attachment  
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; 
        public string Url { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public static List<Announcement> GetAnnouncements()
    {
        return new List<Announcement>
        {
            new()
            {
                Id = 1,
                Title = "⚠️ DÔLEŽITÉ: Zrušený tréning dnes večer",
                Content = "Dnešný večerný tréning o 18:00 je zrušený z dôvodu nepriaznivého počasia. " +
                          "Tréning sa preloží na zajtrajší deň o 17:00. Prosím všetkých členov o potvrdenie účasti.",
                AuthorId = "coach1",
                AuthorName = "Ján Novák",
                Team = "Všetky",
                TargetAudience = "Všetci",
                Priority = "Vysoká",
                IsPinned = true,
                CreatedAt = DateTime.Now.AddHours(-1),
                ViewCount = 24,
                IsRead = false
            },
            new()
            {
                Id = 2,
                Title = "Nový tréningový rozvrh od pondelka",
                Content = "Od budúceho pondelka vstupuje do platnosti nový tréningový rozvrh. " +
                          "A-tím bude trénovať Pondelok a Streda 18:00-20:00, B-tím Utorok a Štvrtok 17:00-19:00. " +
                          "Žiaci majú tréning každý piatok o 16:00. Rozvrh nájdete v sekcii Rozvrh tréningov.",
                AuthorName = "Mária Kováčová",
                AuthorId = "coach2",
                Team = "Všetky",
                TargetAudience = "Všetci",
                Priority = "Stredná",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-1),
                ValidUntil = DateTime.Now.AddDays(7),
                ViewCount = 45,
                IsRead = true,
                Attachments = new List<Attachment>
                {
                    new() { Id = 1, Name = "Rozvrh_2024.pdf", Type = "pdf", Url = "/files/rozvrh.pdf", SizeBytes = 245000 }
                }
            },
            new()
            {
                Id = 3,
                Title = "Registrácia na letný turnaj",
                Content = "Prebieha registrácia na letný turnaj v Bratislave (15.-17. jún). " +
                          "Uzávierka prihlášok je tento piatok. Registrujte sa prosím cez formulár " +
                          "alebo kontaktujte trénera Petra Horvátha.",
                AuthorName = "Peter Horváth",
                AuthorId = "coach3",
                Team = "A-tím",
                TargetAudience = "Športovci",
                Priority = "Stredná",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-3),
                ValidUntil = DateTime.Now.AddDays(2),
                ViewCount = 32,
                IsRead = true,
                Attachments = new List<Attachment>
                {
                    new() { Id = 2, Name = "Prihláška_turnaj.pdf", Type = "pdf", Url = "/files/prihlaska.pdf", SizeBytes = 128000 },
                    new() { Id = 3, Name = "Info_turnaj.docx", Type = "doc", Url = "/files/info.docx", SizeBytes = 56000 }
                }
            },
            new()
            {
                Id = 4,
                Title = "Nové dresy k dispozícii",
                Content = "Prišli nové klubové dresy. Môžete si ich vyzdvihnúť u trénera Jána Nováka " +
                          "počas nasledujúcich tréningov. Cena: 25 EUR.",
                AuthorName = "Ján Novák",
                AuthorId = "coach1",
                Team = "Všetky",
                TargetAudience = "Rodičia",
                Priority = "Nízka",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-5),
                ViewCount = 67,
                IsRead = true,
                Attachments = new List<Attachment>
                {
                    new() { Id = 4, Name = "dresy_foto.jpg", Type = "image", Url = "/files/dresy.jpg", SizeBytes = 1200000 }
                }
            },
            new()
            {
                Id = 5,
                Title = "Zasadnutie výboru klubu",
                Content = "Pozývame všetkých členov výboru na mesačné zasadnutie, ktoré sa uskutoční " +
                          "vo štvrtok 20.12. o 19:00 v klubovni. Program: Finančná správa, plán na Q1 2024.",
                AuthorName = "Admin Systém",
                AuthorId = "admin1",
                Team = "Všetky",
                TargetAudience = "Všetci",
                Priority = "Nízka",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-7),
                ViewCount = 15,
                IsRead = false
            },
            new()
            {
                Id = 6,
                Title = "Žiaci: Príprava na súťaž",
                Content = "Pripomíname žiakom extra tréning v sobotu 9:00 ako prípravu na regionálnu súťaž. " +
                          "Prosíme rodičov o včasnú dopravu detí. Nezabudnite na pitný režim a vhodnú výbavu.",
                AuthorName = "Mária Kováčová",
                AuthorId = "coach2",
                Team = "Žiaci",
                TargetAudience = "Rodičia",
                Priority = "Vysoká",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-2),
                ViewCount = 28,
                IsRead = false
            }
        };
    }
}