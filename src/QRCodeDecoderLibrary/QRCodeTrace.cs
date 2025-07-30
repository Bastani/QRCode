/////////////////////////////////////////////////////////////////////
//
//	QR Code Library
//
//	QR Code trace for debuging.
//
//	Author: Uzi Granot
//
//	Current Version: 3.1.0
//	Date: March 7, 2022
//
//	Original Version: 1.0
//	Date: June 30, 2018
//
//	Copyright (C) 2018-2022 Uzi Granot. All Rights Reserved
//
//	QR Code Library C# class library and the attached test/demo
//  applications are free software.
//	Software developed by this author is licensed under CPOL 1.02.
//	Some portions of the QRCodeVideoDecoder are licensed under GNU Lesser
//	General Public License v3.0.
//
//	The video decoder is using some of the source modules of
//	Camera_Net project published at CodeProject.com:
//	https://www.codeproject.com/Articles/671407/Camera_Net-Library
//	and at GitHub: https://github.com/free5lot/Camera_Net.
//	This project is based on DirectShowLib.
//	http://sourceforge.net/projects/directshownet/
//	This project includes a modified subset of the source modules.
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
//	For version history please refer to QRDecoder.cs
/////////////////////////////////////////////////////////////////////

namespace QRCodeDecoderLibrary;
#if DEBUG
/////////////////////////////////////////////////////////////////////
// Trace Class
/////////////////////////////////////////////////////////////////////
public static class QRCodeTrace
{
	private static string _traceFileName; // trace file name
	private static readonly int MaxAllowedFileSize = 1024 * 1024;

	/////////////////////////////////////////////////////////////////////
	// Open trace file
	/////////////////////////////////////////////////////////////////////

	public static void Open
	(
		string fileName
	)
	{
		// save full file name
		_traceFileName = Path.GetFullPath(fileName);
		Write("----");
	}

	/////////////////////////////////////////////////////////////////////
	// write to trace file
	/////////////////////////////////////////////////////////////////////

	public static void Format
	(
		string message,
		params object[] argArray
	)
	{
		if (argArray.Length == 0)
			Write(message);
		else
			Write(string.Format(message, argArray));
	}

	/////////////////////////////////////////////////////////////////////
	// write to trace file
	/////////////////////////////////////////////////////////////////////

	public static void Write
	(
		string message
	)
	{
		// test file length
		TestSize();

		// open existing or create new trace file
		StreamWriter traceFile = new(_traceFileName, true);

		// write date and time
		traceFile.Write("{0:yyyy}/{0:MM}/{0:dd} {0:HH}:{0:mm}:{0:ss} ", DateTime.Now);

		// write message
		traceFile.WriteLine(message);

		// close the file
		traceFile.Close();

		// exit
	}

	/////////////////////////////////////////////////////////////////////
	// Test file size
	// If file is too big, remove first quarter of the file
	/////////////////////////////////////////////////////////////////////
	private static void TestSize()
	{
		// get trace file info
		FileInfo traceFileInfo = new(_traceFileName);

		// if file does not exist or file length less than max allowed file size do nothing
		if (traceFileInfo.Exists == false || traceFileInfo.Length <= MaxAllowedFileSize) return;

		// create file info class
		FileStream traceFile = new(_traceFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

		// seek to 25% length
		traceFile.Seek(traceFile.Length / 4, SeekOrigin.Begin);

		// new file length
		var newFileLength = (int)(traceFile.Length - traceFile.Position);

		// new file buffer
		var buffer = new byte[newFileLength];

		// read file to the end
		traceFile.Read(buffer, 0, newFileLength);

		// search for first end of line
		var startPtr = 0;
		while (startPtr < 1024 && buffer[startPtr++] != '\n') ;
		if (startPtr == 1024) startPtr = 0;

		// seek to start of file
		traceFile.Seek(0, SeekOrigin.Begin);

		// write 75% top part of file over the start of the file
		traceFile.Write(buffer, startPtr, newFileLength - startPtr);

		// truncate the file
		traceFile.SetLength(traceFile.Position);

		// close the file
		traceFile.Close();

		// exit
	}
}
#endif