/////////////////////////////////////////////////////////////////////
//
//	QR Code Decoder Library
//
//	QR Code decoder.
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
//	2018/06/30: Version 1.0.0 Original version
//	2018/07/20: Version 1.1.0 DirectShowLib consolidation
//	2019/05/15: Version 2.0.0 The software was divided into two solutions. 
//				Encoder solution and Decoder solution. The encode solution is a 
//				multi-target solution. It will produce net462 netstandardapp2.0 libraries.
//	2019/07/22: Version 2.1.0 ECI Assignment Value support was added.
//	2022/03/01: Version 3.0.0 Software was upgraded to VS 2022 and C6.0
//	2022/03/07: Version 3.1.0 Fix problem with unplugging the camera
/////////////////////////////////////////////////////////////////////

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;

namespace QRCodeDecoderLibrary;

/// <summary>
///     QR Code error correction code enumeration
/// </summary>
public enum ErrorCorrection
{
	/// <summary>
	///     Low (01)
	/// </summary>
	L,

	/// <summary>
	///     Medium (00)
	/// </summary>
	M,

	/// <summary>
	///     Medium-high (11)
	/// </summary>
	Q,

	/// <summary>
	///     High (10)
	/// </summary>
	H
}

/// <summary>
///     QR Code encoding modes
/// </summary>
public enum EncodingMode
{
	/// <summary>
	///     Terminator
	/// </summary>
	Terminator,

	/// <summary>
	///     Numeric
	/// </summary>
	Numeric,

	/// <summary>
	///     Alpha numeric
	/// </summary>
	AlphaNumeric,

	/// <summary>
	///     Append
	/// </summary>
	Append,

	/// <summary>
	///     byte encoding
	/// </summary>
	Byte,

	/// <summary>
	///     FNC1 first
	/// </summary>
	Fnc1First,

	/// <summary>
	///     Unknown encoding constant
	/// </summary>
	Unknown6,

	/// <summary>
	///     Extended Channel Interpretaion (ECI) mode
	/// </summary>
	Eci,

	/// <summary>
	///     Kanji encoding (not implemented by this software)
	/// </summary>
	Kanji,

	/// <summary>
	///     FNC1 second
	/// </summary>
	Fnc1Second,

	/// <summary>
	///     Unknown encoding constant
	/// </summary>
	Unknown10,

	/// <summary>
	///     Unknown encoding constant
	/// </summary>
	Unknown11,

	/// <summary>
	///     Unknown encoding constant
	/// </summary>
	Unknown12,

	/// <summary>
	///     Unknown encoding constant
	/// </summary>
	Unknown13,

	/// <summary>
	///     Unknown encoding constant
	/// </summary>
	Unknown14,

	/// <summary>
	///     Unknown encoding constant
	/// </summary>
	Unknown15
}

/// <summary>
///     QR Code decoder class
/// </summary>
public class QRDecoder
{
	public const string VersionNumber = "Rev 3.1.0 - 2022-03-07";

	internal ErrorCorrection ErrorCorrection;
	internal int QRCodeVersion;
	internal int EciAssignValue;
	internal int QRCodeDimension;
	internal int MaskCode;

	internal int ImageWidth;
	internal int ImageHeight;
	internal bool[,] BlackWhiteImage;
	internal List<QRCodeFinder> FinderList;
	internal List<QRCodeFinder> AlignList;
	internal List<QRCodeResult> DataArrayList;
	internal int MaxCodewords;
	internal int MaxDataCodewords;
	internal int MaxDataBits;
	internal int ErrCorrCodewords;
	internal int BlocksGroup1;
	internal int DataCodewordsGroup1;
	internal int BlocksGroup2;
	internal int DataCodewordsGroup2;

	internal byte[] CodewordsArray;
	internal int CodewordsPtr;
	internal uint BitBuffer;
	internal int BitBufferLen;
	internal byte[,] BaseMatrix;
	internal byte[,] MaskMatrix;

	internal bool Trans4Mode;

	// transformation cooefficients from QR modules to image pixels
	internal double Trans3A;
	internal double Trans3B;
	internal double Trans3C;
	internal double Trans3d;
	internal double Trans3E;
	internal double Trans3F;

	// transformation matrix based on three finders plus one more point
	internal double Trans4A;
	internal double Trans4B;
	internal double Trans4C;
	internal double Trans4d;
	internal double Trans4E;
	internal double Trans4F;
	internal double Trans4G;
	internal double Trans4H;

	/// <summary>
	///     Error correction percent (L, M, Q, H)
	/// </summary>
	internal int[] ErrCorrPercent = new[] { 7, 15, 25, 30 };

	// Change
	internal const double SignatureMaxDeviation = 0.35; // 0.25
	internal const double HorVertScanMaxDistance = 2.0;
	internal const double ModuleSizeDeviation = 0.5; // 0.75;
	internal const double CornerSideLengthDev = 0.8;
	internal const double CornerRightAngleDev = 0.25; // about Sin(4 deg)
	internal const double AlignmentSearchArea = 0.3;

	/// <summary>
	///     Convert byte array to string using UTF8 encoding
	/// </summary>
	/// <param name="dataArray">Input array</param>
	/// <returns>Output string</returns>
	public static string ByteArrayToStr
	(
		byte[] dataArray
	)
	{
		var decoder = Encoding.UTF8.GetDecoder();
		var charCount = decoder.GetCharCount(dataArray, 0, dataArray.Length);
		var charArray = new char[charCount];
		decoder.GetChars(dataArray, 0, dataArray.Length, charArray, 0);
		return new string(charArray);
	}

	/// <summary>
	///     QR Code decode image file
	/// </summary>
	/// <param name="fileName"></param>
	/// <returns>Array of QRCodeResult</returns>
	/// <exception cref="ApplicationException"></exception>
	public QRCodeResult[] ImageDecoder
	(
		string fileName
	)
	{
		// test argument
		if (fileName == null) throw new ApplicationException("QRDecode.ImageDecoder File name is null");

		// load file image to bitmap
		var inputImageBitmap = SKBitmap.Decode(fileName);

		// decode bitmap
		return ImageDecoder(inputImageBitmap);
	}

	/// <summary>
	///     QRCode image decoder
	/// </summary>
	/// <param name="inputImageBitmap">Input image</param>
	/// <returns>Output byte arrays</returns>
	public QRCodeResult[] ImageDecoder
	(
		SKBitmap inputImageBitmap
	)
	{
#if DEBUG
		int start;
#endif
		try
		{
			// empty data string output
			DataArrayList = new List<QRCodeResult>();

			// save image dimension
			ImageWidth = inputImageBitmap.Width;
			ImageHeight = inputImageBitmap.Height;

#if DEBUG
			start = Environment.TickCount;
			QRCodeTrace.Write("Convert image to black and white");
#endif

			// convert input image to black and white boolean image
			if (!ConvertImageToBlackAndWhite(inputImageBitmap)) return null;

#if DEBUG
			QRCodeTrace.Format("Time: {0}", Environment.TickCount - start);
			QRCodeTrace.Write("Finders search");
#endif

			// horizontal search for finders
			if (!HorizontalFindersSearch()) return null;

#if DEBUG
			QRCodeTrace.Format("Horizontal Finders count: {0}", FinderList.Count);
#endif

			// vertical search for finders
			VerticalFindersSearch();

#if DEBUG
			var matchedCount = 0;
			foreach (var hf in FinderList)
				if (hf.Distance != double.MaxValue)
					matchedCount++;
			QRCodeTrace.Format("Matched Finders count: {0}", matchedCount);
			QRCodeTrace.Write("Remove all unused finders");
#endif

			// remove unused finders
			if (!RemoveUnusedFinders()) return null;

#if DEBUG
			QRCodeTrace.Format("Time: {0}", Environment.TickCount - start);
			foreach (var hf in FinderList) QRCodeTrace.Write(hf.ToString());
			QRCodeTrace.Write("Search for QR corners");
#endif
		}

#if DEBUG
		catch (Exception ex)
		{
			QRCodeTrace.Write("QR Code decoding failed (no finders). " + ex.Message);
			return null;
		}
#else
			catch
				{
				return null;
				}
#endif

		// look for all possible 3 finder patterns
		var index1End = FinderList.Count - 2;
		var index2End = FinderList.Count - 1;
		var index3End = FinderList.Count;
		for (var index1 = 0; index1 < index1End; index1++)
		for (var index2 = index1 + 1; index2 < index2End; index2++)
		for (var index3 = index2 + 1; index3 < index3End; index3++)
		{
			try
			{
				// find 3 finders arranged in L shape
				var corner = QRCodeCorner.CreateCorner(FinderList[index1], FinderList[index2], FinderList[index3]);

				// not a valid corner
				if (corner == null) continue;

#if DEBUG
				QRCodeTrace.Format("Decode Corner: Top Left:    {0}", corner.TopLeftFinder.ToString());
				QRCodeTrace.Format("Decode Corner: Top Right:   {0}", corner.TopRightFinder.ToString());
				QRCodeTrace.Format("Decode Corner: Bottom Left: {0}", corner.BottomLeftFinder.ToString());
#endif

				// get corner info (version, error code and mask)
				// continue if failed
				if (!GetQRCodeCornerInfo(corner)) continue;

#if DEBUG
				QRCodeTrace.Write("Decode QR code using three finders");
#endif

				// decode corner using three finders
				// continue if successful
				if (DecodeQRCodeCorner(corner)) continue;

				// qr code version 1 has no alignment mark
				// in other words decode failed 
				if (QRCodeVersion == 1) continue;

				// find bottom right alignment mark
				// continue if failed
				if (!FindAlignmentMark(corner)) continue;

				// decode using 4 points
				foreach (var align in AlignList)
				{
#if DEBUG
					QRCodeTrace.Format("Calculated alignment mark: Row {0}, Col {1}", align.Row, align.Col);
#endif

					// calculate transformation based on 3 finders and bottom right alignment mark
					SetTransMatrix(corner, align.Row, align.Col);

					// decode corner using three finders and one alignment mark
					if (DecodeQRCodeCorner(corner)) break;
				}
			}

#if DEBUG
			catch (Exception ex)
			{
				QRCodeTrace.Write("Decode corner failed. " + ex.Message);
			}
#else
						catch
							{
							continue;
							}
#endif
		}

#if DEBUG
		QRCodeTrace.Format("Time: {0}", Environment.TickCount - start);
#endif

		// not found exit
		if (DataArrayList.Count == 0)
		{
#if DEBUG
			QRCodeTrace.Write("No QR Code found");
#endif
			return null;
		}

		// successful exit
		return DataArrayList.ToArray();
	}

	////////////////////////////////////////////////////////////////////
	// Convert image to black and white boolean matrix
	////////////////////////////////////////////////////////////////////

	internal bool ConvertImageToBlackAndWhite
	(
		SKBitmap inputImage
	)
	{
	    // address of first line
	    var bitArrayPtr = inputImage.GetPixels();
	    
	    // length in bytes of one scan line
	    var scanLineWidth = inputImage.RowBytes;
	    if (scanLineWidth < 0)
	    {
	    #if DEBUG
	        QRCodeTrace.Write("Convert image to black and white array. Invalid input image format (upside down).");
	    #endif
	        return false;
	    }

	    // image total bytes
	    var totalBytes = scanLineWidth * ImageHeight;
	    var bitmapArray = new byte[totalBytes];
	    
	    // Copy the RGB values into the array.
	    Marshal.Copy(bitArrayPtr, bitmapArray, 0, totalBytes);

#if DEBUGRGB
		BinaryWriter BW = new BinaryWriter(new FileStream("TestImage.rgb", FileMode.Create));
		int ImageWidthUp = 3 * 32 * ((ImageWidth + 31) / 32);
		byte[] ImageBuf = new byte[ImageWidthUp];
#endif
	    
	    // allocate gray image 
	    var grayImage = new byte[ImageHeight, ImageWidth];
	    var grayLevel = new int[256];

	    // convert to gray
	    var delta = scanLineWidth - 3 * ImageWidth;
	    var bitmapPtr = 0;
	    for (var row = 0; row < ImageHeight; row++)
	    {
#if DEBUGRGB
				Array.Copy(BitmapArray, BitmapPtr, ImageBuf, 0, 3 * ImageWidth);
				BW.Write(ImageBuf);
#endif

	        for (var col = 0; col < ImageWidth; col++)
	        {
		        var module = (30 * bitmapArray[bitmapPtr] + 59 * bitmapArray[bitmapPtr + 1] +
		                      11 * bitmapArray[bitmapPtr + 2]) / 100;
	            grayLevel[module]++;
	            grayImage[row, col] = (byte)module;
	            bitmapPtr += 3;
	        }
	        
	        bitmapPtr += delta;
	    }

#if DEBUGRGB
				BW.Close();
#endif

		// gray level cutoff between black and white
		int levelStart;
		int levelEnd;
		for (levelStart = 0; levelStart < 256 && grayLevel[levelStart] == 0; levelStart++)
			;
		for (levelEnd = 255; levelEnd >= levelStart && grayLevel[levelEnd] == 0; levelEnd--)
			;
	    levelEnd++;
	    if (levelEnd - levelStart < 2)
	    {
	    #if DEBUG
	        QRCodeTrace.Write("Convert image to black and white array. Input image has no color variations");
	    #endif
	        return false;
	    }

	    var cutoffLevel = (levelStart + levelEnd) / 2;

	    // create boolean image white = false, black = true
	    BlackWhiteImage = new bool[ImageHeight, ImageWidth];
	    for (var row = 0; row < ImageHeight; row++)
	    for (var col = 0; col < ImageWidth; col++)
			BlackWhiteImage[row, col] = grayImage[row, col] < cutoffLevel;

		// save as black white image
#if DEBUGBWIMAGE
			QRCodeTrace.Write("Display black and white image");
			DisplayBlackAndWhiteImage();
#endif

		// exit;
	    return true;
	}

	////////////////////////////////////////////////////////////////////
	// Save and display black and white boolean image as png image
	////////////////////////////////////////////////////////////////////

#if DEBUGX
		internal void DisplayBlackAndWhiteImage()
			{
			int ModuleSize = Math.Min(16384 / Math.Max(ImageHeight, ImageWidth), 1);
			SolidBrush BrushWhite = new SolidBrush(Color.White);
			SolidBrush BrushBlack = new SolidBrush(Color.Black);
			Bitmap Image = new Bitmap(ImageWidth * ModuleSize, ImageHeight * ModuleSize);
			Graphics Graphics = Graphics.FromImage(Image);
			Graphics.FillRectangle(BrushWhite, 0, 0, ImageWidth * ModuleSize, ImageHeight * ModuleSize);
			for(int Row = 0; Row < ImageHeight; Row++) for(int Col = 0; Col < ImageWidth; Col++)
				{
				if(BlackWhiteImage[Row, Col]) Graphics.FillRectangle(BrushBlack, Col * ModuleSize, Row * ModuleSize, ModuleSize, ModuleSize);
				}
			string FileName = "DecodeImage.png";
			try
				{
				FileStream fs = new FileStream(FileName, FileMode.Create);
				Image.Save(fs, ImageFormat.Png);
				fs.Close();
				}
			catch(IOException)
				{
				FileName = null;
				}

			// start image editor
			if(FileName != null) Process.Start(FileName);
			return;
			}
#endif

