namespace ZISK.Client.Pages.SampleData;
public static class ExcusesSampleData
{
    public class Excuse
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public DateTime? DateTo { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CoachComment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static List<Excuse> GetExcuses()
    {
        return new List<Excuse>
        {
            new()
            {
                Id = 1,
                Date = DateTime.Now.AddDays(-7),
                Reason = "Choroba",
                Status = "Schválené",
                CoachComment = "V poriadku, dúfam že si sa uzdravil.",
                Note = "Chrípka s teplotou",
                CreatedAt = DateTime.Now.AddDays(-8)
            },
            new()
            {
                Id = 2,
                Date = DateTime.Now.AddDays(-3),
                DateTo = DateTime.Now.AddDays(-1),
                Reason = "Rodinné dôvody",
                Status = "Schválené",
                CoachComment = "Rozumiem, bez problémov.",
                Note = "Rodinná udalosť - svadba",
                CreatedAt = DateTime.Now.AddDays(-5)
            },
            new()
            {
                Id = 3,
                Date = DateTime.Now.AddDays(-1),
                Reason = "Zranenie",
                Status = "Čaká na schválenie",
                Note = "Vyvrtnutý členok pri včerajšom zápase",
                CreatedAt = DateTime.Now.AddDays(-1)
            },
            new()
            {
                Id = 4,
                Date = DateTime.Now.AddDays(-5),
                Reason = "Škola/Štúdium",
                Status = "Zamietnuté",
                CoachComment = "Tréning je priorita. Skúšky si môžeš naplánovať inak.",
                Note = "Predtermínové skúšky",
                CreatedAt = DateTime.Now.AddDays(-6)
            },
            new()
            {
                Id = 5,
                Date = DateTime.Now,
                Reason = "Choroba",
                Status = "Čaká na schválenie",
                Note = "Vysoká teplota 38.5°C",
                CreatedAt = DateTime.Now.AddHours(-2)
            },
            new()
            {
                Id = 6,
                Date = DateTime.Now.AddDays(2),
                Reason = "Rodinné dôvody",
                Status = "Čaká na schválenie",
                Note = "Narodeniny babky",
                CreatedAt = DateTime.Now.AddHours(-1)
            }
        };
    }
}