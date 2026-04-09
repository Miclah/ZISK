using ZISK.Services;

namespace ZISK.Tests;

public class UsernameGeneratorTests
{
    [Theory]
    [InlineData("Ján", "Novák", "jan.novak")]
    [InlineData("Peter", "Horváth", "peter.horvath")]
    [InlineData("Mária", "Kováčová", "maria.kovacova")]
    [InlineData("Ľuboš", "Štefánik", "lubos.stefanik")]
    [InlineData("Žofia", "Čierná", "zofia.cierna")]
    [InlineData("Anna", "O'Brien", "anna.obrien")]
    [InlineData("Josef", "Müller", "josef.muller")]
    public void BuildBase_RemovesDiacriticsAndLowercases(string first, string last, string expected)
    {
        var result = UsernameGenerator.BuildBase(first, last);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Jan", "Novak", "jan.novak")]
    [InlineData("PETER", "HORVATH", "peter.horvath")]
    public void BuildBase_HandlesUpperCase(string first, string last, string expected)
    {
        var result = UsernameGenerator.BuildBase(first, last);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Jan Pavol", "De La Cruz", "janpavol.delacruz")]
    [InlineData("Mary-Jane", "Smith-Jones", "maryjane.smithjones")]
    public void BuildBase_StripsSpacesAndHyphens(string first, string last, string expected)
    {
        var result = UsernameGenerator.BuildBase(first, last);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "Novak", "user.novak")]
    [InlineData("Jan", "", "jan.user")]
    public void BuildBase_FallsBackForEmptySegments(string first, string last, string expected)
    {
        var result = UsernameGenerator.BuildBase(first, last);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildBase_ProducesDotSeparatedFormat()
    {
        var result = UsernameGenerator.BuildBase("Jan", "Novak");
        Assert.Contains(".", result);
        Assert.Equal(2, result.Split('.').Length);
    }
}
