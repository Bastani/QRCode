/////////////////////////////////////////////////////////////////////
//
//	QR Code Encoder Library
//
//	QR Code Encoder command line
//
//	Author: Uzi Granot
//	Original Version: 1.0
//	Date: June 30, 2018
//	Copyright (C) 2018-2022 Uzi Granot. All Rights Reserved
//
//	QR Code Library C# class library and the attached test/demo
//  applications are free software.
//	Software developed by this author is licensed under CPOL 1.02.
//	Some portions of the QRCodeVideoDecoder are licensed under GNU Lesser
//	General Public License v3.0.
//
//	The main points of CPOL 1.02 subject to the terms of the License are:
//
//	Source Code and Executable Files can be used in commercial applications;
//	Source Code and Executable Files can be redistributed; and
//	Source Code can be modified to create derivative works.
//	No claim of suitability, guarantee, or any warranty whatsoever is
//	provided. The software is provided "as-is".
//	The Article accompanying the Work may not be distributed or republished
//	without the Author's consent
//
//	For full version history please look at QREncode.cs
/////////////////////////////////////////////////////////////////////

namespace QRCodeEncoderLibrary;
// calling example for console application
//
// try
//	{
//	QRCodeCommandLine.Encode(Environment.CommandLine);
//	return 0;
//	}
// catch(Exception Ex)
//	{
//	Console.WriteLine(Ex.Message);
//	return 1;
//	}

/// <summary>
///     Command line class
/// </summary>
public static class QRCodeCommandLine
{
	/// <summary>
	///     Command line help text
	/// </summary>
	public static readonly string Help =
		"QRCode encoder console application support.\r\n" +
		"QRCodeEncoderCore [optional arguments] input-file output-file\r\n" +
		"Output file must have .png extension.\r\n" +
		"Options format: /code:value or -code:value (the : can be =).\r\n" +
		"Error correction level: code=[error|e], value=[low|l|medium|m|quarter|q|high|h], default=m\r\n" +
		"Module size: code=[module|m], value=[1-100], default=2\r\n" +
		"Quiet zone: code=[quiet|q], value=[2-200], default=4, min=2*width\r\n" +
		"ECI Assign Value: code=[value|v], value=[0-999999], default is no ECI value.\r\n" +
		"Text file format: code=[text|t] see notes below:\r\n" +
		"Input file is binary unless text file option is specified.\r\n" +
		"If input file format is text, character set is iso-8859-1\r\n\r\n" +
		"Example:\r\n" +
		"QRCodeEncoder -m:4 -q:10 -t QRCodeText.txt QRImage.png\r\n";

	/// <summary>
	///     Encode QRCode using command line class
	/// </summary>
	/// <param name="commandLine">Command line text</param>
	public static void Encode
	(
		string commandLine
	)
	{
		// command line has no quote characters
		if (commandLine.IndexOf('"') < 0)
		{
			Encode(commandLine.Split(new[] { ' ' }));
			return;
		}

		// command line has quote characters
		List<string> args = new();
		var ptr = 0;
		int ptr1;
		int ptr2;
		for (;;)
		{
			// skip white
			for (; ptr < commandLine.Length && commandLine[ptr] == ' '; ptr++) ;
			if (ptr == commandLine.Length) break;

			// test for quote
			if (commandLine[ptr] == '"')
			{
				// look for next quote
				ptr++;
				ptr1 = commandLine.IndexOf('"', ptr);
				if (ptr1 < 0) throw new ApplicationException("Unbalanced double quote");
				ptr2 = ptr1 + 1;
			}
			else
			{
				// look for next white
				ptr1 = commandLine.IndexOf(' ', ptr);
				if (ptr1 < 0) ptr1 = commandLine.Length;
				ptr2 = ptr1;
			}

			args.Add(commandLine[ptr..ptr1]);
			ptr = ptr2;
		}

		Encode(args.ToArray());
	}

	/// <summary>
	///     Command line encode
	/// </summary>
	/// <param name="args">Arguments array</param>
	private static void Encode
	(
		string[] args
	)
	{
		// help
		if (args == null || args.Length < 2)
			throw new ApplicationException(Help);

		var textFile = false;
		string inputFileName = null;
		string outputFileName = null;
		string code;
		string value;
		var errCorr = ErrorCorrection.M;
		var moduleSize = 2;
		var quietZone = 8;
		var eciValue = -1;

		for (var argPtr = 1; argPtr < args.Length; argPtr++)
		{
			var arg = args[argPtr];

			// file name
			if (arg[0] != '/' && arg[0] != '-')
			{
				if (inputFileName == null)
				{
					inputFileName = arg;
					continue;
				}

				if (outputFileName == null)
				{
					outputFileName = arg;
					continue;
				}

				throw new ApplicationException(string.Format("Invalid option. Argument={0}", argPtr + 1));
			}

			// search for colon
			var ptr = arg.IndexOf(':');
			if (ptr < 0) ptr = arg.IndexOf('=');
			if (ptr > 0)
			{
				code = arg[1..ptr];
				value = arg[(ptr + 1)..];
			}
			else
			{
				code = arg[1..];
				value = string.Empty;
			}

			code = code.ToLower();
			value = value.ToLower();

			switch (code)
			{
				case "error":
				case "e":
					errCorr = value switch
					{
						"low" or "l" => ErrorCorrection.L,
						"medium" or "m" => ErrorCorrection.M,
						"quarter" or "q" => ErrorCorrection.Q,
						"high" or "h" => ErrorCorrection.H,
						_ => throw new ApplicationException("Error correction option in error")
					};
					break;

				case "module":
				case "m":
					if (!int.TryParse(value, out moduleSize)) moduleSize = -1;
					break;

				case "quiet":
				case "q":
					if (!int.TryParse(value, out quietZone)) quietZone = -1;
					break;

				case "value":
				case "v":
					if (!int.TryParse(value, out eciValue)) eciValue = -1;
					break;

				case "text":
				case "t":
					textFile = true;
					break;

				default:
					throw new ApplicationException(string.Format("Invalid argument no {0}, code {1}", argPtr + 1,
						code));
			}
		}

		bool[,] qrCodeMatrix;

		QREncoder encoder = new();
		encoder.ErrorCorrection = errCorr;
		if (eciValue != -1) encoder.EciAssignValue = eciValue;
		if (textFile)
		{
			var inputText = File.ReadAllText(inputFileName);
			qrCodeMatrix = encoder.Encode(inputText);
		}
		else
		{
			var inputBytes = File.ReadAllBytes(inputFileName);
			qrCodeMatrix = encoder.Encode(inputBytes);
		}

		QRSavePngImage pngImage = new(qrCodeMatrix);
		if (moduleSize != -1) pngImage.ModuleSize = moduleSize;
		if (quietZone != -1) pngImage.QuietZone = quietZone;
		pngImage.SaveQRCodeToPngFile(outputFileName);
	}
}