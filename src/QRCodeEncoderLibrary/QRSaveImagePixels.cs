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
//	For full version history please look at QREncoder.cs
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
/////////////////////////////////////////////////////////////////////

namespace QRCodeEncoderLibrary;

/// <summary>
///     Convert QR code matrix to boolean image class
/// </summary>
public class QRSaveImagePixels
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
	///     Convert QR code matrix to boolean image constructor
	/// </summary>
	public QRSaveImagePixels
	(
		bool[,] qrCodeMatrix
	)
	{
		// test argument
		if (qrCodeMatrix == null)
			throw new ArgumentException("QRSaveImagePixels: QRCodeMatrix is null");

		// test matrix dimensions
		var width = qrCodeMatrix.GetLength(0);
		var height = qrCodeMatrix.GetLength(1);
		if (width != height)
			throw new ArgumentException("QRSaveImagePixels: QRCodeMatrix width is not equals height");
		if (width < 21 || width > 177 || (width - 21) % 4 != 0)
			throw new ArgumentException("QRSaveImagePixels: Invalid QRCodeMatrix dimension");

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
				throw new ArgumentException("QRSaveImagePixels: Module size error. Default is 2.");
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
				throw new ArgumentException("QRSaveImagePixels: Quiet zone must be 0 to 400. Default is 8.");
			_quietZone = value;
		}
	}

	/// <summary>
	///     convert black and white matrix to black and white image
	/// </summary>
	/// <returns>Black and white image in pixels</returns>
	public bool[,] ConvertQRCodeMatrixToPixels()
	{
		var qrCodeImageDimension = _moduleSize * _qrCodeDimension + 2 * _quietZone;

		// output matrix size in pixels all matrix elements are white (false)
		var bwImage = new bool[qrCodeImageDimension, qrCodeImageDimension];

		// quiet zone offset
		var xOffset = _quietZone;
		var yOffset = _quietZone;

		// convert result matrix to output matrix
		for (var row = 0; row < _qrCodeDimension; row++)
		{
			for (var col = 0; col < _qrCodeDimension; col++)
			{
				// bar is black
				if (_qrCodeMatrix[row, col])
					for (var y = 0; y < ModuleSize; y++)
					for (var x = 0; x < ModuleSize; x++)
						bwImage[yOffset + y, xOffset + x] = true;

				xOffset += ModuleSize;
			}

			xOffset = _quietZone;
			yOffset += ModuleSize;
		}

		return bwImage;
	}
}