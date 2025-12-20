namespace ZISK.Client.Pages.SampleData;

public static class TrainingScheduleSampleData
{
    public class Training
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Coach { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public static List<Training> GetTrainings()
    {
        return new List<Training>
        {
            new()
            {
                Id = 1,
                Title = "Tréning A-tímu",
                Date = DateTime.Now.AddDays(1).AddHours(18),
                Coach = "Ján Novák",
                Location = "Hlavná hala",
                Type = "Kondičný",
                Team = "a-team",
                Description = "Zameranie na vytrvalosť a silu. Rozcvička 15 min, kardio 30 min, posilňovňa 45 min."
            },
            new()
            {
                Id = 2,
                Title = "Tréning B-tímu",
                Date = DateTime.Now.AddDays(2).AddHours(17),
                Coach = "Peter Horváth",
                Location = "Vedľajšia hala",
                Type = "Technický",
                Team = "b-team",
                Description = "Práca s loptou, prihrávky. Dôraz na techniku a presnosť."
            },
            new()
            {
                Id = 3,
                Title = "Tréning žiakov",
                Date = DateTime.Now.AddDays(3).AddHours(16),
                Coach = "Mária Kováčová",
                Location = "Hlavná hala",
                Type = "Herný",
                Team = "youth",
                Description = "Prípravné zápasy. Taktické cvičenia 4 vs 4."
            },
            new()
            {
                Id = 4,
                Title = "Tréning A-tímu",
                Date = DateTime.Now.AddDays(4).AddHours(18),
                Coach = "Ján Novák",
                Location = "Hlavná hala",
                Type = "Herný",
                Team = "a-team",
                Description = "Taktické cvičenia. Príprava na víkendový zápas."
            },
            new()
            {
                Id = 5,
                Title = "Tréning B-tímu",
                Date = DateTime.Now.AddDays(5).AddHours(17),
                Coach = "Peter Horváth",
                Location = "Vedľajšia hala",
                Type = "Kondičný",
                Team = "b-team",
                Description = "Kardio a posilňovanie. Interval tréning."
            },
            new()
            {
                Id = 6,
                Title = "Tréning žiakov",
                Date = DateTime.Now.AddDays(6).AddHours(16),
                Coach = "Mária Kováčová",
                Location = "Vonkajšie ihrisko",
                Type = "Technický",
                Team = "youth",
                Description = "Dribling a strieľanie. Individuálna technika."
            }
        };
    }
}