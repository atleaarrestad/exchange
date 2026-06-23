using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using System.Net;
using System.Text;

namespace Tests;

[TestClass]
public sealed class KrakenBlockchainTransferGatewayTests
{
    [TestMethod]
    public async Task SubmitAsync_WithValidResponse_ReturnsGatewayTransactionId()
    {
        var handler = new StubHttpMessageHandler((request, body) =>
        {
            Assert.AreEqual("/0/private/Withdraw", request.RequestUri?.AbsolutePath);
            Assert.IsTrue(request.Headers.Contains("API-Key"));
            Assert.IsTrue(request.Headers.Contains("API-Sign"));
            StringAssert.Contains(body, "refid=idem-1");
            StringAssert.Contains(body, "address=bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080");
            return CreateJsonResponse("""{"error":[],"result":{"refid":"kraken-ref-1"}}""");
        });
        var gateway = CreateGateway(handler);
        var request = new BlockchainTransferRequest(
            "idem-1",
            "account-1",
            "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080",
            AssetSymbol.Bitcoin,
            0.10m,
            0.001m,
            0.101m);

        var result = await gateway.SubmitAsync(request);

        Assert.AreEqual("kraken-ref-1", result.GatewayTransactionId);
        Assert.AreEqual(3, result.RequiredConfirmations);
    }

    [TestMethod]
    public async Task GetTransferStatusAsync_WithMatchingPendingStatus_ReturnsSubmitted()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.AreEqual("/0/private/WithdrawStatus", request.RequestUri?.AbsolutePath);
            return CreateJsonResponse("""{"error":[],"result":[{"refid":"idem-1","txid":"tx-1","status":"Pending","time":1710000000.0}]}""");
        });
        var gateway = CreateGateway(handler);

        var status = await gateway.GetTransferStatusAsync("account-1", AssetSymbol.Bitcoin, "idem-1");

        Assert.AreEqual(BlockchainTransferStatusKind.Submitted, status.StatusKind);
        Assert.AreEqual("tx-1", status.GatewayTransactionId);
        Assert.AreEqual(3, status.RequiredConfirmations);
    }

    [TestMethod]
    public async Task GetTransferStatusAsync_WithNoMatchingTransfer_ReturnsNotSubmitted()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            CreateJsonResponse("""{"error":[],"result":[{"refid":"different","status":"Pending"}]}"""));
        var gateway = CreateGateway(handler);

        var status = await gateway.GetTransferStatusAsync("account-1", AssetSymbol.Ether, "idem-2");

        Assert.AreEqual(BlockchainTransferStatusKind.NotSubmitted, status.StatusKind);
    }

    [TestMethod]
    public async Task GetTransferStatusAsync_WithUnknownStatus_ReturnsUnknown()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            CreateJsonResponse("""{"error":[],"result":[{"refid":"idem-3","status":"QueuedForReview"}]}"""));
        var gateway = CreateGateway(handler);

        var status = await gateway.GetTransferStatusAsync("account-1", AssetSymbol.Ether, "idem-3");

        Assert.AreEqual(BlockchainTransferStatusKind.Unknown, status.StatusKind);
    }

    [TestMethod]
    public async Task SubmitAsync_WithInvalidCredentialsError_ThrowsDependencyNotConfiguredException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            CreateJsonResponse("""{"error":["EAPI:Invalid key"],"result":{}}"""));
        var gateway = CreateGateway(handler);
        var request = new BlockchainTransferRequest(
            "idem-4",
            "account-1",
            "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080",
            AssetSymbol.Bitcoin,
            0.25m,
            0.001m,
            0.251m);

        await Assert.ThrowsExactlyAsync<ExternalDependencyNotConfiguredException>(() => gateway.SubmitAsync(request));
    }

    private static KrakenBlockchainTransferGateway CreateGateway(HttpMessageHandler handler)
    {
        var options = new KrakenBlockchainTransferGatewayOptions
        {
            Enabled = true,
            BaseUrl = "https://api.sandbox.kraken.com",
            HttpTimeoutSeconds = 15,
            ApiKey = "key",
            ApiSecret = "c2VjcmV0",
            BitcoinWithdrawalKey = "btc-key",
            EtherWithdrawalKey = "eth-key",
            BitcoinRequiredConfirmations = 3,
            EtherRequiredConfirmations = 12
        };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl)
        };
        return new KrakenBlockchainTransferGateway(options, httpClient, TimeProvider.System);
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, string, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request, body);
        }
    }
}