	////////////////////////////////////////////////////////////////////
	// search row by row for finders blocks
	////////////////////////////////////////////////////////////////////
	internal bool HorizontalFindersSearch()
	{
		// create empty finders list
		FinderList = new List<QRCodeFinder>();

		// look for finder patterns
		var colPos = new int[ImageWidth + 1];
		var posPtr = 0;

		// scan one row at a time
		for (var row = 0; row < ImageHeight; row++)
		{
			// look for first black pixel
			int col;
			for (col = 0; col < ImageWidth && !BlackWhiteImage[row, col]; col++) ;
			if (col == ImageWidth) continue;

			// first black
			posPtr = 0;
			colPos[posPtr++] = col;

			// loop for pairs
			for (;;)
			{
				// look for next white
				// if black is all the way to the edge, set next white after the edge
				for (; col < ImageWidth && BlackWhiteImage[row, col]; col++) ;
				colPos[posPtr++] = col;
				if (col == ImageWidth) break;

				// look for next black
				for (; col < ImageWidth && !BlackWhiteImage[row, col]; col++) ;
				if (col == ImageWidth) break;
				colPos[posPtr++] = col;
			}

			// we must have at least 6 positions
			if (posPtr < 6) continue;

			// build length array
			var posLen = posPtr - 1;
			var len = new int[posLen];
			for (var ptr = 0; ptr < posLen; ptr++) len[ptr] = colPos[ptr + 1] - colPos[ptr];

			// test signature
			var sigLen = posPtr - 5;
			for (var sigPtr = 0; sigPtr < sigLen; sigPtr += 2)
				if (TestFinderSig(colPos, len, sigPtr, out var moduleSize))
					FinderList.Add(new QRCodeFinder(row, colPos[sigPtr + 2], colPos[sigPtr + 3], moduleSize));
		}

		// no finders found
		if (FinderList.Count < 3)
		{
#if DEBUG
			QRCodeTrace.Write("Horizontal finders search. Less than 3 finders found");
#endif
			return false;
		}

		// exit
		return true;
	}

	////////////////////////////////////////////////////////////////////
	// search row by row for alignment blocks
	////////////////////////////////////////////////////////////////////

	internal bool HorizontalAlignmentSearch
	(
		int areaLeft,
		int areaTop,
		int areaWidth,
		int areaHeight
	)
	{
		// create empty finders list
		AlignList = new List<QRCodeFinder>();

		// look for finder patterns
		var colPos = new int[areaWidth + 1];
		var posPtr = 0;

		// area right and bottom
		var areaRight = areaLeft + areaWidth;
		var areaBottom = areaTop + areaHeight;

		// scan one row at a time
		for (var row = areaTop; row < areaBottom; row++)
		{
			// look for first black pixel
			int col;
			for (col = areaLeft; col < areaRight && !BlackWhiteImage[row, col]; col++) ;
			if (col == areaRight) continue;

			// first black
			posPtr = 0;
			colPos[posPtr++] = col;

			// loop for pairs
			for (;;)
			{
				// look for next white
				// if black is all the way to the edge, set next white after the edge
				for (; col < areaRight && BlackWhiteImage[row, col]; col++) ;
				colPos[posPtr++] = col;
				if (col == areaRight) break;

				// look for next black
				for (; col < areaRight && !BlackWhiteImage[row, col]; col++) ;
				if (col == areaRight) break;
				colPos[posPtr++] = col;
			}

			// we must have at least 6 positions
			if (posPtr < 6) continue;

			// build length array
			var posLen = posPtr - 1;
			var len = new int[posLen];
			for (var ptr = 0; ptr < posLen; ptr++)
				len[ptr] = colPos[ptr + 1] - colPos[ptr];

			// test signature
			var sigLen = posPtr - 5;
			for (var sigPtr = 0; sigPtr < sigLen; sigPtr += 2)
				if (TestAlignSig(colPos, len, sigPtr, out var moduleSize))
					AlignList.Add(new QRCodeFinder(row, colPos[sigPtr + 2], colPos[sigPtr + 3], moduleSize));
		}

		// list is now empty or has less than three finders
#if DEBUG
		if (AlignList.Count == 0)
			QRCodeTrace.Write("Vertical align search.\r\nNo finders found");
#endif

		// exit
		return AlignList.Count != 0;
	}

	////////////////////////////////////////////////////////////////////
	// search column by column for finders blocks
	////////////////////////////////////////////////////////////////////

	internal void VerticalFindersSearch()
	{
		// active columns
		var activeColumn = new bool[ImageWidth];
		foreach (var hf in FinderList)
			for (var col = hf.Col1; col < hf.Col2; col++)
				activeColumn[col] = true;

		// look for finder patterns
		var rowPos = new int[ImageHeight + 1];

		// scan one column at a time
		for (var col = 0; col < ImageWidth; col++)
		{
			// not active column
			if (!activeColumn[col]) continue;

			// look for first black pixel
			int row;
			for (row = 0; row < ImageHeight && !BlackWhiteImage[row, col]; row++) ;
			if (row == ImageWidth) continue;

			// first black
			var posPtr = 0;
			rowPos[posPtr++] = row;

			// loop for pairs
			for (;;)
			{
				// look for next white
				// if black is all the way to the edge, set next white after the edge
				for (; row < ImageHeight && BlackWhiteImage[row, col]; row++) ;
				rowPos[posPtr++] = row;
				if (row == ImageHeight) break;

				// look for next black
				for (; row < ImageHeight && !BlackWhiteImage[row, col]; row++) ;
				if (row == ImageHeight) break;
				rowPos[posPtr++] = row;
			}

			// we must have at least 6 positions
			if (posPtr < 6) continue;

			// build length array
			var posLen = posPtr - 1;
			var len = new int[posLen];
			for (var ptr = 0; ptr < posLen; ptr++) len[ptr] = rowPos[ptr + 1] - rowPos[ptr];

			// test signature
			var sigLen = posPtr - 5;
			for (var sigPtr = 0; sigPtr < sigLen; sigPtr += 2)
			{
				if (!TestFinderSig(rowPos, len, sigPtr, out var moduleSize)) continue;
				foreach (var hf in FinderList) hf.Match(col, rowPos[sigPtr + 2], rowPos[sigPtr + 3], moduleSize);
			}
		}

		// exit
	}

	////////////////////////////////////////////////////////////////////
	// search column by column for finders blocks
	////////////////////////////////////////////////////////////////////
	internal void VerticalAlignmentSearch
	(
		int areaLeft,
		int areaTop,
		int areaWidth,
		int areaHeight
	)
	{
		// active columns
		var activeColumn = new bool[areaWidth];
		foreach (var hf in AlignList)
			for (var col = hf.Col1; col < hf.Col2; col++)
				activeColumn[col - areaLeft] = true;

		// look for finder patterns
		var rowPos = new int[areaHeight + 1];
		int posPtr;

		// area right and bottom
		var areaRight = areaLeft + areaWidth;
		var areaBottom = areaTop + areaHeight;

		// scan one column at a time
		for (var col = areaLeft; col < areaRight; col++)
		{
			// not active column
			if (!activeColumn[col - areaLeft]) continue;

			// look for first black pixel
			int row;
			for (row = areaTop; row < areaBottom && !BlackWhiteImage[row, col]; row++) ;
			if (row == areaBottom) continue;

			// first black
			posPtr = 0;
			rowPos[posPtr++] = row;

			// loop for pairs
			for (;;)
			{
				// look for next white
				// if black is all the way to the edge, set next white after the edge
				for (; row < areaBottom && BlackWhiteImage[row, col]; row++) ;
				rowPos[posPtr++] = row;
				if (row == areaBottom) break;

				// look for next black
				for (; row < areaBottom && !BlackWhiteImage[row, col]; row++) ;
				if (row == areaBottom) break;
				rowPos[posPtr++] = row;
			}

			// we must have at least 6 positions
			if (posPtr < 6) continue;

			// build length array
			var posLen = posPtr - 1;
			var len = new int[posLen];
			for (var ptr = 0; ptr < posLen; ptr++) len[ptr] = rowPos[ptr + 1] - rowPos[ptr];

			// test signature
			var sigLen = posPtr - 5;
			for (var sigPtr = 0; sigPtr < sigLen; sigPtr += 2)
			{
				if (!TestAlignSig(rowPos, len, sigPtr, out var moduleSize)) continue;
				foreach (var hf in AlignList) hf.Match(col, rowPos[sigPtr + 2], rowPos[sigPtr + 3], moduleSize);
			}
		}

		// exit
	}

	////////////////////////////////////////////////////////////////////
	// search column by column for finders blocks
	////////////////////////////////////////////////////////////////////
	internal bool RemoveUnusedFinders()
	{
		// remove all entries without a match
		for (var index = 0; index < FinderList.Count; index++)
			if (FinderList[index].Distance == double.MaxValue)
			{
				FinderList.RemoveAt(index);
				index--;
			}

		// list is now empty or has less than three finders
		if (FinderList.Count < 3)
		{
#if DEBUG
			QRCodeTrace.Write("Remove unmatched finders. Less than 3 finders found");
#endif
			return false;
		}

		// keep best entry for each overlapping area
		for (var index = 0; index < FinderList.Count; index++)
		{
			var finder = FinderList[index];
			for (var index1 = index + 1; index1 < FinderList.Count; index1++)
			{
				var finder1 = FinderList[index1];
				if (!finder.Overlap(finder1)) continue;
				if (finder1.Distance < finder.Distance)
				{
					finder = finder1;
					FinderList[index] = finder;
				}

				FinderList.RemoveAt(index1);
				index1--;
			}
		}

		// list is now empty or has less than three finders
		if (FinderList.Count < 3)
		{
#if DEBUG
			QRCodeTrace.Write("Keep best matched finders. Less than 3 finders found");
#endif
			return false;
		}

		// exit
		return true;
	}

	////////////////////////////////////////////////////////////////////
	// search column by column for finders blocks
	////////////////////////////////////////////////////////////////////

	internal bool RemoveUnusedAlignMarks()
	{
		// remove all entries without a match
		for (var index = 0; index < AlignList.Count; index++)
			if (AlignList[index].Distance == double.MaxValue)
			{
				AlignList.RemoveAt(index);
				index--;
			}

		// keep best entry for each overlapping area
		for (var index = 0; index < AlignList.Count; index++)
		{
			var finder = AlignList[index];
			for (var index1 = index + 1; index1 < AlignList.Count; index1++)
			{
				var finder1 = AlignList[index1];
				if (!finder.Overlap(finder1)) continue;
				if (finder1.Distance < finder.Distance)
				{
					finder = finder1;
					AlignList[index] = finder;
				}

				AlignList.RemoveAt(index1);
				index1--;
			}
		}

		// list is now empty or has less than three finders
#if DEBUG
		if (AlignList.Count == 0)
			QRCodeTrace.Write("Remove unused alignment marks.\r\nNo alignment marks found");
#endif

		// exit
		return AlignList.Count != 0;
	}

	////////////////////////////////////////////////////////////////////
	// test finder signature 1 1 3 1 1
	////////////////////////////////////////////////////////////////////
	internal static bool TestFinderSig
	(
		int[] pos,
		int[] len,
		int index,
		out double module
	)
	{
		module = (pos[index + 5] - pos[index]) / 7.0;
		var maxDev = SignatureMaxDeviation * module;
		if (Math.Abs(len[index] - module) > maxDev) return false;
		if (Math.Abs(len[index + 1] - module) > maxDev) return false;
		if (Math.Abs(len[index + 2] - 3 * module) > maxDev) return false;
		if (Math.Abs(len[index + 3] - module) > maxDev) return false;
		if (Math.Abs(len[index + 4] - module) > maxDev) return false;
		return true;
	}

	////////////////////////////////////////////////////////////////////
	// test alignment signature n 1 1 1 n
	////////////////////////////////////////////////////////////////////
	internal static bool TestAlignSig
	(
		int[] pos,
		int[] len,
		int index,
		out double module
	)
	{
		module = (pos[index + 4] - pos[index + 1]) / 3.0;
		var maxDev = SignatureMaxDeviation * module;
		if (len[index] < module - maxDev) return false;
		if (Math.Abs(len[index + 1] - module) > maxDev) return false;
		if (Math.Abs(len[index + 2] - module) > maxDev) return false;
		if (Math.Abs(len[index + 3] - module) > maxDev) return false;
		if (len[index + 4] < module - maxDev) return false;
		return true;
	}

	////////////////////////////////////////////////////////////////////
	// Build corner list
	////////////////////////////////////////////////////////////////////

	internal List<QRCodeCorner> BuildCornerList()
	{
		// empty list
		List<QRCodeCorner> corners = new();

		// look for all possible 3 finder patterns
		var index1End = FinderList.Count - 2;
		var index2End = FinderList.Count - 1;
		var index3End = FinderList.Count;
		for (var index1 = 0; index1 < index1End; index1++)
		for (var index2 = index1 + 1; index2 < index2End; index2++)
		for (var index3 = index2 + 1; index3 < index3End; index3++)
		{
			// find 3 finders arranged in L shape
			var corner = QRCodeCorner.CreateCorner(FinderList[index1], FinderList[index2], FinderList[index3]);

			// add corner to list
			if (corner != null) corners.Add(corner);
		}

		// exit
		return corners.Count == 0 ? null : corners;
	}

	////////////////////////////////////////////////////////////////////
	// Get QR Code corner info
	////////////////////////////////////////////////////////////////////

	internal bool GetQRCodeCornerInfo
	(
		QRCodeCorner corner
	)
	{
		try
		{
			// initial version number
			QRCodeVersion = corner.InitialVersionNumber();

			// qr code dimension
			QRCodeDimension = 17 + 4 * QRCodeVersion;

#if DEBUG
			QRCodeTrace.Format("Initial version number: {0}, dimension: {1}", QRCodeVersion, QRCodeDimension);
#endif

			// set transformation matrix
			SetTransMatrix(corner);

			// if version number is 7 or more, get version code
			if (QRCodeVersion >= 7)
			{
				var version = GetVersionOne();
				if (version == 0)
				{
					version = GetVersionTwo();
					if (version == 0) return false;
				}

				// QR Code version number is different than initial version
				if (version != QRCodeVersion)
				{
					// initial version number and dimension
					QRCodeVersion = version;

					// qr code dimension
					QRCodeDimension = 17 + 4 * QRCodeVersion;

#if DEBUG
					QRCodeTrace.Format("Updated version number: {0}, dimension: {1}", QRCodeVersion, QRCodeDimension);
#endif

					// set transformation matrix
					SetTransMatrix(corner);
				}
			}

			// get format info arrays
			var formatInfo = GetFormatInfoOne();
			if (formatInfo < 0)
			{
				formatInfo = GetFormatInfoTwo();
				if (formatInfo < 0) return false;
			}

			// set error correction code and mask code
			ErrorCorrection = FormatInfoToErrCode(formatInfo >> 3);
			MaskCode = formatInfo & 7;

			// successful exit
			return true;
		}

#if DEBUG
		catch (Exception ex)
		{
			QRCodeTrace.Format("Get QR Code corner info exception.\r\n{0}", ex.Message);

			// failed exit
			return false;
		}
#else
			catch
				{
				// failed exit
				return false;
				}
#endif
	}

	////////////////////////////////////////////////////////////////////
	// Search for QR Code version
	////////////////////////////////////////////////////////////////////

