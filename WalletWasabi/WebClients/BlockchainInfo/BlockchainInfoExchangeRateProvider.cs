using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.BlockchainInfo;

public class BlockchainInfoExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken)
	{
		// Only used by the Backend.
#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://blockchain.info")
		};
#pragma warning restore RS0030 // Do not use banned APIs

		using var response = await httpClient.GetAsync("/ticker", cancellationToken).ConfigureAwait(false);
		using var content = response.Content;
		var rates = await content.ReadAsJsonAsync<BlockchainInfoExchangeRates>().ConfigureAwait(false);

		var exchangeRates = new List<ExchangeRate>
		{
			new ExchangeRate { Rate = rates.USD.Sell, Ticker = "USD" },
			new ExchangeRate { Rate = rates.EUR.Sell, Ticker = "EUR" },
			new ExchangeRate { Rate = rates.CNY.Sell, Ticker = "CNY" },
			new ExchangeRate { Rate = rates.HUF.Sell, Ticker = "HUF" },
		};

		return exchangeRates;
	}

	private class BlockchainInfoExchangeRate
	{
		public decimal Last { get; set; }
		public decimal Buy { get; set; }
		public decimal Sell { get; set; }
	}

	private class BlockchainInfoExchangeRates
	{
		public required BlockchainInfoExchangeRate USD { get; init; }
		public required BlockchainInfoExchangeRate EUR { get; init; }
		public required BlockchainInfoExchangeRate CNY { get; init; }
		public required BlockchainInfoExchangeRate HUF { get; init; }
	}
}
