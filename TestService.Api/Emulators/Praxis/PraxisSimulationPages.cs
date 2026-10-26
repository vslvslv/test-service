using System.Net;
using System.Text;

namespace TestService.Api.Emulators.Praxis;

/// <summary>
/// The emulator's simulation pages. The 3DS confirmation page: the same form as the Praxis sandbox (Decline with an
/// <c>error_code</c> select, Pending, Approve, Verification). Each button POSTs <c>error_code</c>
/// to <c>.../pages/3ds/{tid}/submit?status=approved|declined|pending|verification</c>.
/// The e-wallet pages: the "Please wait" redirect page, the five-button APM page and the payout page.
/// </summary>
public static class PraxisSimulationPages
{
    public static string ThreeDsPage(string submitUrl)
    {
        // A JavaScript string literal (JSON-encoded, so '&', '<', quotes are escaped for a <script> block), not HTML encoding.
        var action = System.Text.Json.JsonSerializer.Serialize(submitUrl);
        var options = new StringBuilder();
        foreach (var (code, label) in PraxisTestCases.ThreeDsDeclineCodes)
        {
            options.Append($"<option value=\"{code}\">{WebUtility.HtmlEncode(label)}</option>");
        }

        return $$"""
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <title>3DSecure Confirmation Page</title>
            <script>
                const submit3D = (status) => {
                    const form3ds = document.getElementById('sandbox_3ds_form');
                    form3ds.setAttribute('action', {{action}} + '?status=' + status);
                    form3ds.submit();
                };
            </script>
            </head>
            <body class="body">
            <div class="container">
                <h1>3DSecure Confirmation Page</h1>
                <h3>Please select transaction status:</h3>
                <div class="form">
                    <form class="action declined-case" id="sandbox_3ds_form" name="sandbox_3ds_form" method="POST" action="#">
                        <select id="error_code" name="error_code" class="select">{{options}}</select>
                        <button type="button" onclick="submit3D('declined')" class="button declined normal">Decline</button>
                    </form>
                    <div class="action">
                        <button type="button" onclick="submit3D('pending')" class="button pending normal">Pending</button>
                        <button type="button" onclick="submit3D('approved')" class="button approve normal">Approve</button>
                        <button type="button" onclick="submit3D('verification')" class="button approve normal">Verification</button>
                    </div>
                </div>
            </div>
            </body>
            </html>
            """;
    }

    /// <summary>Shown after a submit when the transaction has no <c>return_url</c>.</summary>
    public static string Done(string status) =>
        $"<!doctype html><html><head><meta charset=\"utf-8\"><title>3DSecure</title></head><body><p>3DS result submitted: {WebUtility.HtmlEncode(status)}</p></body></html>";

    /// <summary>The e-wallet "Please wait" page: its script loads <paramref name="nextUrl"/> (the APM page).</summary>
    public static string WaitPage(string nextUrl)
    {
        var target = System.Text.Json.JsonSerializer.Serialize(nextUrl);
        return $$"""
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <title>Please wait...</title>
            <script>
                function OnLoadEvent() { window.location.href = {{target}}; }
            </script>
            </head>
            <body onload="OnLoadEvent();">
            <div class="loading-wrapper"><span class="loading-text">Please wait...</span></div>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// The e-wallet APM page: one button per status; each loads <c>{responseUrl}?status=&lt;status&gt;</c>,
    /// as the sandbox page does.
    /// </summary>
    public static string ApmPage(string responseUrl)
    {
        var url = System.Text.Json.JsonSerializer.Serialize(responseUrl);
        var buttons = new StringBuilder();
        foreach (var (status, label) in PraxisEWalletCases.Buttons)
        {
            buttons.Append($"<button type=\"submit\" onclick=\"setStatus('{status}')\" class=\"button {status}\">{label}</button>");
        }

        return $$"""
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <title>Test E-Wallet</title>
            <script>
                function setStatus(status) {
                    const buttons = document.querySelectorAll('.button');
                    for (let i = 0; i < buttons.length; i++) {
                        buttons[i].setAttribute('disabled', 'disabled');
                    }
                    var url = {{url}};
                    window.location.href = url + "?status=" + status;
                }
            </script>
            </head>
            <body>
            <div class="text-center">
                <h3>Please click the button to continue</h3>
                <div>{{buttons}}</div>
            </div>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// The e-wallet payout page (payout 300): a link back to the merchant; it changes nothing. Only an absolute
    /// <c>http</c>/<c>https</c> <c>return_url</c> becomes the link (no <c>javascript:</c> on this origin); otherwise <c>#</c>.
    /// </summary>
    public static string PayoutPage(string? returnUrl)
    {
        var safe = Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        var href = WebUtility.HtmlEncode(safe ? returnUrl! : "#");
        return $$"""
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <title>3DSecure Confirmation Page</title>
            </head>
            <body>
            <div class="container">
                <h1>Test E-Wallet 3DS page</h1>
                <h3><a class="btn btn-success submit-btn" href="{{href}}">Back to merchant website</a></h3>
            </div>
            </body>
            </html>
            """;
    }

    /// <summary>Shown after an e-wallet page outcome when the transaction has no <c>return_url</c>.</summary>
    public static string ApmDone(string status) =>
        $"<!doctype html><html><head><meta charset=\"utf-8\"><title>Test E-Wallet</title></head><body><p>E-wallet result: {WebUtility.HtmlEncode(status)}</p></body></html>";
}
