namespace BankingSystemCore.Mailing;

/// <summary>Bound from the "Mailing" configuration section.</summary>
public class MailingOptions
{
    public const string SectionName = "Mailing";

    /// <summary>
    /// Development only: write the whole e-mail (including links and tokens) to the log, so flows that need an
    /// e-mail can be tried without a mail server. Never enable it where real users are.
    /// </summary>
    public bool LogBody { get; set; }
}
