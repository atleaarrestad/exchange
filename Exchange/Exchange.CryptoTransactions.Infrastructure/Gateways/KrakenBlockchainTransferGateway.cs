using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class KrakenBlockchainTransferGateway(
    KrakenBlockchainTransferGatewayOptions options,
    HttpClient httpClient,
    TimeProvider timeProvider) : IBlockchainTransferGateway
{
    private const string ApiKeyHeaderName = "API-Key";
    private const string ApiSignHeaderName = "API-Sign";
    private const string WithdrawPath = "/0/private/Withdraw";
    private const string WithdrawStatusPath = "/0/private/WithdrawStatus";
    private const string ReferenceIdField = "refid";
    private const string TransactionIdField = "txid";
    private static readonly string[] ConfigurationErrorPrefixes = ["EAPI:Invalid key", "EAPI:Invalid signature", "EGeneral:Permission denied"];

    private readonly byte[] apiSecretBytes = Convert.FromBase64String(options.ApiSecret!);

    public async Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var assetCode = MapKrakenAssetCode(request.AssetSymbol);
        var formValues = CreateFormValues(
            ("asset", assetCode),
            ("key", options.GetWithdrawalKey(request.AssetSymbol)),
            ("address", request.DestinationAddress.Trim()),
            ("amount", request.Amount.ToString("G29", CultureInfo.InvariantCulture)),
            (ReferenceIdField, request.IdempotencyKey.Trim()));
        using var payload = await SendPrivatePostAsync(WithdrawPath, formValues, cancellationToken);
        var result = ReadResultObject(payload);
        var referenceId = ReadRequiredString(result, ReferenceIdField);
        return new BlockchainTransferResult(
            referenceId,
            DateTimeOffset.UtcNow,
            options.GetRequiredConfirmations(request.AssetSymbol));
    }

    public async Task<BlockchainTransferStatus> GetTransferStatusAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedIdempotencyKey = idempotencyKey.Trim();
        var assetCode = MapKrakenAssetCode(assetSymbol);
        var formValues = CreateFormValues(
            ("asset", assetCode),
            ("method", options.GetWithdrawalKey(assetSymbol)));
        using var payload = await SendPrivatePostAsync(WithdrawStatusPath, formValues, cancellationToken);

        if (!TryFindMatchingStatus(payload, normalizedIdempotencyKey, out var statusItem))
        {
            return new BlockchainTransferStatus(BlockchainTransferStatusKind.NotSubmitted);
        }

        var statusText = ReadRequiredString(statusItem, "status");
        var statusKind = MapStatus(statusText);
        if (statusKind == BlockchainTransferStatusKind.Unknown)
        {
            return new BlockchainTransferStatus(BlockchainTransferStatusKind.Unknown);
        }

        if (statusKind == BlockchainTransferStatusKind.NotSubmitted)
        {
            return new BlockchainTransferStatus(BlockchainTransferStatusKind.NotSubmitted);
        }

        var gatewayTransactionId = TryReadString(statusItem, TransactionIdField)
            ?? TryReadString(statusItem, ReferenceIdField)
            ?? normalizedIdempotencyKey;
        var submittedAtUtc = TryReadSubmittedAt(statusItem) ?? DateTimeOffset.UtcNow;
        return new BlockchainTransferStatus(
            BlockchainTransferStatusKind.Submitted,
            gatewayTransactionId,
            submittedAtUtc,
            options.GetRequiredConfirmations(assetSymbol));
    }

    private static string MapKrakenAssetCode(AssetSymbol assetSymbol)
    {
        return assetSymbol.Value switch
        {
            "BTC" => "XBT",
            "ETH" => "ETH",
            _ => throw new ArgumentOutOfRangeException(nameof(assetSymbol), assetSymbol.Value, "Unsupported Kraken asset symbol.")
        };
    }

    private async Task<JsonDocument> SendPrivatePostAsync(
        string path,
        IReadOnlyList<KeyValuePair<string, string>> formValues,
        CancellationToken cancellationToken)
    {
        using var request = CreateSignedRequest(path, formValues);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var transient = IsTransientHttpStatusCode(response.StatusCode);
            throw new BlockchainTransferRejectedException(
                $"Kraken request failed with HTTP {(int)response.StatusCode}: {content}",
                transient);
        }

        var payload = JsonDocument.Parse(content);
        var errors = ReadErrors(payload.RootElement);
        if (errors.Count == 0)
        {
            return payload;
        }

        var combinedErrors = string.Join(", ", errors);
        if (errors.Any(IsConfigurationError))
        {
            payload.Dispose();
            throw new ExternalDependencyNotConfiguredException($"Kraken gateway configuration rejected request: {combinedErrors}");
        }

        payload.Dispose();
        throw new BlockchainTransferRejectedException(
            $"Kraken rejected request: {combinedErrors}",
            IsTransientKrakenError(errors));
    }

    private HttpRequestMessage CreateSignedRequest(string path, IReadOnlyList<KeyValuePair<string, string>> formValues)
    {
        var nonce = timeProvider.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var payloadWithNonce = new List<KeyValuePair<string, string>>(formValues.Count + 1) { new("nonce", nonce) };
        payloadWithNonce.AddRange(formValues);

        var encodedPayload = string.Join("&", payloadWithNonce.Select(static pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        var signature = CreateApiSignature(path, nonce, encodedPayload);
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(encodedPayload, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        request.Headers.Add(ApiKeyHeaderName, options.ApiKey);
        request.Headers.Add(ApiSignHeaderName, signature);
        return request;
    }

    private string CreateApiSignature(string path, string nonce, string encodedPayload)
    {
        var payloadHash = SHA256.HashData(Encoding.UTF8.GetBytes(nonce + encodedPayload));
        var pathBytes = Encoding.UTF8.GetBytes(path);
        var signedBytes = new byte[pathBytes.Length + payloadHash.Length];
        Buffer.BlockCopy(pathBytes, 0, signedBytes, 0, pathBytes.Length);
        Buffer.BlockCopy(payloadHash, 0, signedBytes, pathBytes.Length, payloadHash.Length);

        using var hmac = new HMACSHA512(apiSecretBytes);
        var signature = hmac.ComputeHash(signedBytes);
        return Convert.ToBase64String(signature);
    }

    private static List<string> ReadErrors(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var errorElement) || errorElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var errors = new List<string>();
        foreach (var errorItem in errorElement.EnumerateArray())
        {
            var value = errorItem.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                errors.Add(value);
            }
        }

        return errors;
    }

    private static JsonElement ReadResultObject(JsonDocument payload)
    {
        if (!payload.RootElement.TryGetProperty("result", out var resultElement) || resultElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Kraken payload is missing a valid result object.");
        }

        return resultElement;
    }

    private static bool TryFindMatchingStatus(JsonDocument payload, string idempotencyKey, out JsonElement statusItem)
    {
        if (!payload.RootElement.TryGetProperty("result", out var resultElement) || resultElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Kraken payload is missing a valid result array.");
        }

        foreach (var item in resultElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var referenceId = TryReadString(item, ReferenceIdField);
            if (string.Equals(referenceId, idempotencyKey, StringComparison.Ordinal))
            {
                statusItem = item;
                return true;
            }
        }

        statusItem = default;
        return false;
    }

    private static DateTimeOffset? TryReadSubmittedAt(JsonElement statusItem)
    {
        if (!statusItem.TryGetProperty("time", out var timeElement))
        {
            return null;
        }

        if (timeElement.ValueKind == JsonValueKind.Number && timeElement.TryGetDecimal(out var seconds))
        {
            var milliseconds = decimal.ToInt64(decimal.Truncate(seconds * 1000m));
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        if (timeElement.ValueKind == JsonValueKind.String
            && decimal.TryParse(timeElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSeconds))
        {
            var milliseconds = decimal.ToInt64(decimal.Truncate(parsedSeconds * 1000m));
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        return null;
    }

    private static string ReadRequiredString(JsonElement item, string propertyName)
    {
        var value = TryReadString(item, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Kraken payload is missing required property '{propertyName}'.");
    }

    private static string? TryReadString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var valueElement))
        {
            return null;
        }

        if (valueElement.ValueKind == JsonValueKind.String)
        {
            return valueElement.GetString();
        }

        if (valueElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var arrayItem in valueElement.EnumerateArray())
            {
                var candidate = arrayItem.GetString();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool IsConfigurationError(string error)
    {
        return ConfigurationErrorPrefixes.Any(prefix => error.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool IsTransientHttpStatusCode(HttpStatusCode statusCode)
    {
        var numericCode = (int)statusCode;
        return numericCode == 429 || numericCode >= 500;
    }

    private static bool IsTransientKrakenError(IReadOnlyList<string> errors)
    {
        foreach (var error in errors)
        {
            if (error.StartsWith("EAPI:Rate limit exceeded", StringComparison.Ordinal)
                || error.StartsWith("EService:Unavailable", StringComparison.Ordinal)
                || error.StartsWith("EService:Busy", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static BlockchainTransferStatusKind MapStatus(string status)
    {
        var normalized = status.Trim().ToUpperInvariant();
        return normalized switch
        {
            "INITIAL" => BlockchainTransferStatusKind.Submitted,
            "PENDING" => BlockchainTransferStatusKind.Submitted,
            "ON HOLD" => BlockchainTransferStatusKind.Submitted,
            "ON_HOLD" => BlockchainTransferStatusKind.Submitted,
            "SETTLED" => BlockchainTransferStatusKind.Submitted,
            "SUCCESS" => BlockchainTransferStatusKind.Submitted,
            "FAILURE" => BlockchainTransferStatusKind.NotSubmitted,
            "FAILED" => BlockchainTransferStatusKind.NotSubmitted,
            "CANCELED" => BlockchainTransferStatusKind.NotSubmitted,
            "CANCELLED" => BlockchainTransferStatusKind.NotSubmitted,
            "DENIED" => BlockchainTransferStatusKind.NotSubmitted,
            "EXPIRED" => BlockchainTransferStatusKind.NotSubmitted,
            _ => BlockchainTransferStatusKind.Unknown
        };
    }

    private static List<KeyValuePair<string, string>> CreateFormValues(params (string Key, string Value)[] entries)
    {
        var list = new List<KeyValuePair<string, string>>(entries.Length);
        foreach (var (key, value) in entries)
        {
            list.Add(new KeyValuePair<string, string>(key, value));
        }

        return list;
    }
}
