using LibraryManager.Data;
using LibraryManager.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManager.Services;

public class LoanService
{
    public string CanBorrow(int memberId)
    {
        using var d = new LibraryDbContext();
        var m = d.Members.Find(memberId);
        if (m == null) return "عضو انتخاب نشده است.";
        if (m.MembershipExpiryDate < DateTime.Now) return "عضویت عضو منقضی است.";
        if (d.Loans.Count(x => x.MemberId == memberId && !x.IsReturned) >= SettingsService.MaxLoans)
            return "تعداد امانت‌های فعال عضو به سقف مجاز رسیده است.";
        if (d.Loans.Any(x => x.MemberId == memberId && x.IsReturned && !x.FinePaid))
            return "عضو جریمه پرداخت‌نشده دارد.";
        return "";
    }

    public Loan Register(int memberId, int bookId)
    {
        var why = CanBorrow(memberId);
        if (why != "") throw new InvalidOperationException(why);
        using var d = new LibraryDbContext();
        var b = d.Books.Find(bookId) ?? throw new Exception("کتاب پیدا نشد");
        if (b.AvailableCopies <= 0) throw new Exception("نسخه‌ای از کتاب موجود نیست.");
        var now = DateTime.Now;
        b.AvailableCopies--;
        var l = new Loan
        {
            MemberId = memberId,
            BookId = bookId,
            LoanDate = now,
            DueDate = now.AddDays(SettingsService.LoanDays),
            FinePaid = true
        };
        d.Loans.Add(l);
        d.SaveChanges();
        return l;
    }

    public List<Loan> Active() =>
        new LibraryDbContext().Loans
            .Include(x => x.Book)
            .Include(x => x.Member)
            .Where(x => !x.IsReturned)
            .ToList();

    public List<Loan> Unpaid() =>
        new LibraryDbContext().Loans
            .Include(x => x.Book)
            .Include(x => x.Member)
            .Where(x => x.IsReturned && !x.FinePaid)
            .ToList();

    public decimal CalculateFine(Loan l, DateTime returned) =>
        Math.Max(0, (decimal)(returned.Date - l.DueDate.Date).Days * SettingsService.FinePerDay);

    public void Return(int id, PaymentMethod? method, bool payNow)
    {
        using var d = new LibraryDbContext();
        var l = d.Loans.Include(x => x.Book).First(x => x.Id == id);
        if (l.IsReturned) throw new Exception("این امانت قبلاً بازگردانده شده است.");
        l.ReturnDate = DateTime.Now;
        l.IsReturned = true;
        l.Book.AvailableCopies++;
        l.FineAmount = CalculateFine(l, l.ReturnDate.Value);
        if (l.FineAmount > 0)
        {
            if (payNow)
            {
                if (method == null) throw new Exception("روش پرداخت مشخص نشده است.");
                l.FinePaid = true;
                l.FinePaymentMethod = method;
            }
            else
            {
                l.FinePaid = false;
            }
        }
        else
        {
            l.FinePaid = true;
        }
        d.SaveChanges();
    }
}
