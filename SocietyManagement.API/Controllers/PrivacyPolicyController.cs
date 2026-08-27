using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SocietyManagement.API.Controllers;

/// <summary>
/// A minimal, publicly reachable privacy policy page — required by Meta
/// before the WhatsApp Business app can be moved out of "In development"
/// mode (see App Settings -> Basic -> Privacy Policy URL). Served as plain
/// HTML content directly from a controller action rather than a static file,
/// so it's versioned with the rest of the app and needs no separate deploy
/// step to keep in sync.
/// </summary>
[ApiController]
[Route("privacy-policy")]
[AllowAnonymous]
public class PrivacyPolicyController : ControllerBase
{
    [HttpGet]
    public ContentResult Get()
    {
        const string html = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <title>Privacy Policy — Society Management System</title>
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <style>
            body { font-family: system-ui, sans-serif; max-width: 720px; margin: 40px auto; padding: 0 16px; line-height: 1.6; color: #1a1a1a; }
            h1 { font-size: 1.5rem; }
            h2 { font-size: 1.15rem; margin-top: 2rem; }
            footer { margin-top: 3rem; font-size: 0.85rem; color: #666; }
          </style>
        </head>
        <body>
          <h1>Privacy Policy</h1>
          <p>This Privacy Policy explains how the Society Management System ("the App"), operated by Shrios Software Technologies, collects, uses, and protects information for the residential societies that use it.</p>

          <h2>Information We Collect</h2>
          <p>To operate the App, we collect information provided by society administrators and residents, including: names, contact details (phone number, email), flat/unit and building information, vehicle registration numbers, payment and maintenance/festival contribution records, and photos submitted for visitor or vehicle records.</p>

          <h2>How We Use Information</h2>
          <p>Information is used solely to operate society management functions: billing and payment receipts, visitor and vehicle gate records, and resident communication. This includes sending payment receipts and notifications via WhatsApp to the phone number on file for a resident or flat.</p>

          <h2>Sharing of Information</h2>
          <p>We do not sell resident data. Information is shared only with the service providers necessary to operate the App — for example, Meta/WhatsApp Business Platform for message delivery, and our cloud hosting provider (Microsoft Azure) for data storage — solely to provide the described functionality.</p>

          <h2>Data Retention and Deletion</h2>
          <p>Data is retained for as long as a resident or vehicle record remains active with the society, or as required for financial record-keeping. To request correction or deletion of your data, contact your society's administrator, or reach us directly using the contact details below.</p>

          <h2>Contact</h2>
          <p>For questions about this policy or your data, contact: <a href="mailto:shriossoftware@gmail.com">shriossoftware@gmail.com</a></p>

          <footer>Last updated: 2026</footer>
        </body>
        </html>
        """;

        return Content(html, "text/html");
    }
}
