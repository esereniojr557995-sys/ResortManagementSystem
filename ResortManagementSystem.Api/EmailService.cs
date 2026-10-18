using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ResortManagementSystem.Domain.Entities;
using ResortManagementSystem.Infrastructure.Services;

// ------------------------------------------------------------------
// Settings come from configuration section "Email".
// Real passwords go in User Secrets, never in appsettings.json or GitHub:
//   dotnet user-secrets set "Email:Password" "<app password>"
// ------------------------------------------------------------------
public class EmailSettings
{
    public string Host { get; set; } = "";               // e.g. smtp.gmail.com. Leave empty to save .eml files instead of sending
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";        // for Gmail this must be the same address as Username
    public string FromName { get; set; } = "Resort CRM";
    public string PickupDirectory { get; set; } = "";    // used only when Host is empty
}

public interface IPromoMailer
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string fromName, CancellationToken ct = default);
}

public class SmtpPromoMailer : IPromoMailer
{
    private readonly EmailSettings _s;
    public SmtpPromoMailer(IOptions<EmailSettings> options) => _s = options.Value;

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string fromName, CancellationToken ct = default)
    {
        using var client = new SmtpClient();

        if (!string.IsNullOrWhiteSpace(_s.Host))
        {
            client.Host = _s.Host;
            client.Port = _s.Port;
            client.EnableSsl = _s.EnableSsl;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.Timeout = 20000;
            if (!string.IsNullOrWhiteSpace(_s.Username))
                client.Credentials = new NetworkCredential(_s.Username, _s.Password);
        }
        else
        {
            // Test mode: no mail server. Each email is written as an .eml file you can double-click to read.
            var dir = string.IsNullOrWhiteSpace(_s.PickupDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "outbox")
                : _s.PickupDirectory;
            Directory.CreateDirectory(dir);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = dir;
        }

        var fromAddress = !string.IsNullOrWhiteSpace(_s.FromAddress) ? _s.FromAddress
                        : !string.IsNullOrWhiteSpace(_s.Username) ? _s.Username
                        : "no-reply@resortcrm.local";

        using var msg = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(new MailAddress(toEmail, toName));

        await client.SendMailAsync(msg, ct);
    }
}

// Sends everything that is Pending in a resort's outbox, in the background, one resort at a time
public class EmailDispatcher
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IPromoMailer _mailer;
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _gates = new();

    public EmailDispatcher(IServiceScopeFactory scopes, IPromoMailer mailer)
    {
        _scopes = scopes;
        _mailer = mailer;
    }

    public void Kick(int companyId, string companyName) => _ = Task.Run(() => RunAsync(companyId, companyName));

    private async Task RunAsync(int companyId, string companyName)
    {
        var gate = _gates.GetOrAdd(companyId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            using var scope = _scopes.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();

            while (true)
            {
                await using var db = await factory.CreateAsync(companyId);
                var batch = await db.EmailOutbox.Where(m => m.Status == "Pending").OrderBy(m => m.EmailMessageId).Take(25).ToListAsync();
                if (batch.Count == 0) break;

                foreach (var m in batch)
                {
                    m.Attempts++;
                    try
                    {
                        await _mailer.SendAsync(m.ToEmail, m.ToName, m.Subject, m.Body, companyName);
                        m.Status = "Sent";
                        m.SentAt = DateTime.UtcNow;
                        m.LastError = null;
                    }
                    catch (Exception ex)
                    {
                        m.Status = "Failed";
                        var text = ex.InnerException?.Message ?? ex.Message;
                        m.LastError = text.Length > 480 ? text[..480] : text;
                    }
                }
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email dispatcher] company {companyId}: {ex.Message}");
        }
        finally
        {
            gate.Release();
        }
    }
}

public static class PromoEmailTemplate
{
    public static string Build(string company, string customerName, Promotion promo, string? extraMessage)
    {
        string E(string s) => WebUtility.HtmlEncode(s ?? "");
        var first = (customerName ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Guest";
        var who = promo.EligibleCategory is "All" or "" ? "all of our guests" : $"our {promo.EligibleCategory} guests";
        var extra = string.IsNullOrWhiteSpace(extraMessage) ? "" : $"<p style=\"margin:0 0 18px;color:#17323D;font-size:15px;line-height:1.5\">{E(extraMessage).Replace("\n", "<br>")}</p>";

        return $@"<!doctype html><html><body style=""margin:0;background:#F7F3EA;font-family:Segoe UI,Arial,sans-serif"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:24px"">
<table width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:12px;overflow:hidden"">
<tr><td style=""background:#0F8B8D;padding:26px 32px;color:#ffffff;font-size:22px;font-weight:bold"">{E(company)}</td></tr>
<tr><td style=""padding:32px"">
<p style=""margin:0 0 12px;font-size:18px;color:#17323D"">Hi {E(first)},</p>
<p style=""margin:0 0 18px;color:#17323D;font-size:15px;line-height:1.5"">We have a special offer for {E(who)}:</p>
{extra}
<table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""background:#F7F3EA;border:2px dashed #FF7A59;border-radius:10px;padding:22px"">
<div style=""font-size:34px;font-weight:bold;color:#17323D"">{promo.DiscountPercent:0.##}% OFF</div>
<div style=""margin-top:6px;color:#6B7B80;font-size:14px"">{E(promo.Description)}</div>
<div style=""margin-top:14px;font-size:13px;color:#6B7B80"">Use promo code</div>
<div style=""font-size:26px;letter-spacing:3px;font-weight:bold;color:#FF7A59"">{E(promo.Code)}</div>
</td></tr></table>
<p style=""margin:22px 0 0;color:#6B7B80;font-size:13px"">Mention this code when you book. We look forward to welcoming you.</p>
</td></tr>
<tr><td style=""padding:16px 32px;background:#F7F3EA;color:#6B7B80;font-size:12px"">You are receiving this because you are a guest of {E(company)}. Reply to this email if you would rather not receive promotions.</td></tr>
</table></td></tr></table></body></html>";
    }
}

public record NotifyRequest(string? Subject, string? Message, bool PreviewOnly);
public record NotifyResult(int Eligible, int AlreadySent, int ToSend);
public record EmailLogRow(int EmailMessageId, int? PromotionId, string ToEmail, string ToName, string Subject,
    string Status, int Attempts, string? LastError, DateTime CreatedAt, DateTime? SentAt);