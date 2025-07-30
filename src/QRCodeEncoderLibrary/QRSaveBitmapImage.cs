/////////////////////////////////////////////////////////////////////
//
//	QR Code Encoder Library
//
//	QR Save image.
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
//	For full version history please look at QREncoder.cs
/////////////////////////////////////////////////////////////////////

using SkiaSharp;

namespace QRCodeEncoderLibrary;

/// <summary>
///     Save QR Code image as Bitmap class
/// </summary>
public class QRSaveBitmapImage
{
	/// <summary>
	///     QRCode dimension
	/// </summary>
	private readonly int _qrCodeDimension;

	/// <summary>
	///     QR code matrix (no quiet zone)
	///     Black module = true, White module = false
	/// </summary>
	private readonly bool[,] _qrCodeMatrix;

	private int _moduleSize = 2;
	private int _quietZone = 8;

	/// <summary>
	///     Gets QR Code image dimension
	/// </summary>
	private int _qrCodeImageDimension;

	/// <summary>
	///     Save QR Code Bitmap image constructor
	/// </summary>
	/// <param name="qrCodeMatrix">QR code matrix</param>
	public QRSaveBitmapImage
	(
		bool[,] qrCodeMatrix
	)
	{
		// test argument
		if (qrCodeMatrix == null)
			throw new ArgumentException("QRSaveBitmapImage: QRCodeMatrix is null");

		// test matrix dimensions
		var width = qrCodeMatrix.GetLength(0);
		var height = qrCodeMatrix.GetLength(1);
		if (width != height)
			throw new ArgumentException("QRSaveBitmapImage: QRCodeMatrix width and height are not equal");
		if (width < 21 || width > 177 || (width - 21) % 4 != 0)
			throw new ArgumentException("QRSaveBitmapImage: Invalid QRCodeMatrix dimension");

		// save argument
		this._qrCodeMatrix = qrCodeMatrix;
		_qrCodeDimension = width;
	}

	/// <summary>
	///     Module size (Default: 2)
	/// </summary>
	public int ModuleSize
	{
		get => _moduleSize;
		set
		{
			if (value < 1 || value > 100)
				throw new ArgumentException("Module size error. Default is 2.");
			_moduleSize = value;
		}
	}

	/// <summary>
	///     Quiet zone around the barcode in pixels (Default: 8)
	///     It should be 4 times the module size.
	///     However the calling application can set it 0 to 400
	/// </summary>
	public int QuietZone
	{
		get => _quietZone;
		set
		{
			if (value < 0 || value > 400)
				throw new ArgumentException("Quiet zone must be 0 to 400. Default is 8.");
			_quietZone = value;
		}
	}

	/// <summary>
	///     White brush (default white)
	/// </summary>
	public SKColor WhiteBrush { get; set; } = SKColors.White;

	/// <summary>
	///     Black brush (default black)
	/// </summary>
	public SKColor BlackBrush { get; set; } = SKColors.Black;

	/// <summary>
	///     Create QR Code Bitmap image from boolean black and white matrix
	/// </summary>
	/// <returns>QRCode image</returns>
	public SKBitmap CreateQRCodeBitmap()
	{
		// image dimension
		_qrCodeImageDimension = ModuleSize * _qrCodeDimension + 2 * QuietZone;

		// create picture object and make it white
		SKBitmap image = new(_qrCodeImageDimension, _qrCodeImageDimension);
		using var canvas = new SKCanvas(image);
		canvas.Clear(WhiteBrush);

		// x and y image pointers
		var xOffset = QuietZone;
		var yOffset = QuietZone;

		// convert result matrix to output matrix
		for (var row = 0; row < _qrCodeDimension; row++)
		{
			for (var col = 0; col < _qrCodeDimension; col++)
			{
				// bar is black
				if (_qrCodeMatrix[row, col])
					canvas.DrawRect(xOffset, yOffset, ModuleSize, ModuleSize, new SKPaint { Color = BlackBrush } );
				xOffset += ModuleSize;
			}

			xOffset = QuietZone;
			yOffset += ModuleSize;
		}

		// return image
		return image;
	}

	/// <summary>
	///     Save QRCode image to image file
	/// </summary>
	/// <param name="fileName">Image file name</param>
	public void SaveQRCodeToImageFile
	(
		string fileName,
		SKEncodedImageFormat format
	)
	{
		// exceptions
		if (fileName == null)
			throw new ArgumentException("SaveQRCodeToPngFile: FileName is null");

		// create Bitmap
		var imageBitmap = CreateQRCodeBitmap();
		
		// save bitmap
		var data = imageBitmap.Encode(format, 100);
		using var fileStream = File.OpenWrite(fileName);
		data.SaveTo(fileStream);;
	}

	/// <summary>
	///     Save QRCode image to image file
	/// </summary>
	/// <param name="FileName">Image file name</param>
	public void SaveQRCodeToImageFile
	(
		Stream outputStream,
		SKEncodedImageFormat format
	)
	{
		// exceptions
		if (outputStream == null)
			throw new ArgumentException("SaveQRCodeToImageFile: Output stream is null");

		// create Bitmap
		var imageBitmap = CreateQRCodeBitmap();

		// write to stream 
		var data = imageBitmap.Encode(format, 100);
		data.SaveTo(outputStream);

		// flush all buffers
		outputStream.Flush();
	}
}