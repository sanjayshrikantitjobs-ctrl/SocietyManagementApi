using System.Net;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagement.Application.Features.Visitors;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.API.Controllers;

/// <summary>
/// The page behind the "Approve or reject here" link sent by WhatsApp when a
/// watchman creates a visitor request (see VisitorVisitFeature.SendWhatsAppApprovalRequestAsync).
/// [AllowAnonymous] and keyed entirely by VisitorVisit.ApprovalToken (an
/// unguessable GUID) rather than a signed-in user: a flat's owner/tenant may
/// have no login/user account on this system at all, so this can't require one.
///
/// Plain server-rendered HTML with real &lt;form&gt; POSTs, not a JSON API +
/// SPA route — this needs to work when opened straight from WhatsApp on any
/// phone's default browser, with no app install and no JS framework to load.
/// </summary>
[ApiController]
[Route("api/public/visitor-approvals")]
[AllowAnonymous]
public class VisitorApprovalPublicController : ControllerBase
{
    private readonly ISender _mediator;

    public VisitorApprovalPublicController(ISender mediator) => _mediator = mediator;

    [HttpGet("{token}")]
    public async Task<ContentResult> Get(string token, [FromQuery] string? justActioned = null)
    {
        VisitorVisitDto visit;
        try
        {
            visit = await _mediator.Send(new GetVisitByApprovalTokenQuery(token));
        }
        catch (NotFoundException)
        {
            return Page("Link not found", "<p>This approval link is invalid. Please contact your society office if you believe this is a mistake.</p>");
        }

        return Page("Visitor Approval Request", RenderBody(visit, token, justActioned));
    }

    [HttpPost("{token}/approve")]
    public async Task<IActionResult> Approve(string token)
    {
        try
        {
            await _mediator.Send(new ApproveVisitByTokenCommand(token));
            return RedirectToAction(nameof(Get), new { token, justActioned = "approved" });
        }
        catch (NotFoundException)
        {
            return RedirectToAction(nameof(Get), new { token });
        }
        catch (ConflictAppException)
        {
            return RedirectToAction(nameof(Get), new { token });
        }
    }

    [HttpPost("{token}/reject")]
    public async Task<IActionResult> Reject(string token, [FromForm] string? reason)
    {
        try
        {
            await _mediator.Send(new RejectVisitByTokenCommand(token, reason));
            return RedirectToAction(nameof(Get), new { token, justActioned = "rejected" });
        }
        catch (NotFoundException)
        {
            return RedirectToAction(nameof(Get), new { token });
        }
        catch (ConflictAppException)
        {
            return RedirectToAction(nameof(Get), new { token });
        }
    }

    /// <summary>One-tap counterparts of Approve/Reject above, at "verb-first"
    /// routes (action/{token} rather than {token}/action) — required because
    /// these are the actual URLs behind the WhatsApp template's "Approve"/
    /// "Reject" buttons, and a WhatsApp URL button only ever issues a GET
    /// with its dynamic {{1}} value appended as the LAST path segment; Meta
    /// doesn't support appending anything after it, so the verb can't come
    /// after the token the way the form-based routes above do. No reason
    /// field here (a WhatsApp button can't collect text) — a rejecter who
    /// wants to give a reason can still open the plain link instead.</summary>
    [HttpGet("approve/{token}")]
    public async Task<IActionResult> ApproveViaButton(string token)
    {
        try
        {
            await _mediator.Send(new ApproveVisitByTokenCommand(token));
        }
        catch (NotFoundException) { }
        catch (ConflictAppException) { }

        return RedirectToAction(nameof(Get), new { token, justActioned = "approved" });
    }

    [HttpGet("reject/{token}")]
    public async Task<IActionResult> RejectViaButton(string token)
    {
        try
        {
            await _mediator.Send(new RejectVisitByTokenCommand(token, Reason: null));
        }
        catch (NotFoundException) { }
        catch (ConflictAppException) { }

        return RedirectToAction(nameof(Get), new { token, justActioned = "rejected" });
    }

