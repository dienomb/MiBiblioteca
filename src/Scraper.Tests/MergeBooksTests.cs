using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Scraper.Tests;

public class MergeBooksTests
{
    private static MadridLibraryScraper.Book MakeBook(
        string title,
        DateTime? dueDate = null,
        string? author = null,
        string? coleccion = null,
        string? imageUrl = null,
        DateTime? firstSeen = null) => new()
    {
        Title = title,
        DueDate = dueDate,
        Author = author,
        Coleccion = coleccion,
        ImageUrl = imageUrl,
        FirstSeen = firstSeen ?? DateTime.UtcNow
    };

    [Test]
    public async Task Same_book_with_different_return_date_only_updates_due_date()
    {
        var originalDate = new DateTime(2026, 2, 12);
        var newDate = new DateTime(2026, 2, 26);
        var firstSeen = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: originalDate, author: "Cervantes",
                coleccion: "Clasicos", imageUrl: "covers/don-quijote.jpg", firstSeen: firstSeen)
        };

        var scraped = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: newDate, author: "Scraped Author",
                coleccion: "Scraped Col", imageUrl: "covers/scraped.jpg")
        };

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].DueDate).IsEqualTo(newDate);
        await Assert.That(result[0].Author).IsEqualTo("Cervantes");
        await Assert.That(result[0].Coleccion).IsEqualTo("Clasicos");
        await Assert.That(result[0].ImageUrl).IsEqualTo("covers/don-quijote.jpg");
        await Assert.That(result[0].FirstSeen).IsEqualTo(firstSeen);
    }

    [Test]
    public async Task Same_book_with_same_return_date_changes_nothing()
    {
        var dueDate = new DateTime(2026, 2, 12);
        var firstSeen = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: dueDate, author: "Cervantes",
                firstSeen: firstSeen)
        };

        var scraped = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: dueDate, author: "Other")
        };

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].DueDate).IsEqualTo(dueDate);
        await Assert.That(result[0].Author).IsEqualTo("Cervantes");
        await Assert.That(result[0].FirstSeen).IsEqualTo(firstSeen);
    }

    [Test]
    public async Task New_book_is_added_alongside_existing()
    {
        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Existing Book", dueDate: new DateTime(2026, 2, 12))
        };

        var scraped = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Brand New Book", dueDate: new DateTime(2026, 3, 1), author: "New Author")
        };

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result).HasCount().EqualTo(2);
        await Assert.That(result.Any(b => b.Title == "Existing Book")).IsTrue();
        await Assert.That(result.Any(b => b.Title == "Brand New Book")).IsTrue();
    }

    [Test]
    public async Task Title_matching_is_case_insensitive()
    {
        var originalDate = new DateTime(2026, 2, 12);
        var newDate = new DateTime(2026, 3, 5);

        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("La Ovejita Va Al Cole", dueDate: originalDate, author: "Smallman")
        };

        var scraped = new List<MadridLibraryScraper.Book>
        {
            MakeBook("la ovejita va al cole", dueDate: newDate, author: "Other")
        };

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].DueDate).IsEqualTo(newDate);
        await Assert.That(result[0].Author).IsEqualTo("Smallman");
    }

    [Test]
    public async Task Title_matching_trims_whitespace()
    {
        var originalDate = new DateTime(2026, 2, 12);
        var newDate = new DateTime(2026, 3, 5);

        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("  Don Quijote  ", dueDate: originalDate)
        };

        var scraped = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: newDate)
        };

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].DueDate).IsEqualTo(newDate);
    }

    [Test]
    public async Task Duplicate_existing_entries_are_deduplicated()
    {
        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: new DateTime(2026, 2, 12), author: "First"),
            MakeBook("Don Quijote", dueDate: new DateTime(2026, 2, 12), author: "Duplicate")
        };

        var scraped = new List<MadridLibraryScraper.Book>();

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Author).IsEqualTo("First");
    }

    [Test]
    public async Task Books_not_in_scraped_list_are_preserved()
    {
        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Returned Book", dueDate: new DateTime(2026, 1, 15)),
            MakeBook("Still Borrowed", dueDate: new DateTime(2026, 2, 12))
        };

        var scraped = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Still Borrowed", dueDate: new DateTime(2026, 2, 26))
        };

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result).HasCount().EqualTo(2);
        await Assert.That(result.Any(b => b.Title == "Returned Book")).IsTrue();
        await Assert.That(result.First(b => b.Title == "Still Borrowed").DueDate)
            .IsEqualTo(new DateTime(2026, 2, 26));
    }

    [Test]
    public async Task Null_coleccion_is_filled_from_scraped_data()
    {
        var dueDate = new DateTime(2026, 2, 12);
        var firstSeen = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: dueDate, author: null,
                coleccion: null, imageUrl: null, firstSeen: firstSeen)
        };

        var scraped = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: dueDate, author: "Cervantes",
                coleccion: "Clasicos ; 1", imageUrl: "covers/don-quijote.jpg")
        };

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Coleccion).IsEqualTo("Clasicos ; 1");
        await Assert.That(result[0].Author).IsEqualTo("Cervantes");
        await Assert.That(result[0].ImageUrl).IsEqualTo("covers/don-quijote.jpg");
        await Assert.That(result[0].FirstSeen).IsEqualTo(firstSeen);
    }

    [Test]
    public async Task Non_null_coleccion_is_not_overwritten_by_scraped_data()
    {
        var dueDate = new DateTime(2026, 2, 12);
        var firstSeen = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: dueDate, author: "Cervantes",
                coleccion: "Clasicos", imageUrl: "covers/don-quijote.jpg", firstSeen: firstSeen)
        };

        var scraped = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Don Quijote", dueDate: dueDate, author: "Other Author",
                coleccion: "Different Collection", imageUrl: "covers/other.jpg")
        };

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Coleccion).IsEqualTo("Clasicos");
        await Assert.That(result[0].Author).IsEqualTo("Cervantes");
        await Assert.That(result[0].ImageUrl).IsEqualTo("covers/don-quijote.jpg");
    }

    [Test]
    public async Task Result_is_ordered_by_first_seen()
    {
        var older = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var existing = new List<MadridLibraryScraper.Book>
        {
            MakeBook("Second Book", firstSeen: newer),
            MakeBook("First Book", firstSeen: older)
        };

        var scraped = new List<MadridLibraryScraper.Book>();

        var result = MadridLibraryScraper.MergeBooks(existing, scraped);

        await Assert.That(result[0].Title).IsEqualTo("First Book");
        await Assert.That(result[1].Title).IsEqualTo("Second Book");
    }
}
