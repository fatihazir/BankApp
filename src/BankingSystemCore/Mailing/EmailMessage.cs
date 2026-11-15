namespace BankingSystemCore.Mailing;

public sealed record EmailMessage(string To, string Subject, string Body, bool IsHtml = false);