	internal bool DecodeQRCodeCorner
	(
		QRCodeCorner corner
	)
	{
		try
		{
			// create base matrix
			BuildBaseMatrix();

			// create data matrix and test fixed modules
			ConvertImageToMatrix();

			// based on version and format information
			// set number of data and error correction codewords length  
			SetDataCodewordsLength();

			// apply mask as per get format information step
			ApplyMask(MaskCode);

			// unload data from binary matrix to byte format
			UnloadDataFromMatrix();

			// restore blocks (undo interleave)
			RestoreBlocks();

			// calculate error correction
			// in case of error try to correct it
			CalculateErrorCorrection();

			// decode data
			var dataArray = DecodeData();

			// create result class
			QRCodeResult codeResult = new(dataArray)
			{
				EciAssignValue = EciAssignValue,
				QRCodeVersion = QRCodeVersion,
				QRCodeDimension = QRCodeDimension,
				ErrorCorrection = ErrorCorrection
			};

			// add result to the list
			DataArrayList.Add(codeResult);

#if DEBUG
			// trace
			var dataString = ByteArrayToStr(dataArray);
			QRCodeTrace.Format(
				"Version: {0}, Dim: {1}, ErrCorr: {2}, Generator: {3}, Mask: {4}, Group1: {5}:{6}, Group2: {7}:{8}",
				QRCodeVersion.ToString(), QRCodeDimension.ToString(), ErrorCorrection.ToString(),
				ErrCorrCodewords.ToString(), MaskCode.ToString(),
				BlocksGroup1.ToString(), DataCodewordsGroup1.ToString(), BlocksGroup2.ToString(),
				DataCodewordsGroup2.ToString());
			QRCodeTrace.Format("Data: {0}", dataString);
#endif

			// successful exit
			return true;
		}

#if DEBUG
		catch (Exception ex)
		{
			QRCodeTrace.Format("Decode QR code exception.\r\n{0}", ex.Message);

			// failed exit
			return false;
		}
#else
			catch
				{
				// failed exit
				return false;
				}
#endif
	}

	internal void SetTransMatrix
	(
		QRCodeCorner corner
	)
	{
		// save
		var bottomRightPos = QRCodeDimension - 4;

		// transformation matrix based on three finders
		var matrix1 = new double[3, 4];
		var matrix2 = new double[3, 4];

		// build matrix 1 for horizontal X direction
		matrix1[0, 0] = 3;
		matrix1[0, 1] = 3;
		matrix1[0, 2] = 1;
		matrix1[0, 3] = corner.TopLeftFinder.Col;

		matrix1[1, 0] = bottomRightPos;
		matrix1[1, 1] = 3;
		matrix1[1, 2] = 1;
		matrix1[1, 3] = corner.TopRightFinder.Col;

		matrix1[2, 0] = 3;
		matrix1[2, 1] = bottomRightPos;
		matrix1[2, 2] = 1;
		matrix1[2, 3] = corner.BottomLeftFinder.Col;

		// build matrix 2 for Vertical Y direction
		matrix2[0, 0] = 3;
		matrix2[0, 1] = 3;
		matrix2[0, 2] = 1;
		matrix2[0, 3] = corner.TopLeftFinder.Row;

		matrix2[1, 0] = bottomRightPos;
		matrix2[1, 1] = 3;
		matrix2[1, 2] = 1;
		matrix2[1, 3] = corner.TopRightFinder.Row;

		matrix2[2, 0] = 3;
		matrix2[2, 1] = bottomRightPos;
		matrix2[2, 2] = 1;
		matrix2[2, 3] = corner.BottomLeftFinder.Row;

		// solve matrix1
		SolveMatrixOne(matrix1);
		Trans3A = matrix1[0, 3];
		Trans3C = matrix1[1, 3];
		Trans3E = matrix1[2, 3];

		// solve matrix2
		SolveMatrixOne(matrix2);
		Trans3B = matrix2[0, 3];
		Trans3d = matrix2[1, 3];
		Trans3F = matrix2[2, 3];

		// reset trans 4 mode
		Trans4Mode = false;
	}

	internal static void SolveMatrixOne
	(
		double[,] matrix
	)
	{
		for (var row = 0; row < 3; row++)
		{
			// If the element is zero, make it non zero by adding another row
			if (matrix[row, row] == 0)
			{
				int row1;
				for (row1 = row + 1; row1 < 3 && matrix[row1, row] == 0; row1++) ;
				if (row1 == 3) throw new ApplicationException("Solve linear equations failed");

				for (var col = row; col < 4; col++) matrix[row, col] += matrix[row1, col];
			}

			// make the diagonal element 1.0
			for (var col = 3; col > row; col--) matrix[row, col] /= matrix[row, row];

			// subtract current row from next rows to eliminate one value
			for (var row1 = row + 1; row1 < 3; row1++)
			for (var col = 3; col > row; col--)
				matrix[row1, col] -= matrix[row, col] * matrix[row1, row];
		}

		// go up from last row and eliminate all solved values
		matrix[1, 3] -= matrix[1, 2] * matrix[2, 3];
		matrix[0, 3] -= matrix[0, 2] * matrix[2, 3];
		matrix[0, 3] -= matrix[0, 1] * matrix[1, 3];
	}

	////////////////////////////////////////////////////////////////////
	// Get image pixel color
	////////////////////////////////////////////////////////////////////
	internal bool GetModule
	(
		int row,
		int col
	)
	{
		// get module based on three finders
		if (!Trans4Mode)
		{
			var trans3Col = (int)Math.Round(Trans3A * col + Trans3C * row + Trans3E, 0, MidpointRounding.AwayFromZero);
			var trans3Row = (int)Math.Round(Trans3B * col + Trans3d * row + Trans3F, 0, MidpointRounding.AwayFromZero);
			return BlackWhiteImage[trans3Row, trans3Col];
		}

		// get module based on three finders plus one alignment mark
		var w = Trans4G * col + Trans4H * row + 1.0;
		var trans4Col =
			(int)Math.Round((Trans4A * col + Trans4B * row + Trans4C) / w, 0, MidpointRounding.AwayFromZero);
		var trans4Row =
			(int)Math.Round((Trans4d * col + Trans4E * row + Trans4F) / w, 0, MidpointRounding.AwayFromZero);
		return BlackWhiteImage[trans4Row, trans4Col];
	}

	////////////////////////////////////////////////////////////////////
	// search row by row for finders blocks
	////////////////////////////////////////////////////////////////////

	internal bool FindAlignmentMark
	(
		QRCodeCorner corner
	)
	{
		// alignment mark estimated position
		var alignRow = QRCodeDimension - 7;
		var alignCol = QRCodeDimension - 7;
		var imageCol = (int)Math.Round(Trans3A * alignCol + Trans3C * alignRow + Trans3E, 0,
			MidpointRounding.AwayFromZero);
		var imageRow = (int)Math.Round(Trans3B * alignCol + Trans3d * alignRow + Trans3F, 0,
			MidpointRounding.AwayFromZero);

#if DEBUG
		QRCodeTrace.Format("Estimated alignment mark: Row {0}, Col {1}", imageRow, imageCol);
#endif

		// search area
		var side = (int)Math.Round(AlignmentSearchArea * (corner.TopLineLength + corner.LeftLineLength), 0,
			MidpointRounding.AwayFromZero);

		var areaLeft = imageCol - side / 2;
		var areaTop = imageRow - side / 2;
		var areaWidth = side;
		var areaHeight = side;

#if DEBUGBRCORNER
			DisplayBottomRightCorder(AreaLeft, AreaTop, AreaWidth, AreaHeight);
#endif

		// horizontal search for finders
		if (!HorizontalAlignmentSearch(areaLeft, areaTop, areaWidth, areaHeight)) return false;

		// vertical search for finders
		VerticalAlignmentSearch(areaLeft, areaTop, areaWidth, areaHeight);

		// remove unused alignment entries
		if (!RemoveUnusedAlignMarks()) return false;

		// successful exit
		return true;
	}

	internal void SetTransMatrix
	(
		QRCodeCorner corner,
		double imageAlignRow,
		double imageAlignCol
	)
	{
		// top right and bottom left QR code position
		var farFinder = QRCodeDimension - 4;
		var farAlign = QRCodeDimension - 7;

		var matrix = new double[8, 9];

		matrix[0, 0] = 3.0;
		matrix[0, 1] = 3.0;
		matrix[0, 2] = 1.0;
		matrix[0, 6] = -3.0 * corner.TopLeftFinder.Col;
		matrix[0, 7] = -3.0 * corner.TopLeftFinder.Col;
		matrix[0, 8] = corner.TopLeftFinder.Col;

		matrix[1, 0] = farFinder;
		matrix[1, 1] = 3.0;
		matrix[1, 2] = 1.0;
		matrix[1, 6] = -farFinder * corner.TopRightFinder.Col;
		matrix[1, 7] = -3.0 * corner.TopRightFinder.Col;
		matrix[1, 8] = corner.TopRightFinder.Col;

		matrix[2, 0] = 3.0;
		matrix[2, 1] = farFinder;
		matrix[2, 2] = 1.0;
		matrix[2, 6] = -3.0 * corner.BottomLeftFinder.Col;
		matrix[2, 7] = -farFinder * corner.BottomLeftFinder.Col;
		matrix[2, 8] = corner.BottomLeftFinder.Col;

		matrix[3, 0] = farAlign;
		matrix[3, 1] = farAlign;
		matrix[3, 2] = 1.0;
		matrix[3, 6] = -farAlign * imageAlignCol;
		matrix[3, 7] = -farAlign * imageAlignCol;
		matrix[3, 8] = imageAlignCol;

		matrix[4, 3] = 3.0;
		matrix[4, 4] = 3.0;
		matrix[4, 5] = 1.0;
		matrix[4, 6] = -3.0 * corner.TopLeftFinder.Row;
		matrix[4, 7] = -3.0 * corner.TopLeftFinder.Row;
		matrix[4, 8] = corner.TopLeftFinder.Row;

		matrix[5, 3] = farFinder;
		matrix[5, 4] = 3.0;
		matrix[5, 5] = 1.0;
		matrix[5, 6] = -farFinder * corner.TopRightFinder.Row;
		matrix[5, 7] = -3.0 * corner.TopRightFinder.Row;
		matrix[5, 8] = corner.TopRightFinder.Row;

		matrix[6, 3] = 3.0;
		matrix[6, 4] = farFinder;
		matrix[6, 5] = 1.0;
		matrix[6, 6] = -3.0 * corner.BottomLeftFinder.Row;
		matrix[6, 7] = -farFinder * corner.BottomLeftFinder.Row;
		matrix[6, 8] = corner.BottomLeftFinder.Row;

		matrix[7, 3] = farAlign;
		matrix[7, 4] = farAlign;
		matrix[7, 5] = 1.0;
		matrix[7, 6] = -farAlign * imageAlignRow;
		matrix[7, 7] = -farAlign * imageAlignRow;
		matrix[7, 8] = imageAlignRow;

		for (var row = 0; row < 8; row++)
		{
			// If the element is zero, make it non zero by adding another row
			if (matrix[row, row] == 0)
			{
				int row1;
				for (row1 = row + 1; row1 < 8 && matrix[row1, row] == 0; row1++) ;
				if (row1 == 8) throw new ApplicationException("Solve linear equations failed");

				for (var col = row; col < 9; col++) matrix[row, col] += matrix[row1, col];
			}

			// make the diagonal element 1.0
			for (var col = 8; col > row; col--) matrix[row, col] /= matrix[row, row];

			// subtract current row from next rows to eliminate one value
			for (var row1 = row + 1; row1 < 8; row1++)
			for (var col = 8; col > row; col--)
				matrix[row1, col] -= matrix[row, col] * matrix[row1, row];
		}

		// go up from last row and eliminate all solved values
		for (var col = 7; col > 0; col--)
		for (var row = col - 1; row >= 0; row--)
			matrix[row, 8] -= matrix[row, col] * matrix[col, 8];

		Trans4A = matrix[0, 8];
		Trans4B = matrix[1, 8];
		Trans4C = matrix[2, 8];
		Trans4d = matrix[3, 8];
		Trans4E = matrix[4, 8];
		Trans4F = matrix[5, 8];
		Trans4G = matrix[6, 8];
		Trans4H = matrix[7, 8];

		// set trans 4 mode
		Trans4Mode = true;
	}

	////////////////////////////////////////////////////////////////////
	// Get version code bits top right
	////////////////////////////////////////////////////////////////////

	internal int GetVersionOne()
	{
		var versionCode = 0;
		for (var index = 0; index < 18; index++)
			if (GetModule(index / 3, QRCodeDimension - 11 + index % 3))
				versionCode |= 1 << index;
		return TestVersionCode(versionCode);
	}

	////////////////////////////////////////////////////////////////////
	// Get version code bits bottom left
	////////////////////////////////////////////////////////////////////

	internal int GetVersionTwo()
	{
		var versionCode = 0;
		for (var index = 0; index < 18; index++)
			if (GetModule(QRCodeDimension - 11 + index % 3, index / 3))
				versionCode |= 1 << index;
		return TestVersionCode(versionCode);
	}

	////////////////////////////////////////////////////////////////////
	// Test version code bits
	////////////////////////////////////////////////////////////////////

	internal static int TestVersionCode
	(
		int versionCode
	)
	{
		// format info
		var code = versionCode >> 12;

		// test for exact match
		if (code >= 7 && code <= 40 && VersionCodeArray[code - 7] == versionCode)
		{
#if DEBUG
			QRCodeTrace.Format("Version code exact match: {0:X4}, Version: {1}", versionCode, code);
#endif
			return code;
		}

		// look for a match
		var bestInfo = 0;
		var error = int.MaxValue;
		for (var index = 0; index < 34; index++)
		{
			// test for exact match
			var errorBits = VersionCodeArray[index] ^ versionCode;
			if (errorBits == 0) return versionCode >> 12;

			// count errors
			var errorCount = CountBits(errorBits);

			// save best result
			if (errorCount < error)
			{
				error = errorCount;
				bestInfo = index;
			}
		}

#if DEBUG
		if (error <= 3)
			QRCodeTrace.Format("Version code match with errors: {0:X4}, Version: {1}, Errors: {2}",
				versionCode, bestInfo + 7, error);
		else
			QRCodeTrace.Format("Version code no match: {0:X4}", versionCode);
#endif

		return error <= 3 ? bestInfo + 7 : 0;
	}

	////////////////////////////////////////////////////////////////////
	// Get format info around top left corner
	////////////////////////////////////////////////////////////////////

	public int GetFormatInfoOne()
	{
		var info = 0;
		for (var index = 0; index < 15; index++)
			if (GetModule(FormatInfoOne[index, 0], FormatInfoOne[index, 1]))
				info |= 1 << index;
		return TestFormatInfo(info);
	}

	////////////////////////////////////////////////////////////////////
	// Get format info around top right and bottom left corners
	////////////////////////////////////////////////////////////////////

	internal int GetFormatInfoTwo()
	{
		var info = 0;
		for (var index = 0; index < 15; index++)
		{
			var row = FormatInfoTwo[index, 0];
			if (row < 0) row += QRCodeDimension;
			var col = FormatInfoTwo[index, 1];
			if (col < 0) col += QRCodeDimension;
			if (GetModule(row, col)) info |= 1 << index;
		}

		return TestFormatInfo(info);
	}

