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
        public int ViewCount { get; set; }
    }

    public static List<Announcement> GetAnnouncements()
    {
        return new List<Announcement>
        {
            new()
            {
                Id = 1,
                Title = "DÔLEŽITÉ: Zrušený tréning dnes večer",
                Content = "Dnešný večerný tréning o 18:00 je zrušený z dôvodu nepriaznivého počasia. " +
                          "Tréning sa preloží na zajtrajší deň o 17:00. Prosím všetkých členov o potvrdenie účasti.",
                AuthorId = "coach1",
                AuthorName = "Ján Novák",
                Team = "Všetky",
                Priority = "Vysoká",
                IsPinned = true,
                CreatedAt = DateTime.Now.AddHours(-1),
                ViewCount = 24
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
                Priority = "Stredná",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-1),
                ViewCount = 45
            },
            new()
            {
                Id = 3,
                Title = "Registrácia na letný turnaj",
                Content = "Prebiehala registrácia na letný turnaj v Bratislave (15.-17. jún). " +
                          "Uzávierka prihlášok je tento piatok. Registrujte sa prosím cez formulár " +
                          "alebo kontaktujte trénera Petra Horvátha.",
                AuthorName = "Peter Horváth",
                AuthorId = "coach3",
                Team = "A-tím",
                Priority = "Stredná",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-3),
                ViewCount = 32
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
                Priority = "Nízka",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-5),
                ViewCount = 67
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
                Priority = "Nízka",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-7),
                ViewCount = 15
            },
            new()
            {
                Id = 6,
                Title = "Žiaci: Príprava na súťaž",
                Content = "Pripomíname žiakom extra tréning v sobotu 9:00 ako prípravu na regionálnu súťaž. " +
                          "Prosíme rodičov o včasnú dopravy detí.",
                AuthorName = "Mária Kováčová",
                AuthorId = "coach2",
                Team = "Žiaci",
                Priority = "Vysoká",
                IsPinned = false,
                CreatedAt = DateTime.Now.AddDays(-2),
                ViewCount = 28
            }
        };
    }
}