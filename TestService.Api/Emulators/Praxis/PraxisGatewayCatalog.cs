using System.Text.Json.Nodes;

namespace TestService.Api.Emulators.Praxis;

/// <summary>
/// The gateway entries of <c>agent/get-available-gateways</c> and <c>agent/get-gateway-options</c>, shaped
/// as the sandbox captures (keys as BillingServer's <c>ResponseModels/GetAvailableGateways/*</c> and
/// <c>PraxisGetGatewayOptionsResponse</c> read them). A <c>card</c> gateway uses the captured
/// Test Card Processor entry, an <c>ewallet</c> gateway the captured Test E-Wallet entry; ids, method,
/// processor and system name come from the registered gateway and merchant.
/// </summary>
public static class PraxisGatewayCatalog
{
    public const string DefaultCardProcessor = "Test Card Processor";
    public const string DefaultCardMethod = "Credit Card";

    /// <summary><c>countries_supported</c> of both captured gateways (alpha-2 and the sandbox's extra codes).</summary>
    public static readonly IReadOnlyList<string> Countries = (
        "AB AC AD AE AF AG AI AL AM AN AO AQ AR AS AT AU AX AW AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ " +
        "CA CC CD CF CG CH CI CK CL CM CN CO CR CS CT CU CV CW CX CY CZ KM HR DE DG DJ DK DM DO DZ EC EE EH EG ER ES ET FI FJ FK " +
        "FM FO FQ FR FX GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HM HN HK HT HU ID IE IM IN IL IO IQ IR IS IT JE " +
        "JM JO JP JT KE KH KI KG KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MH MG MI MK ML MM MN MO MP MQ " +
        "MR MS MT MU MV MW MX MY MZ NA NC NE NF NG NI NL NO NP NQ NR NT NU NZ OM PA PC PE PF PG PH PK PL PM PN PR PS PT PU PW PY " +
        "PZ QA RE RO RS RU RW SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SU SV SX SY SZ TC TD TH TG TF TJ TK TL TM TN TO " +
        "TR TT TV TW TZ UA UG UM US UY UZ VA VC VD VG VI VE VN VU WF WK WS XK YD YE YT ZA ZM ZW ZZ").Split(' ');

    private static readonly string[] CardLimitedCurrencies = ["EUR", "USD", "GBP", "BRL", "MXN"];
    private static readonly string[] CardOpenCurrencies = ["JPY", "PLN", "VND"];
    private static readonly string[] WalletLimitedCurrencies = ["EUR"];
    private static readonly string[] WalletOpenCurrencies = ["PLN", "BRL", "USD", "JPY"];

    public static bool IsEWallet(PraxisGateway gateway) => gateway.Kind == "ewallet";

    public static string Processor(PraxisGateway gateway) =>
        NullIfEmpty(gateway.PaymentProcessor) ?? (IsEWallet(gateway) ? PraxisWalletData.DefaultProcessor : DefaultCardProcessor);

    public static string Method(PraxisGateway gateway) =>
        NullIfEmpty(gateway.PaymentMethod) ?? (IsEWallet(gateway) ? PraxisWalletData.DefaultMethod : DefaultCardMethod);

    /// <summary>One <c>gateway_list</c> entry (<c>PraxisGatewayResponse</c>).</summary>
    public static JsonObject ListEntry(PraxisGateway gateway, PraxisMerchant merchant)
    {
        var ewallet = IsEWallet(gateway);
        return new JsonObject
        {
            ["gateway"] = gateway.Id,
            ["gateway_type"] = ewallet ? "apm" : "card",
            ["payment_method"] = Method(gateway),
            ["display_name"] = Processor(gateway),
            ["system_name"] = SystemName(gateway, merchant),
            ["image"] = "",
            ["bdcc"] = 0,
            ["payment_information"] = new JsonObject
            {
                ["deposit_supported"] = 1,
                ["authorization_supported"] = 0,
                ["payout_supported"] = 1,
                ["mit_supported"] = 0,
                ["input_fields"] = ewallet
                    ? new JsonArray(PraxisWalletData.PaymentFields.Select(f => (JsonNode)ListInputField(f)).ToArray())
                    : new JsonArray(),
                ["card_types"] = new JsonArray(),
                ["processing_currencies"] = ewallet
                    ? Currencies(WalletLimitedCurrencies, WalletOpenCurrencies)
                    : Currencies(CardLimitedCurrencies, CardOpenCurrencies),
                ["fees"] = new JsonArray(),
                ["spread"] = new JsonArray(),
                ["countries_supported"] = new JsonArray(Countries.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray())
            }
        };
    }