	////////////////////////////////////////////////////////////////////
	// Test format info bits
	////////////////////////////////////////////////////////////////////

	internal static int TestFormatInfo
	(
		int formatInfo
	)
	{
		// format info
		var info = (formatInfo ^ 0x5412) >> 10;

		// test for exact match
		if (FormatInfoArray[info] == formatInfo)
		{
#if DEBUG
			QRCodeTrace.Format("Format info exact match: {0:X4}, EC: {1}, mask: {2}",
				formatInfo, FormatInfoToErrCode(info >> 3).ToString(), info & 7);
#endif
			return info;
		}

		// look for a match
		var bestInfo = 0;
		var error = int.MaxValue;
		for (var index = 0; index < 32; index++)
		{
			var errorCount = CountBits(FormatInfoArray[index] ^ formatInfo);
			if (errorCount < error)
			{
				error = errorCount;
				bestInfo = index;
			}
		}

#if DEBUG
		if (error <= 3)
			QRCodeTrace.Format("Format info match with errors: {0:X4}, EC: {1}, mask: {2}, errors: {3}",
				formatInfo, FormatInfoToErrCode(info >> 3).ToString(), info & 7, error);
		else
			QRCodeTrace.Format("Format info no match: {0:X4}", formatInfo);

#endif
		return error <= 3 ? bestInfo : -1;
	}

	////////////////////////////////////////////////////////////////////
	// Count Bits
	////////////////////////////////////////////////////////////////////

	internal static int CountBits
	(
		int value
	)
	{
		var count = 0;
		for (var mask = 0x4000; mask != 0; mask >>= 1)
			if ((value & mask) != 0)
				count++;
		return count;
	}

	////////////////////////////////////////////////////////////////////
	// Convert image to qr code matrix and test fixed modules
	////////////////////////////////////////////////////////////////////

	internal void ConvertImageToMatrix()
	{
		// loop for all modules
		var fixedCount = 0;
		var errorCount = 0;
		for (var row = 0; row < QRCodeDimension; row++)
		for (var col = 0; col < QRCodeDimension; col++)
			// the module (Row, Col) is not a fixed module 
			if ((BaseMatrix[row, col] & Fixed) == 0)
			{
				if (GetModule(row, col)) BaseMatrix[row, col] |= Black;
			}

			// fixed module
			else
			{
				// total fixed modules
				fixedCount++;

				// test for error
				if ((GetModule(row, col) ? Black : White) != (BaseMatrix[row, col] & 1)) errorCount++;
			}

#if DEBUG
		if (errorCount == 0)
			QRCodeTrace.Write("Fixed modules no error");
		else if (errorCount <= fixedCount * ErrCorrPercent[(int)ErrorCorrection] / 100)
			QRCodeTrace.Format("Fixed modules some errors: {0} / {1}", errorCount, fixedCount);
		else
			QRCodeTrace.Format("Fixed modules too many errors: {0} / {1}", errorCount, fixedCount);
#endif
		if (errorCount > fixedCount * ErrCorrPercent[(int)ErrorCorrection] / 100)
			throw new ApplicationException("Fixed modules error");
	}

	////////////////////////////////////////////////////////////////////
	// Unload matrix data from base matrix
	////////////////////////////////////////////////////////////////////

	internal void UnloadDataFromMatrix()
	{
		// input array pointer initialization
		var ptr = 0;
		var ptrEnd = 8 * MaxCodewords;
		CodewordsArray = new byte[MaxCodewords];

		// bottom right corner of output matrix
		var row = QRCodeDimension - 1;
		var col = QRCodeDimension - 1;

		// step state
		var state = 0;
		for (;;)
		{
			// current module is data
			if ((MaskMatrix[row, col] & NonData) == 0)
			{
				// unload current module with
				if ((MaskMatrix[row, col] & 1) != 0) CodewordsArray[ptr >> 3] |= (byte)(1 << (7 - (ptr & 7)));
				if (++ptr == ptrEnd) break;
			}

			// current module is non data and vertical timing line condition is on
			else if (col == 6)
			{
				col--;
			}

			// update matrix position to next module
			switch (state)
			{
				// going up: step one to the left
				case 0:
					col--;
					state = 1;
					continue;

				// going up: step one row up and one column to the right
				case 1:
					col++;
					row--;
					// we are not at the top, go to state 0
					if (row >= 0)
					{
						state = 0;
						continue;
					}

					// we are at the top, step two columns to the left and start going down
					col -= 2;
					row = 0;
					state = 2;
					continue;

				// going down: step one to the left
				case 2:
					col--;
					state = 3;
					continue;

				// going down: step one row down and one column to the right
				case 3:
					col++;
					row++;
					// we are not at the bottom, go to state 2
					if (row < QRCodeDimension)
					{
						state = 2;
						continue;
					}

					// we are at the bottom, step two columns to the left and start going up
					col -= 2;
					row = QRCodeDimension - 1;
					state = 0;
					continue;
			}
		}
	}

	////////////////////////////////////////////////////////////////////
	// Restore interleave data and error correction blocks
	////////////////////////////////////////////////////////////////////

	internal void RestoreBlocks()
	{
		// allocate temp codewords array
		var tempArray = new byte[MaxCodewords];

		// total blocks
		var totalBlocks = BlocksGroup1 + BlocksGroup2;

		// create array of data blocks starting point
		var start = new int[totalBlocks];
		for (var index = 1; index < totalBlocks; index++)
			start[index] = start[index - 1] + (index <= BlocksGroup1 ? DataCodewordsGroup1 : DataCodewordsGroup2);

		// step one. iterleave base on group one length
		var ptrEnd = DataCodewordsGroup1 * totalBlocks;

		// restore group one and two
		int ptr;
		var block = 0;
		for (ptr = 0; ptr < ptrEnd; ptr++)
		{
			tempArray[start[block]] = CodewordsArray[ptr];
			start[block]++;
			block++;
			if (block == totalBlocks) block = 0;
		}

		// restore group two
		if (DataCodewordsGroup2 > DataCodewordsGroup1)
		{
			// step one. iterleave base on group one length
			ptrEnd = MaxDataCodewords;

			block = BlocksGroup1;
			for (; ptr < ptrEnd; ptr++)
			{
				tempArray[start[block]] = CodewordsArray[ptr];
				start[block]++;
				block++;
				if (block == totalBlocks) block = BlocksGroup1;
			}
		}

		// create array of error correction blocks starting point
		start[0] = MaxDataCodewords;
		for (var index = 1; index < totalBlocks; index++)
			start[index] = start[index - 1] + ErrCorrCodewords;

		// restore all groups
		ptrEnd = MaxCodewords;
		block = 0;
		for (; ptr < ptrEnd; ptr++)
		{
			tempArray[start[block]] = CodewordsArray[ptr];
			start[block]++;
			block++;
			if (block == totalBlocks) block = 0;
		}

		// save result
		CodewordsArray = tempArray;
	}

	////////////////////////////////////////////////////////////////////
	// Calculate Error Correction
	////////////////////////////////////////////////////////////////////

	protected void CalculateErrorCorrection()
	{
		// total error count
		var totalErrorCount = 0;

		// set generator polynomial array
		var generator = GenArray[ErrCorrCodewords - 7];

		// error correcion calculation buffer
		var bufSize = Math.Max(DataCodewordsGroup1, DataCodewordsGroup2) + ErrCorrCodewords;
		var errCorrBuff = new byte[bufSize];

		// initial number of data codewords
		var dataCodewords = DataCodewordsGroup1;
		var buffLen = dataCodewords + ErrCorrCodewords;

		// codewords pointer
		var dataCodewordsPtr = 0;

		// codewords buffer error correction pointer
		var codewordsArrayErrCorrPtr = MaxDataCodewords;

		// loop one block at a time
		var totalBlocks = BlocksGroup1 + BlocksGroup2;
		for (var blockNumber = 0; blockNumber < totalBlocks; blockNumber++)
		{
			// switch to group2 data codewords
			if (blockNumber == BlocksGroup1)
			{
				dataCodewords = DataCodewordsGroup2;
				buffLen = dataCodewords + ErrCorrCodewords;
			}

			// copy next block of codewords to the buffer and clear the remaining part
			Array.Copy(CodewordsArray, dataCodewordsPtr, errCorrBuff, 0, dataCodewords);
			Array.Copy(CodewordsArray, codewordsArrayErrCorrPtr, errCorrBuff, dataCodewords, ErrCorrCodewords);

			// make a duplicate
			var correctionBuffer = (byte[])errCorrBuff.Clone();

			// error correction polynomial division
			PolynominalDivision(errCorrBuff, buffLen, generator, ErrCorrCodewords);

			// test for error
			int index;
			for (index = 0; index < ErrCorrCodewords && errCorrBuff[dataCodewords + index] == 0; index++)
				;
			if (index < ErrCorrCodewords)
			{
				// correct the error
				var errorCount = CorrectData(correctionBuffer, buffLen, ErrCorrCodewords);
				if (errorCount <= 0) throw new ApplicationException("Data is damaged. Error correction failed");

				totalErrorCount += errorCount;

				// fix the data
				Array.Copy(correctionBuffer, 0, CodewordsArray, dataCodewordsPtr, dataCodewords);
			}

			// update codewords array to next buffer
			dataCodewordsPtr += dataCodewords;

			// update pointer				
			codewordsArrayErrCorrPtr += ErrCorrCodewords;
		}

#if DEBUG
		if (totalErrorCount == 0)
			QRCodeTrace.Write("No data errors");
		else
			QRCodeTrace.Write("Error correction applied to data. Total errors: " + totalErrorCount);
#endif
	}

	////////////////////////////////////////////////////////////////////
	// Convert bit array to byte array
	////////////////////////////////////////////////////////////////////

	internal byte[] DecodeData()
	{
		// bit buffer initial condition
		BitBuffer = (uint)((CodewordsArray[0] << 24) | (CodewordsArray[1] << 16) | (CodewordsArray[2] << 8) |
		                   CodewordsArray[3]);
		BitBufferLen = 32;
		CodewordsPtr = 4;

		// allocate data byte list
		List<byte> dataSeg = new();

		// reset ECI assignment value
		EciAssignValue = -1;

		// data might be made of blocks
		for (;;)
		{
			// first 4 bits is mode indicator
			var encodingMode = (EncodingMode)ReadBitsFromCodewordsArray(4);

			// end of data
			if (encodingMode <= 0) break;

			// test for encoding ECI assignment number
			if (encodingMode == EncodingMode.Eci)
			{
				// one byte assinment value
				EciAssignValue = ReadBitsFromCodewordsArray(8);
				if ((EciAssignValue & 0x80) == 0) continue;

				// two bytes assinment value
				EciAssignValue = (EciAssignValue << 8) | ReadBitsFromCodewordsArray(8);
				if ((EciAssignValue & 0x4000) == 0)
				{
					EciAssignValue &= 0x3fff;
					continue;
				}

				// three bytes assinment value
				EciAssignValue = (EciAssignValue << 8) | ReadBitsFromCodewordsArray(8);
				if ((EciAssignValue & 0x200000) == 0)
				{
					EciAssignValue &= 0x1fffff;
					continue;
				}

				throw new ApplicationException("ECI encoding assinment number in error");
			}

			// read data length
			var dataLength = ReadBitsFromCodewordsArray(DataLengthBits(encodingMode));
			if (dataLength < 0) throw new ApplicationException("Premature end of data (DataLengh)");

			// save start of segment
			var segStart = dataSeg.Count;

			// switch based on encode mode
			// numeric code indicator is 0001, alpha numeric 0010, byte 0100
			switch (encodingMode)
			{
				// numeric mode
				case EncodingMode.Numeric:
					// encode digits in groups of 2
					var numericEnd = dataLength / 3 * 3;
					for (var index = 0; index < numericEnd; index += 3)
					{
						var temp = ReadBitsFromCodewordsArray(10);
						if (temp < 0) throw new ApplicationException("Premature end of data (Numeric 1)");
						dataSeg.Add(DecodingTable[temp / 100]);
						dataSeg.Add(DecodingTable[temp % 100 / 10]);
						dataSeg.Add(DecodingTable[temp % 10]);
					}

					// we have one character remaining
					if (dataLength - numericEnd == 1)
					{
						var temp = ReadBitsFromCodewordsArray(4);
						if (temp < 0) throw new ApplicationException("Premature end of data (Numeric 2)");
						dataSeg.Add(DecodingTable[temp]);
					}

					// we have two character remaining
					else if (dataLength - numericEnd == 2)
					{
						var temp = ReadBitsFromCodewordsArray(7);
						if (temp < 0) throw new ApplicationException("Premature end of data (Numeric 3)");
						dataSeg.Add(DecodingTable[temp / 10]);
						dataSeg.Add(DecodingTable[temp % 10]);
					}

					break;

				// alphanumeric mode
				case EncodingMode.AlphaNumeric:
					// encode digits in groups of 2
					var alphaNumEnd = dataLength / 2 * 2;
					for (var index = 0; index < alphaNumEnd; index += 2)
					{
						var temp = ReadBitsFromCodewordsArray(11);
						if (temp < 0) throw new ApplicationException("Premature end of data (Alpha Numeric 1)");
						dataSeg.Add(DecodingTable[temp / 45]);
						dataSeg.Add(DecodingTable[temp % 45]);
					}

					// we have one character remaining
					if (dataLength - alphaNumEnd == 1)
					{
						var temp = ReadBitsFromCodewordsArray(6);
						if (temp < 0) throw new ApplicationException("Premature end of data (Alpha Numeric 2)");
						dataSeg.Add(DecodingTable[temp]);
					}

					break;

				// byte mode					
				case EncodingMode.Byte:
					// append the data after mode and character count
					for (var index = 0; index < dataLength; index++)
					{
						var temp = ReadBitsFromCodewordsArray(8);
						if (temp < 0) throw new ApplicationException("Premature end of data (byte mode)");
						dataSeg.Add((byte)temp);
					}

					break;

				default:
					throw new ApplicationException(string.Format("Encoding mode not supported {0}",
						encodingMode.ToString()));
			}

			if (dataLength != dataSeg.Count - segStart)
				throw new ApplicationException("Data encoding length in error");
		}

		// save data
		return dataSeg.ToArray();
	}

	////////////////////////////////////////////////////////////////////
	// Read data from codeword array
	////////////////////////////////////////////////////////////////////
	internal int ReadBitsFromCodewordsArray
	(
		int bits
	)
	{
		if (bits > BitBufferLen) return -1;
		var data = (int)(BitBuffer >> (32 - bits));
		BitBuffer <<= bits;
		BitBufferLen -= bits;
		while (BitBufferLen <= 24 && CodewordsPtr < MaxDataCodewords)
		{
			BitBuffer |= (uint)(CodewordsArray[CodewordsPtr++] << (24 - BitBufferLen));
			BitBufferLen += 8;
		}

		return data;
	}
	////////////////////////////////////////////////////////////////////
	// Set encoded data bits length
	////////////////////////////////////////////////////////////////////

