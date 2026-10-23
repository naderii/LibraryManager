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
            .ToList();
    }

    public List<Loan> Late()
    {
        using var db = new LibraryDbContext();
        return db.Loans
            .Include(loan => loan.Book)
            .Include(loan => loan.Member)
            .Where(loan => !loan.IsReturned && loan.DueDate < DateTime.Now)
            .ToList();
    }

    public List<Member> Members()
    {
        using var db = new LibraryDbContext();
        return db.Members.AsNoTracking().ToList();
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