    /// <summary>One <c>gateway_options</c> entry (<c>PraxisGatewayOption</c>).</summary>
    public static JsonObject OptionsEntry(PraxisGateway gateway, PraxisMerchant merchant)
    {
        var ewallet = IsEWallet(gateway);
        return new JsonObject
        {
            ["payment_method"] = Method(gateway),
            ["payment_processor"] = Processor(gateway),
            ["gateway"] = gateway.Id,
            ["system_name"] = SystemName(gateway, merchant),
            ["image"] = "",
            ["status"] = "active",
            ["deposit_supported"] = 1,
            ["authorization_supported"] = 0,
            ["payout_supported"] = 1,
            ["refund_supported"] = ewallet ? 0 : 1,
            ["is_3d"] = ewallet ? null : "no",
            ["input_fields"] = ewallet
                ? new JsonObject
                {
                    ["input_fields_for_payment"] = new JsonArray(PraxisWalletData.PaymentFields.Select(f => (JsonNode)OptionsInputField(f)).ToArray()),
                    ["input_fields_for_withdrawal"] = new JsonArray(PraxisWalletData.WithdrawalFields.Select(f => (JsonNode)OptionsInputField(f)).ToArray())
                }
                : null
        };
    }

    private static string SystemName(PraxisGateway gateway, PraxisMerchant merchant) => $"{Processor(gateway)} - {merchant.MerchantId}";

    /// <summary>An input field of <c>get-available-gateways</c>: <c>options</c> is a JSON string, as captured.</summary>
    private static JsonObject ListInputField(PraxisInputField field)
    {
        var node = new JsonObject
        {
            ["key"] = field.Key,
            ["label"] = field.Label,
            ["default_value"] = field.DefaultValue,
            ["required"] = field.Required,
            ["format"] = field.Format,
            ["min"] = 0,
            ["max"] = 50
        };
        if (field.Options is not null)
        {
            node["options"] = OptionsObject(field.Options).ToJsonString();
        }
        return node;
    }

    /// <summary>An input field of <c>get-gateway-options</c>: <c>options</c> is an object and a null default is "", as captured.</summary>
    private static JsonObject OptionsInputField(PraxisInputField field)
    {
        var node = new JsonObject
        {
            ["key"] = field.Key,
            ["label"] = field.Label,
            ["required"] = field.Required,
            ["format"] = field.Format,
            ["min"] = 0,
            ["default_value"] = field.DefaultValue ?? "",
            ["max"] = 50
        };
        if (field.Options is not null)
        {
            node["options"] = OptionsObject(field.Options);
        }
        return node;
    }

    private static JsonObject OptionsObject(IReadOnlyDictionary<string, string> options)
    {
        var node = new JsonObject();
        foreach (var (value, label) in options)
        {
            node[value] = label;
        }
        return node;
    }

    private static JsonObject Currencies(IEnumerable<string> limited, IEnumerable<string> open)
    {
        var node = new JsonObject();
        foreach (var currency in limited)
        {
            node[currency] = Currency(currency, "1.0000000000", "10000.0000000000");
        }
        foreach (var currency in open)
        {
            node[currency] = Currency(currency, null, null);
        }
        return node;
    }

    private static JsonObject Currency(string currency, string? min, string? max) => new()
    {
        ["currency"] = currency,
        ["min_amount"] = min,
        ["max_amount"] = max,
        ["is_bdcc_enabled"] = 0,
        ["bdcc"] = 0
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