	internal int DataLengthBits
	(
		EncodingMode encodingMode
	)
	{
		// Data length bits
#pragma warning disable IDE0066
		switch (encodingMode)
		{
			// numeric mode
			case EncodingMode.Numeric:
				return QRCodeVersion < 10 ? 10 : QRCodeVersion < 27 ? 12 : 14;

			// alpha numeric mode
			case EncodingMode.AlphaNumeric:
				return QRCodeVersion < 10 ? 9 : QRCodeVersion < 27 ? 11 : 13;

			// byte mode
			case EncodingMode.Byte:
				return QRCodeVersion < 10 ? 8 : 16;
		}
#pragma warning restore IDE0066

		throw new ApplicationException("Unsupported encoding mode " + encodingMode);
	}

	////////////////////////////////////////////////////////////////////
	// Set data and error correction codewords length
	////////////////////////////////////////////////////////////////////

	internal void SetDataCodewordsLength()
	{
		// index shortcut
		var blockInfoIndex = (QRCodeVersion - 1) * 4 + (int)ErrorCorrection;

		// Number of blocks in group 1
		BlocksGroup1 = EcBlockInfo[blockInfoIndex, BLOCKS_GROUP1];

		// Number of data codewords in blocks of group 1
		DataCodewordsGroup1 = EcBlockInfo[blockInfoIndex, DATA_CODEWORDS_GROUP1];

		// Number of blocks in group 2
		BlocksGroup2 = EcBlockInfo[blockInfoIndex, BLOCKS_GROUP2];

		// Number of data codewords in blocks of group 2
		DataCodewordsGroup2 = EcBlockInfo[blockInfoIndex, DATA_CODEWORDS_GROUP2];

		// Total number of data codewords for this version and EC level
		MaxDataCodewords = BlocksGroup1 * DataCodewordsGroup1 + BlocksGroup2 * DataCodewordsGroup2;
		MaxDataBits = 8 * MaxDataCodewords;

		// total data plus error correction bits
		MaxCodewords = MaxCodewordsArray[QRCodeVersion];

		// Error correction codewords per block
		ErrCorrCodewords = (MaxCodewords - MaxDataCodewords) / (BlocksGroup1 + BlocksGroup2);

		// exit
	}

	////////////////////////////////////////////////////////////////////
	// Format info to error correction code
	////////////////////////////////////////////////////////////////////
	internal static ErrorCorrection FormatInfoToErrCode
	(
		int info
	)
	{
		return (ErrorCorrection)(info ^ 1);
	}

	////////////////////////////////////////////////////////////////////
	// Build Base Matrix
	////////////////////////////////////////////////////////////////////
	internal void BuildBaseMatrix()
	{
		// allocate base matrix
		BaseMatrix = new byte[QRCodeDimension + 5, QRCodeDimension + 5];

		// top left finder patterns
		for (var row = 0; row < 9; row++)
		for (var col = 0; col < 9; col++)
			BaseMatrix[row, col] = FinderPatternTopLeft[row, col];

		// top right finder patterns
		var pos = QRCodeDimension - 8;
		for (var row = 0; row < 9; row++)
		for (var col = 0; col < 8; col++)
			BaseMatrix[row, pos + col] = FinderPatternTopRight[row, col];

		// bottom left finder patterns
		for (var row = 0; row < 8; row++)
		for (var col = 0; col < 9; col++)
			BaseMatrix[pos + row, col] = FinderPatternBottomLeft[row, col];

		// Timing pattern
		for (var z = 8; z < QRCodeDimension - 8; z++)
			BaseMatrix[z, 6] = BaseMatrix[6, z] = (z & 1) == 0 ? FixedBlack : FixedWhite;

		// alignment pattern
		if (QRCodeVersion > 1)
		{
			var alignPos = AlignmentPositionArray[QRCodeVersion];
			var alignmentDimension = alignPos.Length;
			for (var row = 0; row < alignmentDimension; row++)
			for (var col = 0; col < alignmentDimension; col++)
			{
				if ((col == 0 && row == 0) || (col == alignmentDimension - 1 && row == 0) ||
				    (col == 0 && row == alignmentDimension - 1))
					continue;

				int posRow = alignPos[row];
				int posCol = alignPos[col];
				for (var aRow = -2; aRow < 3; aRow++)
				for (var aCol = -2; aCol < 3; aCol++)
					BaseMatrix[posRow + aRow, posCol + aCol] = AlignmentPattern[aRow + 2, aCol + 2];
			}
		}

		// reserve version information
		if (QRCodeVersion >= 7)
		{
			// position of 3 by 6 rectangles
			pos = QRCodeDimension - 11;

			// top right
			for (var row = 0; row < 6; row++)
			for (var col = 0; col < 3; col++)
				BaseMatrix[row, pos + col] = FormatWhite;

			// bottom right
			for (var col = 0; col < 6; col++)
			for (var row = 0; row < 3; row++)
				BaseMatrix[pos + row, col] = FormatWhite;
		}
	}

	////////////////////////////////////////////////////////////////////
	// Apply Mask
	////////////////////////////////////////////////////////////////////

	internal void ApplyMask
	(
		int mask
	)
	{
		MaskMatrix = (byte[,])BaseMatrix.Clone();
		switch (mask)
		{
			case 0:
				ApplyMask0();
				break;

			case 1:
				ApplyMask1();
				break;

			case 2:
				ApplyMask2();
				break;

			case 3:
				ApplyMask3();
				break;

			case 4:
				ApplyMask4();
				break;

			case 5:
				ApplyMask5();
				break;

			case 6:
				ApplyMask6();
				break;

			case 7:
				ApplyMask7();
				break;
		}
	}

	////////////////////////////////////////////////////////////////////
	// Apply Mask 0
	// (row + column) % 2 == 0
	////////////////////////////////////////////////////////////////////

	internal void ApplyMask0()
	{
		for (var row = 0; row < QRCodeDimension; row += 2)
		for (var col = 0; col < QRCodeDimension; col += 2)
		{
			if ((MaskMatrix[row, col] & NonData) == 0)
				MaskMatrix[row, col] ^= 1;
			if ((MaskMatrix[row + 1, col + 1] & NonData) == 0)
				MaskMatrix[row + 1, col + 1] ^= 1;
		}
	}

	////////////////////////////////////////////////////////////////////
	// Apply Mask 1
	// row % 2 == 0
	////////////////////////////////////////////////////////////////////

	internal void ApplyMask1()
	{
		for (var row = 0; row < QRCodeDimension; row += 2)
		for (var col = 0; col < QRCodeDimension; col++)
			if ((MaskMatrix[row, col] & NonData) == 0)
				MaskMatrix[row, col] ^= 1;
	}

	////////////////////////////////////////////////////////////////////
	// Apply Mask 2
	// column % 3 == 0
	////////////////////////////////////////////////////////////////////

	internal void ApplyMask2()
	{
		for (var row = 0; row < QRCodeDimension; row++)
		for (var col = 0; col < QRCodeDimension; col += 3)
			if ((MaskMatrix[row, col] & NonData) == 0)
				MaskMatrix[row, col] ^= 1;
	}

	////////////////////////////////////////////////////////////////////
	// Apply Mask 3
	// (row + column) % 3 == 0
	////////////////////////////////////////////////////////////////////

	internal void ApplyMask3()
	{
		for (var row = 0; row < QRCodeDimension; row += 3)
		for (var col = 0; col < QRCodeDimension; col += 3)
		{
			if ((MaskMatrix[row, col] & NonData) == 0)
				MaskMatrix[row, col] ^= 1;
			if ((MaskMatrix[row + 1, col + 2] & NonData) == 0)
				MaskMatrix[row + 1, col + 2] ^= 1;
			if ((MaskMatrix[row + 2, col + 1] & NonData) == 0)
				MaskMatrix[row + 2, col + 1] ^= 1;
		}
	}

	////////////////////////////////////////////////////////////////////
	// Apply Mask 4
	// ((row / 2) + (column / 3)) % 2 == 0
	////////////////////////////////////////////////////////////////////

	internal void ApplyMask4()
	{
		for (var row = 0; row < QRCodeDimension; row += 4)
		for (var col = 0; col < QRCodeDimension; col += 6)
		{
			if ((MaskMatrix[row, col] & NonData) == 0)
				MaskMatrix[row, col] ^= 1;
			if ((MaskMatrix[row, col + 1] & NonData) == 0)
				MaskMatrix[row, col + 1] ^= 1;
			if ((MaskMatrix[row, col + 2] & NonData) == 0)
				MaskMatrix[row, col + 2] ^= 1;

			if ((MaskMatrix[row + 1, col] & NonData) == 0)
				MaskMatrix[row + 1, col] ^= 1;
			if ((MaskMatrix[row + 1, col + 1] & NonData) == 0)
				MaskMatrix[row + 1, col + 1] ^= 1;
			if ((MaskMatrix[row + 1, col + 2] & NonData) == 0)
				MaskMatrix[row + 1, col + 2] ^= 1;

			if ((MaskMatrix[row + 2, col + 3] & NonData) == 0)
				MaskMatrix[row + 2, col + 3] ^= 1;
			if ((MaskMatrix[row + 2, col + 4] & NonData) == 0)
				MaskMatrix[row + 2, col + 4] ^= 1;
			if ((MaskMatrix[row + 2, col + 5] & NonData) == 0)
				MaskMatrix[row + 2, col + 5] ^= 1;

			if ((MaskMatrix[row + 3, col + 3] & NonData) == 0)
				MaskMatrix[row + 3, col + 3] ^= 1;
			if ((MaskMatrix[row + 3, col + 4] & NonData) == 0)
				MaskMatrix[row + 3, col + 4] ^= 1;
			if ((MaskMatrix[row + 3, col + 5] & NonData) == 0)
				MaskMatrix[row + 3, col + 5] ^= 1;
		}
	}

	////////////////////////////////////////////////////////////////////
	// Apply Mask 5
	// ((row * column) % 2) + ((row * column) % 3) == 0
	////////////////////////////////////////////////////////////////////

	internal void ApplyMask5()
	{
		for (var row = 0; row < QRCodeDimension; row += 6)
		for (var col = 0; col < QRCodeDimension; col += 6)
		{
			for (var delta = 0; delta < 6; delta++)
				if ((MaskMatrix[row, col + delta] & NonData) == 0)
					MaskMatrix[row, col + delta] ^= 1;
			for (var delta = 1; delta < 6; delta++)
				if ((MaskMatrix[row + delta, col] & NonData) == 0)
					MaskMatrix[row + delta, col] ^= 1;
			if ((MaskMatrix[row + 2, col + 3] & NonData) == 0)
				MaskMatrix[row + 2, col + 3] ^= 1;
			if ((MaskMatrix[row + 3, col + 2] & NonData) == 0)
				MaskMatrix[row + 3, col + 2] ^= 1;
			if ((MaskMatrix[row + 3, col + 4] & NonData) == 0)
				MaskMatrix[row + 3, col + 4] ^= 1;
			if ((MaskMatrix[row + 4, col + 3] & NonData) == 0)
				MaskMatrix[row + 4, col + 3] ^= 1;
		}
	}

	////////////////////////////////////////////////////////////////////
	// Apply Mask 6
	// (((row * column) % 2) + ((row * column) mod 3)) mod 2 == 0
	////////////////////////////////////////////////////////////////////

	internal void ApplyMask6()
	{
		for (var row = 0; row < QRCodeDimension; row += 6)
		for (var col = 0; col < QRCodeDimension; col += 6)
		{
			for (var delta = 0; delta < 6; delta++)
				if ((MaskMatrix[row, col + delta] & NonData) == 0)
					MaskMatrix[row, col + delta] ^= 1;
			for (var delta = 1; delta < 6; delta++)
				if ((MaskMatrix[row + delta, col] & NonData) == 0)
					MaskMatrix[row + delta, col] ^= 1;
			if ((MaskMatrix[row + 1, col + 1] & NonData) == 0)
				MaskMatrix[row + 1, col + 1] ^= 1;
			if ((MaskMatrix[row + 1, col + 2] & NonData) == 0)
				MaskMatrix[row + 1, col + 2] ^= 1;
			if ((MaskMatrix[row + 2, col + 1] & NonData) == 0)
				MaskMatrix[row + 2, col + 1] ^= 1;
			if ((MaskMatrix[row + 2, col + 3] & NonData) == 0)
				MaskMatrix[row + 2, col + 3] ^= 1;
			if ((MaskMatrix[row + 2, col + 4] & NonData) == 0)
				MaskMatrix[row + 2, col + 4] ^= 1;
			if ((MaskMatrix[row + 3, col + 2] & NonData) == 0)
				MaskMatrix[row + 3, col + 2] ^= 1;
			if ((MaskMatrix[row + 3, col + 4] & NonData) == 0)
				MaskMatrix[row + 3, col + 4] ^= 1;
			if ((MaskMatrix[row + 4, col + 2] & NonData) == 0)
				MaskMatrix[row + 4, col + 2] ^= 1;
			if ((MaskMatrix[row + 4, col + 3] & NonData) == 0)
				MaskMatrix[row + 4, col + 3] ^= 1;
			if ((MaskMatrix[row + 4, col + 5] & NonData) == 0)
				MaskMatrix[row + 4, col + 5] ^= 1;
			if ((MaskMatrix[row + 5, col + 4] & NonData) == 0)
				MaskMatrix[row + 5, col + 4] ^= 1;
			if ((MaskMatrix[row + 5, col + 5] & NonData) == 0)
				MaskMatrix[row + 5, col + 5] ^= 1;
		}
	}

	////////////////////////////////////////////////////////////////////
	// Apply Mask 7
	// (((row + column) % 2) + ((row * column) mod 3)) mod 2 == 0
	////////////////////////////////////////////////////////////////////

	internal void ApplyMask7()
	{
		for (var row = 0; row < QRCodeDimension; row += 6)
		for (var col = 0; col < QRCodeDimension; col += 6)
		{
			if ((MaskMatrix[row, col] & NonData) == 0)
				MaskMatrix[row, col] ^= 1;
			if ((MaskMatrix[row, col + 2] & NonData) == 0)
				MaskMatrix[row, col + 2] ^= 1;
			if ((MaskMatrix[row, col + 4] & NonData) == 0)
				MaskMatrix[row, col + 4] ^= 1;

			if ((MaskMatrix[row + 1, col + 3] & NonData) == 0)
				MaskMatrix[row + 1, col + 3] ^= 1;
			if ((MaskMatrix[row + 1, col + 4] & NonData) == 0)
				MaskMatrix[row + 1, col + 4] ^= 1;
			if ((MaskMatrix[row + 1, col + 5] & NonData) == 0)
				MaskMatrix[row + 1, col + 5] ^= 1;

			if ((MaskMatrix[row + 2, col] & NonData) == 0)
				MaskMatrix[row + 2, col] ^= 1;
			if ((MaskMatrix[row + 2, col + 4] & NonData) == 0)
				MaskMatrix[row + 2, col + 4] ^= 1;
			if ((MaskMatrix[row + 2, col + 5] & NonData) == 0)
				MaskMatrix[row + 2, col + 5] ^= 1;

			if ((MaskMatrix[row + 3, col + 1] & NonData) == 0)
				MaskMatrix[row + 3, col + 1] ^= 1;
			if ((MaskMatrix[row + 3, col + 3] & NonData) == 0)
				MaskMatrix[row + 3, col + 3] ^= 1;
			if ((MaskMatrix[row + 3, col + 5] & NonData) == 0)
				MaskMatrix[row + 3, col + 5] ^= 1;

			if ((MaskMatrix[row + 4, col] & NonData) == 0)
				MaskMatrix[row + 4, col] ^= 1;
			if ((MaskMatrix[row + 4, col + 1] & NonData) == 0)
				MaskMatrix[row + 4, col + 1] ^= 1;
			if ((MaskMatrix[row + 4, col + 2] & NonData) == 0)
				MaskMatrix[row + 4, col + 2] ^= 1;

			if ((MaskMatrix[row + 5, col + 1] & NonData) == 0)
				MaskMatrix[row + 5, col + 1] ^= 1;
			if ((MaskMatrix[row + 5, col + 2] & NonData) == 0)
				MaskMatrix[row + 5, col + 2] ^= 1;
			if ((MaskMatrix[row + 5, col + 3] & NonData) == 0)
				MaskMatrix[row + 5, col + 3] ^= 1;
		}
	}

