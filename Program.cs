//************************************************************************************************
// Copyright © 2020 Steven M Cohn. All rights reserved.
//************************************************************************************************

namespace ClipboardViewer
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Windows.Forms;
	using System.Xml.Linq;

	// Formats
	// https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats

	class Program
	{
		private bool saveStreams;


		// clipboard must be accessed from STA thread; can either declare it here or
		// use Threading namespace to create a new STA thread
		[STAThread]
		static void Main(string[] args)
		{
			new Program().Run(args);
		}


		private void Run(string[] args)
		{
			var autoConvert = args.Contains("--all") || args.Contains("--auto");
			saveStreams = args.Any(a => a.StartsWith("--save"));

			var data = Clipboard.GetDataObject();
			var formats = data.GetFormats();
			DumpFormats(formats);

			foreach (var format in formats)
			{
				var item = data.GetData(format, autoConvert);
				if (item is not null)
				{
					DumpContent(item, format, item.GetType().FullName);
				}
			}
		}

		private void DumpFormats(string[] formats)
		{
			Console.WriteLine();
			ConsoleWrite("Formats: ", ConsoleColor.DarkBlue);
			ConsoleWrite("[", ConsoleColor.DarkYellow);
			ConsoleWrite(string.Join(", ", formats), ConsoleColor.Blue);
			ConsoleWriteLine("]", ConsoleColor.DarkYellow);
		}


		private void DumpContent(object item, string format, string typeName)
		{
			string content;
			string bytehead = null;
			int length;

			if (item is MemoryStream stream)
			{
				var buffer = stream.ToArray();

				if (format == "Locale")
				{
					var value = 0;
					for (int i = 0; i < buffer.Length; i++)
					{
						value += buffer[i] << (i * 8);
					}

					content = value.ToString();
				}
				else if (format == "OneNote 2016 Internal")
				{
					content = "<< internal >>";
				}
				else
				{
					bytehead = buffer.Take(10).Aggregate("", (a, b) => $"{a:x02}0x{b:x02} ");

					var detector = new ImageDetector();
					var signature = detector.GetSignature(buffer);

					if (signature == ImageSignature.Unknown)
					{
						content = Encoding.UTF8.GetString(buffer);
					}
					else if (saveStreams)
					{
						var filetype = format == "DeviceIndependentBitmap"
							? "dib"
							: signature.ToString().ToLower();

						var filename = Path.GetFileNameWithoutExtension(Path.GetRandomFileName())
							+ $".{filetype}";

						var outpath = Path.Combine(Path.GetTempPath(), filename);
						using var outstream = new FileStream(outpath, FileMode.CreateNew);
						outstream.Write(buffer, 0, buffer.Length);

						content = $"<< image: {signature} @ {outpath} >>";
					}
					else
					{
						content = $"<< image: {signature} >>";
					}
				}

				length = buffer.Length;
			}
			else
			{
				content = item.ToString();
				length = content.Length;
			}

			Console.WriteLine();
			ConsoleWriteLine($"{format} - {length} chars ({typeName})", ConsoleColor.Yellow);

			if (bytehead is not null)
			{
				ConsoleWriteLine($"Bytes 0..9 {{ {bytehead}}}", ConsoleColor.DarkYellow);
			}

			if (content.StartsWith("Version:"))
			{
				var start = content.IndexOf('<');
				var preamble = content.Substring(0, start);
				ConsoleWrite(preamble, ConsoleColor.DarkCyan);

				// trim html preamble if it's present
				content = content.Substring(start);
			}
			else if (content.StartsWith("<") && content.EndsWith(">"))
			{
				try
				{
					// format XML
					content = XElement.Parse(content).ToString(SaveOptions.None);
				}
				catch { /* no-op */ }
			}

			ConsoleWrite("[", ConsoleColor.DarkYellow);
			ConsoleWrite(content, ConsoleColor.DarkGray);
			ConsoleWriteLine("]", ConsoleColor.DarkYellow);
		}


		internal static void ConsoleWrite(string text, ConsoleColor color)
		{
			var save = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.Write(text);
			Console.ForegroundColor = save;
		}

		internal static void ConsoleWriteLine(string text, ConsoleColor color)
		{
			var save = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(text);
			Console.ForegroundColor = save;
		}
	}
}
