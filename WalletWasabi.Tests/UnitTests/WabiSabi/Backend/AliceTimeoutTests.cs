using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class AliceTimeoutTests
{
	[Fact]
	public async Task AliceRegistrationTimesOutAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5)); // Sanity timeout for the unit test.
		using CancellationTokenSource silentLeave = new();
		var silentLeaveToken = silentLeave.Token;

		// Alice times out when its deadline is reached.
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(cfg);
		var km = ServiceFactory.CreateKeyManager(password: "");
		var key = BitcoinFactory.CreateHdPubKey(km);
		var smartCoin = BitcoinFactory.CreateSmartCoin(key, 10m);
		var rpc = WabiSabiTestFactory.CreatePreconfiguredRpcClient(smartCoin.Coin);

		using Arena arena = await ArenaTestFactory.From(cfg).With(rpc).CreateAndStartAsync(round);
		var arenaClient = WabiSabiTestFactory.CreateArenaClient(arena);

		using RoundStateUpdater roundStateUpdater = new(TimeSpan.FromSeconds(2), ["CoinJoinCoordinatorIdentifier"], arena);
		await roundStateUpdater.StartAsync(testDeadlineCts.Token);

		// Register Alices.
		KeyChain keyChain = new(km, new Kitchen(ingredients: ""));

		using CancellationTokenSource registrationCts = new();
		Task<AliceClient> task = AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), arenaClient, smartCoin, keyChain, roundStateUpdater, registrationCts.Token, registrationCts.Token, confirmationCancellationToken: testDeadlineCts.Token, silentLeaveToken);

		while (round.Alices.Count == 0)
		{
			await Task.Delay(10, testDeadlineCts.Token);
		}

		var alice = Assert.Single(round.Alices);
		alice.Deadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		Assert.Empty(round.Alices);

		registrationCts.Cancel();

		try
		{
			await task;
			throw new InvalidOperationException("The operation should throw!");
		}
		catch (Exception ex)
		{
			Assert.True(ex is OperationCanceledException or WabiSabiProtocolException);
		}

		await roundStateUpdater.StopAsync(testDeadlineCts.Token);
		await arena.StopAsync(testDeadlineCts.Token);
	}

	[Fact]
	public async Task AliceDoesntTimeoutInConnectionConfirmationAsync()
	{
		// Alice does not time out when it's not input registration anymore,
		// even though the deadline is reached.
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(cfg);
		round.SetPhase(Phase.ConnectionConfirmation);
		var alice = WabiSabiTestFactory.CreateAlice(round);
		round.Alices.Add(alice);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiTestFactory.CreateConnectionConfirmationRequest(round);

		Assert.Single(round.Alices);
		DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		alice.Deadline = preDeadline;
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Single(round.Alices);
		Assert.Equal(preDeadline, alice.Deadline);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceDoesntTimeoutIfInputRegistrationTimedoutAsync()
	{
		// Alice does not time out if input registration timed out,
		// even though the deadline is reached and still in input reg.
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.StandardInputRegistrationTimeout = TimeSpan.Zero;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		var alice = WabiSabiTestFactory.CreateAlice(round);
		round.Alices.Add(alice);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiTestFactory.CreateConnectionConfirmationRequest(round);

		Assert.Single(round.Alices);
		DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		alice.Deadline = preDeadline;
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Single(round.Alices);
		Assert.Equal(preDeadline, alice.Deadline);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceDoesntTimeoutIfMaxInputCountReachedAsync()
	{
		// Alice does not time out if input reg is full with alices,
		// even though the deadline is reached and still in input reg.
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.MaxInputCountByRound = 3;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		var alice = WabiSabiTestFactory.CreateAlice(round);
		round.Alices.Add(alice);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiTestFactory.CreateConnectionConfirmationRequest(round);

		Assert.Equal(3, round.Alices.Count);
		DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		alice.Deadline = preDeadline;
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(3, round.Alices.Count);
		Assert.Equal(preDeadline, alice.Deadline);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceDeadlineUpdatedAsync()
	{
		// Alice's deadline is updated by connection confirmation.
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(cfg);
		var alice = WabiSabiTestFactory.CreateAlice(round);
		round.Alices.Add(alice);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiTestFactory.CreateConnectionConfirmationRequest(round);

		Assert.Single(round.Alices);
		DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		alice.Deadline = preDeadline;
		await arena.ConfirmConnectionAsync(req, CancellationToken.None);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Single(round.Alices);
		Assert.NotEqual(preDeadline, alice.Deadline);

		await arena.StopAsync(CancellationToken.None);
	}
}