	internal static int IncorrectableError = -1;

	internal static int CorrectData
	(
		byte[] receivedData, // recived data buffer with data and error correction code
		int dataLength, // length of data in the buffer (note sometimes the array is longer than data) 
		int errCorrCodewords // numer of error correction codewords
	)
	{
		// calculate syndrome vector
		var syndrome = CalculateSyndrome(receivedData, dataLength, errCorrCodewords);

		// received data has no error
		// note: this should not happen because we call this method only if error was detected
		if (syndrome == null) return 0;

		// Modified Berlekamp-Massey
		// calculate sigma and omega
		var sigma = new int[errCorrCodewords / 2 + 2];
		var omega = new int[errCorrCodewords / 2 + 1];
		var errorCount = CalculateSigmaMbm(sigma, omega, syndrome, errCorrCodewords);

		// data cannot be corrected
		if (errorCount <= 0) return IncorrectableError;

		// look for error position using Chien search
		var errorPosition = new int[errorCount];
		if (!ChienSearch(errorPosition, dataLength, errorCount, sigma)) return IncorrectableError;

		// correct data array based on position array
		ApplyCorrection(receivedData, dataLength, errorCount, errorPosition, sigma, omega);

		// return error count before it was corrected
		return errorCount;
	}

	// Syndrome vector calculation
	// S0 = R0 + R1 +        R2 + ....        + Rn
	// S1 = R0 + R1 * A**1 + R2 * A**2 + .... + Rn * A**n
	// S2 = R0 + R1 * A**2 + R2 * A**4 + .... + Rn * A**2n
	// ....
	// Sm = R0 + R1 * A**m + R2 * A**2m + .... + Rn * A**mn

	internal static int[] CalculateSyndrome
	(
		byte[] receivedData, // recived data buffer with data and error correction code
		int dataLength, // length of data in the buffer (note sometimes the array is longer than data) 
		int errCorrCodewords // numer of error correction codewords
	)
	{
		// allocate syndrome vector
		var syndrome = new int[errCorrCodewords];

		// reset error indicator
		var error = false;

		// syndrome[zero] special case
		// Total = Data[0] + Data[1] + ... Data[n]
		int total = receivedData[0];
		for (var sumIndex = 1; sumIndex < dataLength; sumIndex++) total = receivedData[sumIndex] ^ total;
		syndrome[0] = total;
		if (total != 0) error = true;

		// all other synsromes
		for (var index = 1; index < errCorrCodewords; index++)
		{
			// Total = Data[0] + Data[1] * Alpha + Data[2] * Alpha ** 2 + ... Data[n] * Alpha ** n
			total = receivedData[0];
			for (var indexT = 1; indexT < dataLength; indexT++)
				total = receivedData[indexT] ^ MultiplyIntByExp(total, index);
			syndrome[index] = total;
			if (total != 0) error = true;
		}

		// if there is an error return syndrome vector otherwise return null
		return error ? syndrome : null;
	}

	// Modified Berlekamp-Massey
	internal static int CalculateSigmaMbm
	(
		int[] sigma,
		int[] omega,
		int[] syndrome,
		int errCorrCodewords
	)
	{
		var polyC = new int[errCorrCodewords];
		var polyB = new int[errCorrCodewords];
		polyC[1] = 1;
		polyB[0] = 1;
		var errorControl = 1;
		var errorCount = 0; // L
		var m = -1;

		for (var errCorrIndex = 0; errCorrIndex < errCorrCodewords; errCorrIndex++)
		{
			// Calculate the discrepancy
			var dis = syndrome[errCorrIndex];
			for (var i = 1; i <= errorCount; i++)
				dis ^= Multiply(polyB[i], syndrome[errCorrIndex - i]);

			if (dis != 0)
			{
				int disExp = IntToExp[dis];
				var workPolyB = new int[errCorrCodewords];
				for (var index = 0; index <= errCorrIndex; index++)
					workPolyB[index] = polyB[index] ^ MultiplyIntByExp(polyC[index], disExp);
				var js = errCorrIndex - m;
				if (js > errorCount)
				{
					m = errCorrIndex - errorCount;
					errorCount = js;
					if (errorCount > errCorrCodewords / 2) return IncorrectableError;
					for (var index = 0; index <= errorControl; index++)
						polyC[index] = DivideIntByExp(polyB[index], disExp);
					errorControl = errorCount;
				}

				polyB = workPolyB;
			}

			// shift polynomial right one
			Array.Copy(polyC, 0, polyC, 1, Math.Min(polyC.Length - 1, errorControl));
			polyC[0] = 0;
			errorControl++;
		}

		PolynomialMultiply(omega, polyB, syndrome);
		Array.Copy(polyB, 0, sigma, 0, Math.Min(polyB.Length, sigma.Length));
		return errorCount;
	}

	// Chien search is a fast algorithm for determining roots of polynomials defined over a finite field.
	// The most typical use of the Chien search is in finding the roots of error-locator polynomials
	// encountered in decoding Reed-Solomon codes and BCH codes.
	private static bool ChienSearch
	(
		int[] errorPosition,
		int dataLength,
		int errorCount,
		int[] sigma
	)
	{
		// last error
		var lastPosition = sigma[1];

		// one error
		if (errorCount == 1)
		{
			// position is out of range
			if (IntToExp[lastPosition] >= dataLength) return false;

			// save the only error position in position array
			errorPosition[0] = lastPosition;
			return true;
		}

		// we start at last error position
		var posIndex = errorCount - 1;
		for (var dataIndex = 0; dataIndex < dataLength; dataIndex++)
		{
			var dataIndexInverse = 255 - dataIndex;
			var total = 1;
			for (var index = 1; index <= errorCount; index++)
				total ^= MultiplyIntByExp(sigma[index], dataIndexInverse * index % 255);
			if (total != 0) continue;

			int position = ExpToInt[dataIndex];
			lastPosition ^= position;
			errorPosition[posIndex--] = position;
			if (posIndex == 0)
			{
				// position is out of range
				if (IntToExp[lastPosition] >= dataLength) return false;
				errorPosition[0] = lastPosition;
				return true;
			}
		}

		// search failed
		return false;
	}

	private static void ApplyCorrection
	(
		byte[] receivedData,
		int dataLength,
		int errorCount,
		int[] errorPosition,
		int[] sigma,
		int[] omega
	)
	{
		for (var errIndex = 0; errIndex < errorCount; errIndex++)
		{
			var ps = errorPosition[errIndex];
			var zlog = 255 - IntToExp[ps];
			var omegaTotal = omega[0];
			for (var index = 1; index < errorCount; index++)
				omegaTotal ^= MultiplyIntByExp(omega[index], zlog * index % 255);
			var sigmaTotal = sigma[1];
			for (var j = 2; j < errorCount; j += 2)
				sigmaTotal ^= MultiplyIntByExp(sigma[j + 1], zlog * j % 255);
			receivedData[dataLength - 1 - IntToExp[ps]] ^= (byte)MultiplyDivide(ps, omegaTotal, sigmaTotal);
		}
	}

	internal static void PolynominalDivision(byte[] polynomial, int polyLength, byte[] generator, int errCorrCodewords)
	{
		var dataCodewords = polyLength - errCorrCodewords;

		// error correction polynomial division
		for (var index = 0; index < dataCodewords; index++)
		{
			// current first codeword is zero
			if (polynomial[index] == 0)
				continue;

			// current first codeword is not zero
			int multiplier = IntToExp[polynomial[index]];

			// loop for error correction coofficients
			for (var generatorIndex = 0; generatorIndex < errCorrCodewords; generatorIndex++)
				polynomial[index + 1 + generatorIndex] = (byte)(polynomial[index + 1 + generatorIndex] ^
				                                                ExpToInt[generator[generatorIndex] + multiplier]);
		}
	}

	internal static int Multiply
	(
		int int1,
		int int2
	)
	{
		return int1 == 0 || int2 == 0 ? 0 : ExpToInt[IntToExp[int1] + IntToExp[int2]];
	}

	internal static int MultiplyIntByExp
	(
		int @int,
		int exp
	)
	{
		return @int == 0 ? 0 : ExpToInt[IntToExp[@int] + exp];
	}

	internal static int MultiplyDivide
	(
		int int1,
		int int2,
		int int3
	)
	{
		return int1 == 0 || int2 == 0 ? 0 : ExpToInt[(IntToExp[int1] + IntToExp[int2] - IntToExp[int3] + 255) % 255];
	}

	internal static int DivideIntByExp
	(
		int @int,
		int exp
	)
	{
		return @int == 0 ? 0 : ExpToInt[IntToExp[@int] - exp + 255];
	}

	internal static void PolynomialMultiply(int[] result, int[] poly1, int[] poly2)
	{
		Array.Clear(result, 0, result.Length);
		for (var index1 = 0; index1 < poly1.Length; index1++)
		{
			if (poly1[index1] == 0)
				continue;
			int loga = IntToExp[poly1[index1]];
			var index2End = Math.Min(poly2.Length, result.Length - index1);
			// = Sum(Poly1[Index1] * Poly2[Index2]) for all Index2
			for (var index2 = 0; index2 < index2End; index2++)
				if (poly2[index2] != 0)
					result[index1 + index2] ^= ExpToInt[loga + IntToExp[poly2[index2]]];
		}
	}

	// alignment symbols position as function of dimension
	internal static readonly byte[][] AlignmentPositionArray =
	{
		null,
		null,
		new byte[] { 6, 18 },
		new byte[] { 6, 22 },
		new byte[] { 6, 26 },
		new byte[] { 6, 30 },
		new byte[] { 6, 34 },
		new byte[] { 6, 22, 38 },
		new byte[] { 6, 24, 42 },
		new byte[] { 6, 26, 46 },
		new byte[] { 6, 28, 50 },
		new byte[] { 6, 30, 54 },
		new byte[] { 6, 32, 58 },
		new byte[] { 6, 34, 62 },
		new byte[] { 6, 26, 46, 66 },
		new byte[] { 6, 26, 48, 70 },
		new byte[] { 6, 26, 50, 74 },
		new byte[] { 6, 30, 54, 78 },
		new byte[] { 6, 30, 56, 82 },
		new byte[] { 6, 30, 58, 86 },
		new byte[] { 6, 34, 62, 90 },
		new byte[] { 6, 28, 50, 72, 94 },
		new byte[] { 6, 26, 50, 74, 98 },
		new byte[] { 6, 30, 54, 78, 102 },
		new byte[] { 6, 28, 54, 80, 106 },
		new byte[] { 6, 32, 58, 84, 110 },
		new byte[] { 6, 30, 58, 86, 114 },
		new byte[] { 6, 34, 62, 90, 118 },
		new byte[] { 6, 26, 50, 74, 98, 122 },
		new byte[] { 6, 30, 54, 78, 102, 126 },
		new byte[] { 6, 26, 52, 78, 104, 130 },
		new byte[] { 6, 30, 56, 82, 108, 134 },
		new byte[] { 6, 34, 60, 86, 112, 138 },
		new byte[] { 6, 30, 58, 86, 114, 142 },
		new byte[] { 6, 34, 62, 90, 118, 146 },
		new byte[] { 6, 30, 54, 78, 102, 126, 150 },
		new byte[] { 6, 24, 50, 76, 102, 128, 154 },
		new byte[] { 6, 28, 54, 80, 106, 132, 158 },
		new byte[] { 6, 32, 58, 84, 110, 136, 162 },
		new byte[] { 6, 26, 54, 82, 110, 138, 166 },
		new byte[] { 6, 30, 58, 86, 114, 142, 170 }
	};

	// maximum code words as function of dimension
	internal static readonly int[] MaxCodewordsArray =
	{
		0,
		26, 44, 70, 100, 134, 172, 196, 242, 292, 346,
		404, 466, 532, 581, 655, 733, 815, 901, 991, 1085,
		1156, 1258, 1364, 1474, 1588, 1706, 1828, 1921, 2051, 2185,
		2323, 2465, 2611, 2761, 2876, 3034, 3196, 3362, 3532, 3706
	};

	// Encodable character set:
	// 1) numeric data (digits 0 - 9);
	// 2) alphanumeric data (digits 0 - 9; upper case letters A -Z; nine other characters: space, $ % * + - . / : );
	// 3) 8-bit byte data (JIS 8-bit character set (Latin and Kana) in accordance with JIS X 0201);
	// 4) Kanji characters (Shift JIS character set in accordance with JIS X 0208 Annex 1 Shift Coded
	//    Representation. Note that Kanji characters in QR Code can have values 8140HEX -9FFCHEX and E040HEX -
	//    EBBFHEX , which can be compacted into 13 bits.)

	internal static readonly byte[] EncodingTable =
	{
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		36, 45, 45, 45, 37, 38, 45, 45, 45, 45, 39, 40, 45, 41, 42, 43,
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 44, 45, 45, 45, 45, 45,
		45, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
		25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45,
		45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45
	};

	internal static readonly byte[] DecodingTable =
	{
		(byte)'0', // 0
		(byte)'1', // 1
		(byte)'2', // 2
		(byte)'3', // 3
		(byte)'4', // 4
		(byte)'5', // 5
		(byte)'6', // 6
		(byte)'7', // 7
		(byte)'8', // 8
		(byte)'9', // 9
		(byte)'A', // 10
		(byte)'B', // 11
		(byte)'C', // 12
		(byte)'D', // 13
		(byte)'E', // 14
		(byte)'F', // 15
		(byte)'G', // 16
		(byte)'H', // 17
		(byte)'I', // 18
		(byte)'J', // 19
		(byte)'K', // 20
		(byte)'L', // 21
		(byte)'M', // 22
		(byte)'N', // 23
		(byte)'O', // 24
		(byte)'P', // 25
		(byte)'Q', // 26
		(byte)'R', // 27
		(byte)'S', // 28
		(byte)'T', // 29
		(byte)'U', // 30
		(byte)'V', // 31
		(byte)'W', // 32
		(byte)'X', // 33
		(byte)'Y', // 34
		(byte)'Z', // 35
		(byte)' ', // 36 (space)
		(byte)'$', // 37
		(byte)'%', // 38
		(byte)'*', // 39
		(byte)'+', // 40
		(byte)'-', // 41
		(byte)'.', // 42
		(byte)'/', // 43
		(byte)':' // 44
	};

	// Error correction block information
	// A-Number of blocks in group 1
	internal const int BLOCKS_GROUP1 = 0;

	// B-Number of data codewords in blocks of group 1
	internal const int DATA_CODEWORDS_GROUP1 = 1;

