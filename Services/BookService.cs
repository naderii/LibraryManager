using LibraryManager.Data;
using LibraryManager.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManager.Services;

public class BookService
{
    public List<Book> All() =>
        new LibraryDbContext().Books.AsNoTracking().OrderBy(x => x.Title).ToList();

    public Book? FindByIsbn(string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return null;
        var normalized = IsbnLookupService.NormalizeIsbn(isbn);
        if (normalized.Length is not (10 or 13)) return null;

        using var d = new LibraryDbContext();
        var candidates = d.Books.AsNoTracking()
            .Where(b => b.ISBN != null && b.ISBN != "")
            .ToList();
        return candidates.FirstOrDefault(b =>
            IsbnLookupService.NormalizeIsbn(b.ISBN) == normalized);
    }

    public bool IsbnExists(string isbn, int excludeId = 0)
    {
        var found = FindByIsbn(isbn);
        return found != null && found.Id != excludeId;
    }

    public Book Save(Book b)
    {
        // جلوگیری از ثبت شابک تکراری
        if (!string.IsNullOrWhiteSpace(b.ISBN) && IsbnExists(b.ISBN, b.Id))
        {
            var existing = FindByIsbn(b.ISBN)!;
            throw new InvalidOperationException(
                $"این شابک قبلاً ثبت شده است.\n\nعنوان: {existing.Title}\nنویسنده: {existing.Author}\nشناسه: {existing.Id}");
        }

        using var d = new LibraryDbContext();
        if (b.Id == 0)
        {
            if (b.TotalCopies <= 0) b.TotalCopies = 1;
            if (b.AvailableCopies <= 0) b.AvailableCopies = b.TotalCopies;
            d.Books.Add(b);
        }
        else
        {
            d.Books.Update(b);
        }
        d.SaveChanges();
        return b;
    }

    public void Delete(int id)
    {
        using var d = new LibraryDbContext();
        var b = d.Books.Find(id);
        if (b == null) return;
        if (d.Loans.Any(x => x.BookId == id))
            throw new InvalidOperationException("برای این کتاب سابقه امانت وجود دارد.");
        d.Remove(b);
        d.SaveChanges();
    }
}
