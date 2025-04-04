using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Helpers;

public static partial class TextHelpers
{
	public static string AddSIfPlural(int n) => n > 1 ? Lang.Resources.Plural : "";

	public static string CloseSentenceIfZero(params int[] counts) => counts.All(x => x == 0) ? "." : " ";

	private static string ConcatNumberAndUnit(int n, string unit) => n > 0 ? $"{n} {unit}{AddSIfPlural(n)}" : "";

	[GeneratedRegex(@"\s+")]
	private static partial Regex ParseLabelRegex();

	private static void AddIfNotEmpty(List<string> list, string item)
	{
		if (!string.IsNullOrEmpty(item))
		{
			list.Add(item);
		}
	}

	public static string TimeSpanToFriendlyString(TimeSpan time)
	{
		var textMembers = new List<string>();
		string result = "";

		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Days, Lang.Resources.day));
		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Hours, Lang.Resources.hour));
		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Minutes, Lang.Resources.minute));
		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Seconds, Lang.Resources.second));

		for (int i = 0; i < textMembers.Count; i++)
		{
			result += textMembers[i];

			if (textMembers.Count > 1 && i < textMembers.Count - 2)
			{
				result += ", ";
			}
			else if (textMembers.Count > 1 && i == textMembers.Count - 2)
			{
				result += $" {Lang.Resources.and} ";
			}
		}

		return result;
	}

	public static string ToBtcWithUnit(this Money money, bool fplus = false)
	{
		return money.ToFormattedString(fplus) + " BTC";
	}

	public static string ToFormattedString(this Money money, bool fplus = false)
	{
		const int WholeGroupSize = 3;

		var moneyString = money.ToString(fplus: fplus, false);

		moneyString = moneyString.Insert(moneyString.Length - 4, " ");

		var startIndex = moneyString.IndexOf(".", StringComparison.Ordinal) - WholeGroupSize;

		if (startIndex > 0)
		{
			for (var i = startIndex; i > 0; i -= WholeGroupSize)
			{
				moneyString = moneyString.Insert(i, " ");
			}
		}

		return moneyString;
	}

	public static string ParseLabel(this string text) => ParseLabelRegex().Replace(text, " ").Trim();

	public static string TotalTrim(this string text)
	{
		return text
			.Replace("\r", "")
			.Replace("\n", "")
			.Replace("\t", "")
			.Replace(" ", "");
	}

	public static string GetPrivacyMask(int repeatCount)
	{
		return new string(UiConstants.PrivacyChar, repeatCount);
	}

	public static string GetConfirmationText(int confirmations)
	{
		return string.Format(CultureInfo.InvariantCulture, Resources.ConfirmedWithConfirmationCount, confirmations, AddSIfPlural(confirmations));
	}

	public static string FormatPercentageDiff(double n)
	{
		var precision = 0.01m;
		var withFriendlyDecimals = (n * 100).WithFriendlyDecimals();

		if (Math.Abs(withFriendlyDecimals) < precision)
		{
			var num = n >= 0 ? precision : -precision;
			return $"{Resources.LessThan} {num.ToString("+0.##;-0.##", CultureInfo.InvariantCulture)}%";
		}
		else
		{
			return $"{withFriendlyDecimals.ToString("+0.##;-0.##", CultureInfo.InvariantCulture)}%";
		}
	}
}