	// C-Number of blocks in group 2
	internal const int BLOCKS_GROUP2 = 2;

	// D-Number of data codewords in blocks of group 2
	internal const int DATA_CODEWORDS_GROUP2 = 3;

	internal static readonly byte[,] EcBlockInfo =
	{
		// A,   B,   C,   D 
		{ 1, 19, 0, 0 }, // 1-L
		{ 1, 16, 0, 0 }, // 1-M
		{ 1, 13, 0, 0 }, // 1-Q
		{ 1, 9, 0, 0 }, // 1-H
		{ 1, 34, 0, 0 }, // 2-L
		{ 1, 28, 0, 0 }, // 2-M
		{ 1, 22, 0, 0 }, // 2-Q
		{ 1, 16, 0, 0 }, // 2-H
		{ 1, 55, 0, 0 }, // 3-L
		{ 1, 44, 0, 0 }, // 3-M
		{ 2, 17, 0, 0 }, // 3-Q
		{ 2, 13, 0, 0 }, // 3-H
		{ 1, 80, 0, 0 }, // 4-L
		{ 2, 32, 0, 0 }, // 4-M
		{ 2, 24, 0, 0 }, // 4-Q
		{ 4, 9, 0, 0 }, // 4-H
		{ 1, 108, 0, 0 }, // 5-L
		{ 2, 43, 0, 0 }, // 5-M
		{ 2, 15, 2, 16 }, // 5-Q
		{ 2, 11, 2, 12 }, // 5-H
		{ 2, 68, 0, 0 }, // 6-L
		{ 4, 27, 0, 0 }, // 6-M
		{ 4, 19, 0, 0 }, // 6-Q
		{ 4, 15, 0, 0 }, // 6-H
		{ 2, 78, 0, 0 }, // 7-L
		{ 4, 31, 0, 0 }, // 7-M
		{ 2, 14, 4, 15 }, // 7-Q
		{ 4, 13, 1, 14 }, // 7-H
		{ 2, 97, 0, 0 }, // 8-L
		{ 2, 38, 2, 39 }, // 8-M
		{ 4, 18, 2, 19 }, // 8-Q
		{ 4, 14, 2, 15 }, // 8-H
		{ 2, 116, 0, 0 }, // 9-L
		{ 3, 36, 2, 37 }, // 9-M
		{ 4, 16, 4, 17 }, // 9-Q
		{ 4, 12, 4, 13 }, // 9-H
		{ 2, 68, 2, 69 }, // 10-L
		{ 4, 43, 1, 44 }, // 10-M
		{ 6, 19, 2, 20 }, // 10-Q
		{ 6, 15, 2, 16 }, // 10-H
		{ 4, 81, 0, 0 }, // 11-L
		{ 1, 50, 4, 51 }, // 11-M
		{ 4, 22, 4, 23 }, // 11-Q
		{ 3, 12, 8, 13 }, // 11-H
		{ 2, 92, 2, 93 }, // 12-L
		{ 6, 36, 2, 37 }, // 12-M
		{ 4, 20, 6, 21 }, // 12-Q
		{ 7, 14, 4, 15 }, // 12-H
		{ 4, 107, 0, 0 }, // 13-L
		{ 8, 37, 1, 38 }, // 13-M
		{ 8, 20, 4, 21 }, // 13-Q
		{ 12, 11, 4, 12 }, // 13-H
		{ 3, 115, 1, 116 }, // 14-L
		{ 4, 40, 5, 41 }, // 14-M
		{ 11, 16, 5, 17 }, // 14-Q
		{ 11, 12, 5, 13 }, // 14-H
		{ 5, 87, 1, 88 }, // 15-L
		{ 5, 41, 5, 42 }, // 15-M
		{ 5, 24, 7, 25 }, // 15-Q
		{ 11, 12, 7, 13 }, // 15-H
		{ 5, 98, 1, 99 }, // 16-L
		{ 7, 45, 3, 46 }, // 16-M
		{ 15, 19, 2, 20 }, // 16-Q
		{ 3, 15, 13, 16 }, // 16-H
		{ 1, 107, 5, 108 }, // 17-L
		{ 10, 46, 1, 47 }, // 17-M
		{ 1, 22, 15, 23 }, // 17-Q
		{ 2, 14, 17, 15 }, // 17-H
		{ 5, 120, 1, 121 }, // 18-L
		{ 9, 43, 4, 44 }, // 18-M
		{ 17, 22, 1, 23 }, // 18-Q
		{ 2, 14, 19, 15 }, // 18-H
		{ 3, 113, 4, 114 }, // 19-L
		{ 3, 44, 11, 45 }, // 19-M
		{ 17, 21, 4, 22 }, // 19-Q
		{ 9, 13, 16, 14 }, // 19-H
		{ 3, 107, 5, 108 }, // 20-L
		{ 3, 41, 13, 42 }, // 20-M
		{ 15, 24, 5, 25 }, // 20-Q
		{ 15, 15, 10, 16 }, // 20-H
		{ 4, 116, 4, 117 }, // 21-L
		{ 17, 42, 0, 0 }, // 21-M
		{ 17, 22, 6, 23 }, // 21-Q
		{ 19, 16, 6, 17 }, // 21-H
		{ 2, 111, 7, 112 }, // 22-L
		{ 17, 46, 0, 0 }, // 22-M
		{ 7, 24, 16, 25 }, // 22-Q
		{ 34, 13, 0, 0 }, // 22-H
		{ 4, 121, 5, 122 }, // 23-L
		{ 4, 47, 14, 48 }, // 23-M
		{ 11, 24, 14, 25 }, // 23-Q
		{ 16, 15, 14, 16 }, // 23-H
		{ 6, 117, 4, 118 }, // 24-L
		{ 6, 45, 14, 46 }, // 24-M
		{ 11, 24, 16, 25 }, // 24-Q
		{ 30, 16, 2, 17 }, // 24-H
		{ 8, 106, 4, 107 }, // 25-L
		{ 8, 47, 13, 48 }, // 25-M
		{ 7, 24, 22, 25 }, // 25-Q
		{ 22, 15, 13, 16 }, // 25-H
		{ 10, 114, 2, 115 }, // 26-L
		{ 19, 46, 4, 47 }, // 26-M
		{ 28, 22, 6, 23 }, // 26-Q
		{ 33, 16, 4, 17 }, // 26-H
		{ 8, 122, 4, 123 }, // 27-L
		{ 22, 45, 3, 46 }, // 27-M
		{ 8, 23, 26, 24 }, // 27-Q
		{ 12, 15, 28, 16 }, // 27-H
		{ 3, 117, 10, 118 }, // 28-L
		{ 3, 45, 23, 46 }, // 28-M
		{ 4, 24, 31, 25 }, // 28-Q
		{ 11, 15, 31, 16 }, // 28-H
		{ 7, 116, 7, 117 }, // 29-L
		{ 21, 45, 7, 46 }, // 29-M
		{ 1, 23, 37, 24 }, // 29-Q
		{ 19, 15, 26, 16 }, // 29-H
		{ 5, 115, 10, 116 }, // 30-L
		{ 19, 47, 10, 48 }, // 30-M
		{ 15, 24, 25, 25 }, // 30-Q
		{ 23, 15, 25, 16 }, // 30-H
		{ 13, 115, 3, 116 }, // 31-L
		{ 2, 46, 29, 47 }, // 31-M
		{ 42, 24, 1, 25 }, // 31-Q
		{ 23, 15, 28, 16 }, // 31-H
		{ 17, 115, 0, 0 }, // 32-L
		{ 10, 46, 23, 47 }, // 32-M
		{ 10, 24, 35, 25 }, // 32-Q
		{ 19, 15, 35, 16 }, // 32-H
		{ 17, 115, 1, 116 }, // 33-L
		{ 14, 46, 21, 47 }, // 33-M
		{ 29, 24, 19, 25 }, // 33-Q
		{ 11, 15, 46, 16 }, // 33-H
		{ 13, 115, 6, 116 }, // 34-L
		{ 14, 46, 23, 47 }, // 34-M
		{ 44, 24, 7, 25 }, // 34-Q
		{ 59, 16, 1, 17 }, // 34-H
		{ 12, 121, 7, 122 }, // 35-L
		{ 12, 47, 26, 48 }, // 35-M
		{ 39, 24, 14, 25 }, // 35-Q
		{ 22, 15, 41, 16 }, // 35-H
		{ 6, 121, 14, 122 }, // 36-L
		{ 6, 47, 34, 48 }, // 36-M
		{ 46, 24, 10, 25 }, // 36-Q
		{ 2, 15, 64, 16 }, // 36-H
		{ 17, 122, 4, 123 }, // 37-L
		{ 29, 46, 14, 47 }, // 37-M
		{ 49, 24, 10, 25 }, // 37-Q
		{ 24, 15, 46, 16 }, // 37-H
		{ 4, 122, 18, 123 }, // 38-L
		{ 13, 46, 32, 47 }, // 38-M
		{ 48, 24, 14, 25 }, // 38-Q
		{ 42, 15, 32, 16 }, // 38-H
		{ 20, 117, 4, 118 }, // 39-L
		{ 40, 47, 7, 48 }, // 39-M
		{ 43, 24, 22, 25 }, // 39-Q
		{ 10, 15, 67, 16 }, // 39-H
		{ 19, 118, 6, 119 }, // 40-L
		{ 18, 47, 31, 48 }, // 40-M
		{ 34, 24, 34, 25 }, // 40-Q
		{ 20, 15, 61, 16 } // 40-H
	};

	internal static readonly byte[] Generator7 =
		{ 87, 229, 146, 149, 238, 102, 21 };

	internal static readonly byte[] Generator10 =
		{ 251, 67, 46, 61, 118, 70, 64, 94, 32, 45 };

	internal static readonly byte[] Generator13 =
		{ 74, 152, 176, 100, 86, 100, 106, 104, 130, 218, 206, 140, 78 };

	internal static readonly byte[] Generator15 =
		{ 8, 183, 61, 91, 202, 37, 51, 58, 58, 237, 140, 124, 5, 99, 105 };

	internal static readonly byte[] Generator16 =
		{ 120, 104, 107, 109, 102, 161, 76, 3, 91, 191, 147, 169, 182, 194, 225, 120 };

	internal static readonly byte[] Generator17 =
	{
		43, 139, 206, 78, 43, 239, 123, 206, 214, 147, 24, 99, 150, 39, 243, 163,
		136
	};

	internal static readonly byte[] Generator18 =
	{
		215, 234, 158, 94, 184, 97, 118, 170, 79, 187, 152, 148, 252, 179, 5, 98,
		96, 153
	};

	internal static readonly byte[] Generator20 =
	{
		17, 60, 79, 50, 61, 163, 26, 187, 202, 180, 221, 225, 83, 239, 156, 164,
		212, 212, 188, 190
	};

	internal static readonly byte[] Generator22 =
	{
		210, 171, 247, 242, 93, 230, 14, 109, 221, 53, 200, 74, 8, 172, 98, 80,
		219, 134, 160, 105, 165, 231
	};

	internal static readonly byte[] Generator24 =
	{
		229, 121, 135, 48, 211, 117, 251, 126, 159, 180, 169, 152, 192, 226, 228, 218,
		111, 0, 117, 232, 87, 96, 227, 21
	};

	internal static readonly byte[] Generator26 =
	{
		173, 125, 158, 2, 103, 182, 118, 17, 145, 201, 111, 28, 165, 53, 161, 21,
		245, 142, 13, 102, 48, 227, 153, 145, 218, 70
	};

	internal static readonly byte[] Generator28 =
	{
		168, 223, 200, 104, 224, 234, 108, 180, 110, 190, 195, 147, 205, 27, 232, 201,
		21, 43, 245, 87, 42, 195, 212, 119, 242, 37, 9, 123
	};

	internal static readonly byte[] Generator30 =
	{
		41, 173, 145, 152, 216, 31, 179, 182, 50, 48, 110, 86, 239, 96, 222, 125,
		42, 173, 226, 193, 224, 130, 156, 37, 251, 216, 238, 40, 192, 180
	};

	internal static readonly byte[] Generator32 =
	{
		10, 6, 106, 190, 249, 167, 4, 67, 209, 138, 138, 32, 242, 123, 89, 27,
		120, 185, 80, 156, 38, 60, 171, 60, 28, 222, 80, 52, 254, 185, 220, 241
	};

	internal static readonly byte[] Generator34 =
	{
		111, 77, 146, 94, 26, 21, 108, 19, 105, 94, 113, 193, 86, 140, 163, 125,
		58, 158, 229, 239, 218, 103, 56, 70, 114, 61, 183, 129, 167, 13, 98, 62,
		129, 51
	};

	internal static readonly byte[] Generator36 =
	{
		200, 183, 98, 16, 172, 31, 246, 234, 60, 152, 115, 0, 167, 152, 113, 248,
		238, 107, 18, 63, 218, 37, 87, 210, 105, 177, 120, 74, 121, 196, 117, 251,
		113, 233, 30, 120
	};

	internal static readonly byte[] Generator40 =
	{
		59, 116, 79, 161, 252, 98, 128, 205, 128, 161, 247, 57, 163, 56, 235, 106,
		53, 26, 187, 174, 226, 104, 170, 7, 175, 35, 181, 114, 88, 41, 47, 163,
		125, 134, 72, 20, 232, 53, 35, 15
	};

	internal static readonly byte[] Generator42 =
	{
		250, 103, 221, 230, 25, 18, 137, 231, 0, 3, 58, 242, 221, 191, 110, 84,
		230, 8, 188, 106, 96, 147, 15, 131, 139, 34, 101, 223, 39, 101, 213, 199,
		237, 254, 201, 123, 171, 162, 194, 117, 50, 96
	};

	internal static readonly byte[] Generator44 =
	{
		190, 7, 61, 121, 71, 246, 69, 55, 168, 188, 89, 243, 191, 25, 72, 123,
		9, 145, 14, 247, 1, 238, 44, 78, 143, 62, 224, 126, 118, 114, 68, 163,
		52, 194, 217, 147, 204, 169, 37, 130, 113, 102, 73, 181
	};

	internal static readonly byte[] Generator46 =
	{
		112, 94, 88, 112, 253, 224, 202, 115, 187, 99, 89, 5, 54, 113, 129, 44,
		58, 16, 135, 216, 169, 211, 36, 1, 4, 96, 60, 241, 73, 104, 234, 8,
		249, 245, 119, 174, 52, 25, 157, 224, 43, 202, 223, 19, 82, 15
	};

	internal static readonly byte[] Generator48 =
	{
		228, 25, 196, 130, 211, 146, 60, 24, 251, 90, 39, 102, 240, 61, 178, 63,
		46, 123, 115, 18, 221, 111, 135, 160, 182, 205, 107, 206, 95, 150, 120, 184,
		91, 21, 247, 156, 140, 238, 191, 11, 94, 227, 84, 50, 163, 39, 34, 108
	};

	internal static readonly byte[] Generator50 =
	{
		232, 125, 157, 161, 164, 9, 118, 46, 209, 99, 203, 193, 35, 3, 209, 111,
		195, 242, 203, 225, 46, 13, 32, 160, 126, 209, 130, 160, 242, 215, 242, 75,
		77, 42, 189, 32, 113, 65, 124, 69, 228, 114, 235, 175, 124, 170, 215, 232,
		133, 205
	};

