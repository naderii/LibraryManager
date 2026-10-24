using System.Collections;
using LibraryManager.Data;
using LibraryManager.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManager.Services;

public class ReportService
{
    public List<Book> Books()
    {
        using var db = new LibraryDbContext();
        return db.Books.AsNoTracking().OrderBy(book => book.Title).ToList();
    }

    public List<Loan> Current()
    {
        using var db = new LibraryDbContext();
        return db.Loans
            .Include(loan => loan.Book)
            .Include(loan => loan.Member)
            .Where(loan => !loan.IsReturned)
            .OrderBy(loan => loan.DueDate)
            .ToList();
    }

    public List<Loan> Late()
    {
        using var db = new LibraryDbContext();
        return db.Loans
            .Include(loan => loan.Book)
            .Include(loan => loan.Member)
            .Where(loan => !loan.IsReturned && loan.DueDate < DateTime.Now)
            .OrderBy(loan => loan.DueDate)
            .ToList();
    }

    public List<Member> Members()
    {
        using var db = new LibraryDbContext();
        return db.Members.AsNoTracking().ToList();
    }

    /// <summary>
    /// کتاب‌هایی که الان امانت هستند به همراه عضو و تاریخ‌ها
    /// </summary>
    public List<ActiveLoanRow> ActiveLoansDetailed()
    {
        using var db = new LibraryDbContext();
        var loans = db.Loans
            .Include(l => l.Book)
            .Include(l => l.Member)
            .Where(l => !l.IsReturned)
            .OrderBy(l => l.DueDate)
            .AsNoTracking()
            .ToList();

        var finePerDay = SettingsService.FinePerDay;
        var today = DateTime.Now.Date;

        return loans.Select(l =>
        {
            var days = l.DueDate.Date < today
                ? (int)(today - l.DueDate.Date).TotalDays
                : 0;
            return new ActiveLoanRow
            {
                LoanId = l.Id,
                BookTitle = l.Book?.Title ?? "",
                BookAuthor = l.Book?.Author ?? "",
                BookIsbn = l.Book?.ISBN ?? "",
                MemberName = ((l.Member?.FirstName ?? "") + " " + (l.Member?.LastName ?? "")).Trim(),
                MemberPhone = l.Member?.PhoneNumber ?? "",
                LoanDate = l.LoanDate,
                DueDate = l.DueDate,
                DaysOverdue = days,
                EstimatedFine = days * finePerDay
            };
        }).ToList();
    }

    /// <summary>
    /// برای هر عضو، لیست کتاب‌های امانت‌گرفته‌اش
    /// </summary>
    public List<MemberBooksRow> MembersWithBooks()
    {
        using var db = new LibraryDbContext();
        var active = db.Loans
            .Include(l => l.Book)
            .Include(l => l.Member)
            .Where(l => !l.IsReturned)
            .ToList();

        return active
            .GroupBy(l => l.MemberId)
            .Select(g =>
            {
                var first = g.First();
                return new MemberBooksRow
                {
                    MemberId = first.MemberId,
                    MemberName = first.Member.FirstName + " " + first.Member.LastName,
                    MemberPhone = first.Member.PhoneNumber,
                    BookCount = g.Count(),
                    Books = string.Join(" | ", g.Select(x => x.Book.Title)),
                    EarliestDue = g.Min(x => x.DueDate)
                };
            })
            .OrderByDescending(x => x.BookCount)
            .ToList();
    }

    /// <summary>
    /// خلاصه موجودی کتاب‌ها
    /// </summary>
    public InventorySummary Inventory()
    {
        using var db = new LibraryDbContext();
        var books = db.Books.AsNoTracking().ToList();
        var activeLoans = db.Loans.Count(l => !l.IsReturned);

        return new InventorySummary
        {
            TotalTitles = books.Count,
            TotalCopies = books.Sum(b => b.TotalCopies),
            AvailableCopies = books.Sum(b => b.AvailableCopies),
            OnLoanCopies = activeLoans,
            ZeroAvailable = books.Count(b => b.AvailableCopies <= 0)
        };
    }

    public IEnumerable Popular()
    {
        using var db = new LibraryDbContext();
        return (from loan in db.Loans.Include(loan => loan.Book).AsNoTracking()
                group loan by loan.Book.Title into groupLoans
                orderby groupLoans.Count() descending
                select new { کتاب = groupLoans.Key, تعداد = groupLoans.Count() })
            .Take(20)
            .ToList();
    }

    public (decimal Cash, decimal Card) Finance(DateTime from, DateTime to)
    {
        using var db = new LibraryDbContext();

        var membershipPayments = db.MembershipPayments
            .Where(payment => payment.PaymentDate >= from && payment.PaymentDate <= to);

        var finePayments = db.Loans
            .Where(loan => loan.FinePaid && loan.FineAmount > 0 &&
                           loan.ReturnDate >= from && loan.ReturnDate <= to);

        var cash =
            (membershipPayments.Where(payment => payment.PaymentMethod == PaymentMethod.Cash)
                .Sum(payment => (decimal?)payment.Amount) ?? 0m) +
            (finePayments.Where(loan => loan.FinePaymentMethod == PaymentMethod.Cash)
                .Sum(loan => (decimal?)loan.FineAmount) ?? 0m);

        var card =
            (membershipPayments.Where(payment => payment.PaymentMethod == PaymentMethod.Card)
                .Sum(payment => (decimal?)payment.Amount) ?? 0m) +
            (finePayments.Where(loan => loan.FinePaymentMethod == PaymentMethod.Card)
                .Sum(loan => (decimal?)loan.FineAmount) ?? 0m);

        return (cash, card);
    }
}

public class ActiveLoanRow
{
    public int LoanId { get; set; }
    public string BookTitle { get; set; } = "";
    public string BookAuthor { get; set; } = "";
    public string BookIsbn { get; set; } = "";
    public string MemberName { get; set; } = "";
    public string MemberPhone { get; set; } = "";
    public DateTime LoanDate { get; set; }
    public DateTime DueDate { get; set; }
    public int DaysOverdue { get; set; }
    public decimal EstimatedFine { get; set; }
}

public class MemberBooksRow
{
    public int MemberId { get; set; }
    public string MemberName { get; set; } = "";
    public string MemberPhone { get; set; } = "";
    public int BookCount { get; set; }
    public string Books { get; set; } = "";
    public DateTime EarliestDue { get; set; }
}

public class InventorySummary
{
    public int TotalTitles { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int OnLoanCopies { get; set; }
    public int ZeroAvailable { get; set; }
}