    private static string RenderBody(VisitorVisitDto visit, string token, string? justActioned)
    {
        var photoHtml = string.IsNullOrWhiteSpace(visit.VisitorPhotoUrl)
            ? ""
            : $"""<img src="{Enc(visit.VisitorPhotoUrl)}" alt="Visitor photo" class="photo" />""";

        var banner = justActioned switch
        {
            "approved" => """<div class="banner success">Visitor approved.</div>""",
            "rejected" => """<div class="banner danger">Visitor rejected.</div>""",
            _ => ""
        };

        var details = $"""
            <dl>
              <dt>Visitor</dt><dd>{Enc(visit.VisitorName)}</dd>
              <dt>Mobile</dt><dd>{Enc(visit.VisitorMobile)}</dd>
              <dt>Purpose</dt><dd>{Enc(visit.PurposeName)}</dd>
              <dt>Flat</dt><dd>{Enc(visit.FlatNumber)}</dd>
              <dt>Gate</dt><dd>{Enc(visit.GateName)}</dd>
              <dt>Visitors</dt><dd>{visit.NumberOfVisitors}</dd>
              <dt>Requested</dt><dd>{visit.RequestedAt:dd MMM yyyy, hh:mm tt}</dd>
            </dl>
            """;

        var actionHtml = visit.Status == VisitorVisitStatus.PendingApproval
            ? $"""
                <div class="actions">
                  <form method="post" action="/api/public/visitor-approvals/{Enc(token)}/approve">
                    <button type="submit" class="approve">Approve</button>
                  </form>
                  <form method="post" action="/api/public/visitor-approvals/{Enc(token)}/reject">
                    <input type="text" name="reason" placeholder="Reason (optional)" />
                    <button type="submit" class="reject">Reject</button>
                  </form>
                </div>
                """
            : $"""<div class="banner status">Current status: <strong>{Enc(StatusLabel(visit.Status))}</strong></div>""";

        return photoHtml + banner + details + actionHtml;
    }

    private static string StatusLabel(VisitorVisitStatus status) => status switch
    {
        VisitorVisitStatus.Approved => "Approved",
        VisitorVisitStatus.Rejected => "Rejected",
        VisitorVisitStatus.Expired => "Expired — no longer actionable",
        VisitorVisitStatus.Cancelled => "Cancelled by the gate",
        VisitorVisitStatus.CheckedIn => "Approved — visitor has checked in",
        VisitorVisitStatus.CheckedOut => "Approved — visitor has checked out",
        _ => status.ToString()
    };

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static ContentResult Page(string title, string bodyHtml)
    {
        var html = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <title>{{Enc(title)}}</title>
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <style>
            body { font-family: system-ui, sans-serif; max-width: 480px; margin: 24px auto; padding: 0 16px; line-height: 1.5; color: #1a1a1a; }
            h1 { font-size: 1.25rem; }
            .photo { width: 100%; max-height: 280px; object-fit: cover; border-radius: 8px; margin-bottom: 16px; }
            dl { display: grid; grid-template-columns: auto 1fr; gap: 4px 12px; margin: 16px 0; }
            dt { font-weight: 600; color: #555; }
            dd { margin: 0; }
            .banner { padding: 12px; border-radius: 8px; margin-bottom: 16px; font-weight: 600; }
            .banner.success { background: #e6f4ea; color: #1e7d32; }
            .banner.danger { background: #fdecea; color: #b03a2e; }
            .banner.status { background: #eef2f7; color: #333; }
            .actions { display: flex; flex-direction: column; gap: 12px; margin-top: 20px; }
            .actions form { display: flex; gap: 8px; }
            .actions input[type=text] { flex: 1; padding: 10px; border: 1px solid #ccc; border-radius: 6px; }
            button { padding: 12px 20px; border: none; border-radius: 6px; font-size: 1rem; font-weight: 600; cursor: pointer; color: #fff; width: 100%; }
            button.approve { background: #1e7d32; }
            button.reject { background: #b03a2e; }
          </style>
        </head>
        <body>
          <h1>Visitor Approval Request</h1>
          {{bodyHtml}}
        </body>
        </html>
        """;

        return new ContentResult { Content = html, ContentType = "text/html" };
    }
}