	internal static readonly byte[] Generator52 =
	{
		116, 50, 86, 186, 50, 220, 251, 89, 192, 46, 86, 127, 124, 19, 184, 233,
		151, 215, 22, 14, 59, 145, 37, 242, 203, 134, 254, 89, 190, 94, 59, 65,
		124, 113, 100, 233, 235, 121, 22, 76, 86, 97, 39, 242, 200, 220, 101, 33,
		239, 254, 116, 51
	};

	internal static readonly byte[] Generator54 =
	{
		183, 26, 201, 84, 210, 221, 113, 21, 46, 65, 45, 50, 238, 184, 249, 225,
		102, 58, 209, 218, 109, 165, 26, 95, 184, 192, 52, 245, 35, 254, 238, 175,
		172, 79, 123, 25, 122, 43, 120, 108, 215, 80, 128, 201, 235, 8, 153, 59,
		101, 31, 198, 76, 31, 156
	};

	internal static readonly byte[] Generator56 =
	{
		106, 120, 107, 157, 164, 216, 112, 116, 2, 91, 248, 163, 36, 201, 202, 229,
		6, 144, 254, 155, 135, 208, 170, 209, 12, 139, 127, 142, 182, 249, 177, 174,
		190, 28, 10, 85, 239, 184, 101, 124, 152, 206, 96, 23, 163, 61, 27, 196,
		247, 151, 154, 202, 207, 20, 61, 10
	};

	internal static readonly byte[] Generator58 =
	{
		82, 116, 26, 247, 66, 27, 62, 107, 252, 182, 200, 185, 235, 55, 251, 242,
		210, 144, 154, 237, 176, 141, 192, 248, 152, 249, 206, 85, 253, 142, 65, 165,
		125, 23, 24, 30, 122, 240, 214, 6, 129, 218, 29, 145, 127, 134, 206, 245,
		117, 29, 41, 63, 159, 142, 233, 125, 148, 123
	};

	internal static readonly byte[] Generator60 =
	{
		107, 140, 26, 12, 9, 141, 243, 197, 226, 197, 219, 45, 211, 101, 219, 120,
		28, 181, 127, 6, 100, 247, 2, 205, 198, 57, 115, 219, 101, 109, 160, 82,
		37, 38, 238, 49, 160, 209, 121, 86, 11, 124, 30, 181, 84, 25, 194, 87,
		65, 102, 190, 220, 70, 27, 209, 16, 89, 7, 33, 240
	};

	internal static readonly byte[] Generator62 =
	{
		65, 202, 113, 98, 71, 223, 248, 118, 214, 94, 0, 122, 37, 23, 2, 228,
		58, 121, 7, 105, 135, 78, 243, 118, 70, 76, 223, 89, 72, 50, 70, 111,
		194, 17, 212, 126, 181, 35, 221, 117, 235, 11, 229, 149, 147, 123, 213, 40,
		115, 6, 200, 100, 26, 246, 182, 218, 127, 215, 36, 186, 110, 106
	};

	internal static readonly byte[] Generator64 =
	{
		45, 51, 175, 9, 7, 158, 159, 49, 68, 119, 92, 123, 177, 204, 187, 254,
		200, 78, 141, 149, 119, 26, 127, 53, 160, 93, 199, 212, 29, 24, 145, 156,
		208, 150, 218, 209, 4, 216, 91, 47, 184, 146, 47, 140, 195, 195, 125, 242,
		238, 63, 99, 108, 140, 230, 242, 31, 204, 11, 178, 243, 217, 156, 213, 231
	};

	internal static readonly byte[] Generator66 =
	{
		5, 118, 222, 180, 136, 136, 162, 51, 46, 117, 13, 215, 81, 17, 139, 247,
		197, 171, 95, 173, 65, 137, 178, 68, 111, 95, 101, 41, 72, 214, 169, 197,
		95, 7, 44, 154, 77, 111, 236, 40, 121, 143, 63, 87, 80, 253, 240, 126,
		217, 77, 34, 232, 106, 50, 168, 82, 76, 146, 67, 106, 171, 25, 132, 93,
		45, 105
	};

	internal static readonly byte[] Generator68 =
	{
		247, 159, 223, 33, 224, 93, 77, 70, 90, 160, 32, 254, 43, 150, 84, 101,
		190, 205, 133, 52, 60, 202, 165, 220, 203, 151, 93, 84, 15, 84, 253, 173,
		160, 89, 227, 52, 199, 97, 95, 231, 52, 177, 41, 125, 137, 241, 166, 225,
		118, 2, 54, 32, 82, 215, 175, 198, 43, 238, 235, 27, 101, 184, 127, 3,
		5, 8, 163, 238
	};

	internal static readonly byte[][] GenArray =
	{
		Generator7, null, null, Generator10, null, null, Generator13, null, Generator15, Generator16,
		Generator17, Generator18, null, Generator20, null, Generator22, null, Generator24, null, Generator26,
		null, Generator28, null, Generator30, null, Generator32, null, Generator34, null, Generator36,
		null, null, null, Generator40, null, Generator42, null, Generator44, null, Generator46,
		null, Generator48, null, Generator50, null, Generator52, null, Generator54, null, Generator56,
		null, Generator58, null, Generator60, null, Generator62, null, Generator64, null, Generator66,
		null, Generator68
	};

	internal static readonly byte[] ExpToInt = //	ExpToInt =
	{
		1, 2, 4, 8, 16, 32, 64, 128, 29, 58, 116, 232, 205, 135, 19, 38,
		76, 152, 45, 90, 180, 117, 234, 201, 143, 3, 6, 12, 24, 48, 96, 192,
		157, 39, 78, 156, 37, 74, 148, 53, 106, 212, 181, 119, 238, 193, 159, 35,
		70, 140, 5, 10, 20, 40, 80, 160, 93, 186, 105, 210, 185, 111, 222, 161,
		95, 190, 97, 194, 153, 47, 94, 188, 101, 202, 137, 15, 30, 60, 120, 240,
		253, 231, 211, 187, 107, 214, 177, 127, 254, 225, 223, 163, 91, 182, 113, 226,
		217, 175, 67, 134, 17, 34, 68, 136, 13, 26, 52, 104, 208, 189, 103, 206,
		129, 31, 62, 124, 248, 237, 199, 147, 59, 118, 236, 197, 151, 51, 102, 204,
		133, 23, 46, 92, 184, 109, 218, 169, 79, 158, 33, 66, 132, 21, 42, 84,
		168, 77, 154, 41, 82, 164, 85, 170, 73, 146, 57, 114, 228, 213, 183, 115,
		230, 209, 191, 99, 198, 145, 63, 126, 252, 229, 215, 179, 123, 246, 241, 255,
		227, 219, 171, 75, 150, 49, 98, 196, 149, 55, 110, 220, 165, 87, 174, 65,
		130, 25, 50, 100, 200, 141, 7, 14, 28, 56, 112, 224, 221, 167, 83, 166,
		81, 162, 89, 178, 121, 242, 249, 239, 195, 155, 43, 86, 172, 69, 138, 9,
		18, 36, 72, 144, 61, 122, 244, 245, 247, 243, 251, 235, 203, 139, 11, 22,
		44, 88, 176, 125, 250, 233, 207, 131, 27, 54, 108, 216, 173, 71, 142, 1,

		2, 4, 8, 16, 32, 64, 128, 29, 58, 116, 232, 205, 135, 19, 38,
		76, 152, 45, 90, 180, 117, 234, 201, 143, 3, 6, 12, 24, 48, 96, 192,
		157, 39, 78, 156, 37, 74, 148, 53, 106, 212, 181, 119, 238, 193, 159, 35,
		70, 140, 5, 10, 20, 40, 80, 160, 93, 186, 105, 210, 185, 111, 222, 161,
		95, 190, 97, 194, 153, 47, 94, 188, 101, 202, 137, 15, 30, 60, 120, 240,
		253, 231, 211, 187, 107, 214, 177, 127, 254, 225, 223, 163, 91, 182, 113, 226,
		217, 175, 67, 134, 17, 34, 68, 136, 13, 26, 52, 104, 208, 189, 103, 206,
		129, 31, 62, 124, 248, 237, 199, 147, 59, 118, 236, 197, 151, 51, 102, 204,
		133, 23, 46, 92, 184, 109, 218, 169, 79, 158, 33, 66, 132, 21, 42, 84,
		168, 77, 154, 41, 82, 164, 85, 170, 73, 146, 57, 114, 228, 213, 183, 115,
		230, 209, 191, 99, 198, 145, 63, 126, 252, 229, 215, 179, 123, 246, 241, 255,
		227, 219, 171, 75, 150, 49, 98, 196, 149, 55, 110, 220, 165, 87, 174, 65,
		130, 25, 50, 100, 200, 141, 7, 14, 28, 56, 112, 224, 221, 167, 83, 166,
		81, 162, 89, 178, 121, 242, 249, 239, 195, 155, 43, 86, 172, 69, 138, 9,
		18, 36, 72, 144, 61, 122, 244, 245, 247, 243, 251, 235, 203, 139, 11, 22,
		44, 88, 176, 125, 250, 233, 207, 131, 27, 54, 108, 216, 173, 71, 142, 1
	};

	internal static readonly byte[] IntToExp = //	IntToExp =
	{
		0, 0, 1, 25, 2, 50, 26, 198, 3, 223, 51, 238, 27, 104, 199, 75,
		4, 100, 224, 14, 52, 141, 239, 129, 28, 193, 105, 248, 200, 8, 76, 113,
		5, 138, 101, 47, 225, 36, 15, 33, 53, 147, 142, 218, 240, 18, 130, 69,
		29, 181, 194, 125, 106, 39, 249, 185, 201, 154, 9, 120, 77, 228, 114, 166,
		6, 191, 139, 98, 102, 221, 48, 253, 226, 152, 37, 179, 16, 145, 34, 136,
		54, 208, 148, 206, 143, 150, 219, 189, 241, 210, 19, 92, 131, 56, 70, 64,
		30, 66, 182, 163, 195, 72, 126, 110, 107, 58, 40, 84, 250, 133, 186, 61,
		202, 94, 155, 159, 10, 21, 121, 43, 78, 212, 229, 172, 115, 243, 167, 87,
		7, 112, 192, 247, 140, 128, 99, 13, 103, 74, 222, 237, 49, 197, 254, 24,
		227, 165, 153, 119, 38, 184, 180, 124, 17, 68, 146, 217, 35, 32, 137, 46,
		55, 63, 209, 91, 149, 188, 207, 205, 144, 135, 151, 178, 220, 252, 190, 97,
		242, 86, 211, 171, 20, 42, 93, 158, 132, 60, 57, 83, 71, 109, 65, 162,
		31, 45, 67, 216, 183, 123, 164, 118, 196, 23, 73, 236, 127, 12, 111, 246,
		108, 161, 59, 82, 41, 157, 85, 170, 251, 96, 134, 177, 187, 204, 62, 90,
		203, 89, 95, 176, 156, 169, 160, 81, 11, 245, 22, 235, 122, 117, 44, 215,
		79, 174, 213, 233, 230, 231, 173, 232, 116, 214, 244, 234, 168, 80, 88, 175
	};

	internal static readonly int[] FormatInfoArray =
	{
		0x5412, 0x5125, 0x5E7C, 0x5B4B, 0x45F9, 0x40CE, 0x4F97, 0x4AA0, // M = 00
		0x77C4, 0x72F3, 0x7DAA, 0x789D, 0x662F, 0x6318, 0x6C41, 0x6976, // L = 01
		0x1689, 0x13BE, 0x1CE7, 0x19D0, 0x762, 0x255, 0xD0C, 0x83B, // H - 10
		0x355F, 0x3068, 0x3F31, 0x3A06, 0x24B4, 0x2183, 0x2EDA, 0x2BED // Q = 11
	};

	internal static readonly int[,] FormatInfoOne = new[,]
	{
		{ 0, 8 }, { 1, 8 }, { 2, 8 }, { 3, 8 }, { 4, 8 }, { 5, 8 }, { 7, 8 }, { 8, 8 },
		{ 8, 7 }, { 8, 5 }, { 8, 4 }, { 8, 3 }, { 8, 2 }, { 8, 1 }, { 8, 0 }
	};

	internal static readonly int[,] FormatInfoTwo = new[,]
	{
		{ 8, -1 }, { 8, -2 }, { 8, -3 }, { 8, -4 }, { 8, -5 }, { 8, -6 }, { 8, -7 }, { 8, -8 },
		{ -7, 8 }, { -6, 8 }, { -5, 8 }, { -4, 8 }, { -3, 8 }, { -2, 8 }, { -1, 8 }
	};

	internal static readonly int[] VersionCodeArray =
	{
		0x7c94, 0x85bc, 0x9a99, 0xa4d3, 0xbbf6, 0xc762, 0xd847, 0xe60d, 0xf928, 0x10b78,
		0x1145d, 0x12a17, 0x13532, 0x149a6, 0x15683, 0x168c9, 0x177ec, 0x18ec4, 0x191e1, 0x1afab,
		0x1b08e, 0x1cc1a, 0x1d33f, 0x1ed75, 0x1f250, 0x209d5, 0x216f0, 0x228ba, 0x2379f, 0x24b0b,
		0x2542e, 0x26a64, 0x27541, 0x28c69
	};

	internal const byte White = 0;
	internal const byte Black = 1;
	internal const byte NonData = 2;
	internal const byte Fixed = 4;
	internal const byte DataWhite = White;
	internal const byte DataBlack = Black;
	internal const byte FormatWhite = NonData | White;
	internal const byte FormatBlack = NonData | Black;
	internal const byte FixedWhite = Fixed | NonData | White;
	internal const byte FixedBlack = Fixed | NonData | Black;

	internal static readonly byte[,] FinderPatternTopLeft =
	{
		{ FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FormatWhite },
		{ FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FormatWhite },
		{
			FormatWhite, FormatWhite, FormatWhite, FormatWhite, FormatWhite, FormatWhite, FormatWhite, FormatWhite,
			FormatWhite
		}
	};

	internal static readonly byte[,] FinderPatternTopRight =
	{
		{ FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack },
		{ FixedWhite, FixedBlack, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedBlack },
		{ FixedWhite, FixedBlack, FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FixedBlack },
		{ FixedWhite, FixedBlack, FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FixedBlack },
		{ FixedWhite, FixedBlack, FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FixedBlack },
		{ FixedWhite, FixedBlack, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedBlack },
		{ FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack },
		{ FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite },
		{ FormatWhite, FormatWhite, FormatWhite, FormatWhite, FormatWhite, FormatWhite, FormatWhite, FormatWhite }
	};

	internal static readonly byte[,] FinderPatternBottomLeft =
	{
		{ FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedBlack },
		{ FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedWhite, FixedBlack, FixedWhite, FormatWhite },
		{ FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedWhite, FormatWhite }
	};

	internal static readonly byte[,] AlignmentPattern =
	{
		{ FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack },
		{ FixedBlack, FixedWhite, FixedWhite, FixedWhite, FixedBlack },
		{ FixedBlack, FixedWhite, FixedBlack, FixedWhite, FixedBlack },
		{ FixedBlack, FixedWhite, FixedWhite, FixedWhite, FixedBlack },
		{ FixedBlack, FixedBlack, FixedBlack, FixedBlack, FixedBlack }
	};
}