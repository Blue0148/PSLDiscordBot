﻿using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using yt6983138.Common;

namespace PSLDiscordBot;
public static class Manager
{
	private static volatile Dictionary<ulong, UserData> _registeredUsers;
	private static EventId EventId { get; } = new(114510, "Manager");
	private static object FakeState { get => null!; }

	public const string ConfigLocation = "./Config.json";
	public static bool FirstStart { get; private set; }
	public static Config Config { get; set; }
	public static Logger Logger { get; set; }
	public static DiscordSocketClient SocketClient { get; set; } = new();
	public static Dictionary<ulong, UserData> RegisteredUsers
	{
		get => _registeredUsers;
		set => _registeredUsers = value;
	}
	public static IReadOnlyDictionary<string, float[]> Difficulties { get; set; }
	public static IReadOnlyDictionary<string, string> Names { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	static Manager()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	{
		FileInfo file = new(ConfigLocation);
		if (!file.Directory!.Exists)
			file.Directory!.Create();
#pragma warning disable CS0162 // Unreachable code detected
		try
		{
#if DEBUG
			throw null!;
#endif
			Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigLocation))!;
			FirstStart = false;
		}
		catch
		{
			Config = new();
			FirstStart = true;
		}
#pragma warning restore CS0162 // Unreachable code detected
		FileInfo user = new(Config.UserDataLocation);
		if (!file.Directory!.Exists)
			file.Directory!.Create();
		try
		{
			RegisteredUsers = JsonConvert.DeserializeObject<Dictionary<ulong, UserData>>(File.ReadAllText(Config.UserDataLocation))!;
		}
		catch
		{
			RegisteredUsers = new();
		}

		Logger = new(Config.LogLocation);
		if (!Config.Verbose)
		{
			Logger.Disabled.Add(LogLevel.Debug);
		}
		AppDomain.CurrentDomain.ProcessExit += (_, _2) => { WriteEverything(); Console.WriteLine("Shutting down..."); };
#if DEBUG
		if (true)
#else
		if (FirstStart)
#endif
		{
			using (HttpClient client = new())
			{
				Task<byte[]> diff = client.GetByteArrayAsync(@"https://yt6983138.github.io/Assets/RksReader/Latest/difficulty.csv");
				Task<byte[]> name = client.GetByteArrayAsync(@"https://yt6983138.github.io/Assets/RksReader/Latest/info.csv");
				Task<byte[]> help = client.GetByteArrayAsync(@"https://raw.githubusercontent.com/yt6983138/PSLDiscordBot/master/help.md");
				Task<byte[]> zip = client.GetByteArrayAsync(@"https://github.com/yt6983138/PSLDiscordBot/raw/master/Assets.zip");
				diff.Wait(); // ^ no async constructor :(
				name.Wait();
				help.Wait();
				zip.Wait();
				File.WriteAllBytes(Config.DifficultyCsvLocation, diff.Result);
				File.WriteAllBytes(Config.NameCsvLocation, name.Result);
				File.WriteAllBytes(Config.HelpMDLocation, help.Result);
#if DEBUG
				DirectoryInfo asset = new("./Assets");
				Logger.Log(LogLevel.Debug, "copying");
				asset.Create();

				new DirectoryInfo(Secret.AssetsFolder).CopyFilesRecursively(asset);
#else
				File.WriteAllBytes("./Assets.zip", zip.Result);
				var fastZip = new ICSharpCode.SharpZipLib.Zip.FastZip();
				fastZip.ExtractZip("./Assets.zip", ".", "");
#endif
			}
		}
		ReadCsvs();
		Task.Run(AutoSave);
	}
	public static void WriteEverything()
	{
		File.WriteAllText(ConfigLocation, JsonConvert.SerializeObject(Config));
		File.WriteAllText(Config.UserDataLocation, JsonConvert.SerializeObject(RegisteredUsers));
	}
	private async static void AutoSave()
	{
		while (true)
		{
			lock (RegisteredUsers)
			{
				WriteEverything();
			}
			Logger.Log(LogLevel.Debug, "Auto saved.");
			await Task.Delay(Config.AutoSaveInterval);
		}
	}
	public static void ReadCsvs()
	{
		string[] csvFile = File.ReadAllLines(Config.DifficultyCsvLocation);
		Dictionary<string, float[]> diffculties = new();
		foreach (string line in csvFile)
		{
			try
			{
				float[] diffcultys = new float[4];
				string[] splitted = line.Split(",");
				for (byte i = 0; i < splitted.Length; i++)
				{
					if (i > 4 || i == 0) { continue; }
					if (!float.TryParse(splitted[i], out diffcultys[i - 1])) { Console.WriteLine($"Error processing {splitted[i]}"); }
				}
				// Console.WriteLine($"{splitted[0]}, {diffcultys[0]}, {diffcultys[1]}, {diffcultys[2]}, {diffcultys[3]}");
				diffculties.Add(splitted[0], diffcultys);
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, EventId, FakeState, ex);
			}
		}
		Difficulties = diffculties;

		string[] csvFile2 = File.ReadAllLines(Config.NameCsvLocation);
		Dictionary<string, string> names = new();
		foreach (string line in csvFile2)
		{
			try
			{
				string[] splitted = line.Split(@"\");
				names.Add(splitted[0], splitted[1]);
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, EventId, FakeState, ex);
			}
		}
		Names = names;
	}
}
