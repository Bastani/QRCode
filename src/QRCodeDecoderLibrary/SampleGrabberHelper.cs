/////////////////////////////////////////////////////////////////////
//
//	QR Code Decoder Library
//
//	Video camera sample grabber helper
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

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace QRCodeDecoderLibrary;

/// <summary>
///     Helper for SampleGrabber. Used to make screenshots (snapshots).
/// </summary>
/// <remarks>This class is inherited from <see cref="ISampleGrabberCB" /> class.</remarks>
/// <author> free5lot (free5lot@yandex.ru) </author>
/// <version> 2013.10.17 </version>
internal sealed class SampleGrabberHelper : ISampleGrabberCB, IDisposable
{
	/// <summary>
	///     Flag means should helper store (buffer) samples of current frame or not.
	/// </summary>
	private readonly bool _mBBufferSamplesOfCurrentFrame;

	/// <summary>
	///     Flag to wait for the async job to finish.
	/// </summary>
	private readonly ManualResetEvent _mPictureReady;

	/// <summary>
	///     Flag indicates we want to store a frame.
	/// </summary>
	private volatile bool _mBWantOneFrame;

	/// <summary>
	///     Size of frame in bytes.
	/// </summary>
	private int _mImageSize;

	/// <summary>
	///     Buffer for bitmap data.  Always release by caller.
	/// </summary>
	private IntPtr _mIpBuffer = IntPtr.Zero;

	/// <summary>
	///     Pointer to COM-interface ISampleGrabber.
	/// </summary>
	private ISampleGrabber _mSampleGrabber;

	/// <summary>
	///     Video frame bits per pixel.
	/// </summary>
	private int _mVideoBitCount;

	/// <summary>
	///     Video frame height. Calculated once in constructor for perf.
	/// </summary>
	private int _mVideoHeight;

	/// <summary>
	///     Video frame width. Calculated once in constructor for perf.
	/// </summary>
	private int _mVideoWidth;

	/// <summary>
	///     Default constructor for <see cref="SampleGrabberHelper" /> class.
	/// </summary>
	/// <param name="sampleGrabber">Pointer to COM-interface ISampleGrabber.</param>
	/// <param name="buffer_samples_of_current_frame">Flag means should helper store (buffer) samples of current frame or not.</param>
	public SampleGrabberHelper(ISampleGrabber sampleGrabber, bool bufferSamplesOfCurrentFrame)
	{
		_mSampleGrabber = sampleGrabber;
		_mBBufferSamplesOfCurrentFrame = bufferSamplesOfCurrentFrame;

		// tell the callback to ignore new images
		_mPictureReady = new ManualResetEvent(false);
	}

	/// <summary>
	///     Disposes object and snapshot.
	/// </summary>
	public void Dispose()
	{
		if (_mPictureReady != null) _mPictureReady.Close();
		_mSampleGrabber = null;
	}

	/// <summary>
	///     SampleCB callback (NOT USED). It should be implemented for ISampleGrabberCB
	/// </summary>
	int ISampleGrabberCB.SampleCB(double sampleTime, IMediaSample pSample)
	{
		Marshal.ReleaseComObject(pSample);
		return 0;
	}

	/// <summary>
	///     BufferCB callback
	/// </summary>
	/// <remarks>COULD BE EXECUTED FROM FOREIGN THREAD.</remarks>
	int ISampleGrabberCB.BufferCB(double sampleTime, IntPtr pBuffer, int bufferLen)
	{
		// Note that we depend on only being called once per call to Click.  Otherwise
		// a second call can overwrite the previous image.
		Debug.Assert(bufferLen == Math.Abs(_mVideoBitCount / 8 * _mVideoWidth) * _mVideoHeight,
			"Incorrect buffer length");

		if (_mBWantOneFrame)
		{
			_mBWantOneFrame = false;
			Debug.Assert(_mIpBuffer != IntPtr.Zero, "Unitialized buffer");

			// Save the buffer
			CopyMemory(_mIpBuffer, pBuffer, bufferLen);

			// Picture is ready.
			_mPictureReady.Set();
		}

		return 0;
	}

	/// <summary>
	///     Configures mode (mediatype, format type and etc).
	/// </summary>
	public void ConfigureMode()
	{
		AmMediaType media = new();

		// Set the media type to Video/RBG24
		media.majorType = MediaType.Video;
		media.subType = MediaSubType.Rgb24;
		media.formatType = FormatType.VideoInfo;
		var hr = _mSampleGrabber.SetMediaType(media);
		DsError.ThrowExceptionForHr(hr);

		DsUtils.FreeAmMediaType(media);

		// Configure the samplegrabber

		// To save current frame via SnapshotNextFrame
		// ISampleGrabber::SetCallback method
		// Note  [Deprecated. This API may be removed from future releases of Windows.]
		// http://msdn.microsoft.com/en-us/library/windows/desktop/dd376992%28v=vs.85%29.aspx
		hr = _mSampleGrabber.SetCallback(this,
			1); // 1 == WhichMethodToCallback, call the ISampleGrabberCB::BufferCB method
		DsError.ThrowExceptionForHr(hr);

		// To save current frame via SnapshotCurrentFrame
		if (_mBBufferSamplesOfCurrentFrame)
		{
			//ISampleGrabber::SetBufferSamples method
			// Note  [Deprecated. This API may be removed from future releases of Windows.]
			// http://msdn.microsoft.com/en-us/windows/dd376991
			hr = _mSampleGrabber.SetBufferSamples(true);
			DsError.ThrowExceptionForHr(hr);
		}
	}

	/// <summary>
	///     Gets and saves mode (mediatype, format type and etc).
	/// </summary>
	public void SaveMode()
	{
		// Get the media type from the SampleGrabber
		AmMediaType media = new();

		var hr = _mSampleGrabber.GetConnectedMediaType(media);
		DsError.ThrowExceptionForHr(hr);

		if (media.formatType != FormatType.VideoInfo || media.formatPtr == IntPtr.Zero)
			throw new NotSupportedException("Unknown Grabber Media Format");

		// Grab the size info
		var videoInfoHeader = (VideoInfoHeader)Marshal.PtrToStructure(media.formatPtr, typeof(VideoInfoHeader));
		_mVideoWidth = videoInfoHeader.BmiHeader.Width;
		_mVideoHeight = videoInfoHeader.BmiHeader.Height;
		_mVideoBitCount = videoInfoHeader.BmiHeader.BitCount;
		_mImageSize = videoInfoHeader.BmiHeader.ImageSize;

		DsUtils.FreeAmMediaType(media);
	}

	[DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
	public static extern void CopyMemory(IntPtr destination, IntPtr source, [MarshalAs(UnmanagedType.U4)] int length);

	/// <summary>
	///     Makes a snapshot of next frame
	/// </summary>
	/// <returns>Bitmap with snapshot</returns>
	public Bitmap SnapshotNextFrame()
	{
		if (_mSampleGrabber == null) throw new ApplicationException("SampleGrabber was not initialized");

		// capture image
		var ip = GetNextFrame();

		if (ip == IntPtr.Zero) throw new ApplicationException("Can not snap next frame");

		var pixelFormat = _mVideoBitCount switch
		{
			24 => PixelFormat.Format24bppRgb,
			32 => PixelFormat.Format32bppRgb,
			48 => PixelFormat.Format48bppRgb,
			_ => throw new ApplicationException("Unsupported BitCount")
		};

		Bitmap bitmap = new(_mVideoWidth, _mVideoHeight, _mVideoBitCount / 8 * _mVideoWidth, pixelFormat, ip);

		var bitmapClone = bitmap.Clone(new Rectangle(0, 0, _mVideoWidth, _mVideoHeight), PixelFormat.Format24bppRgb);
		bitmapClone.RotateFlip(RotateFlipType.RotateNoneFlipY);

		// Release any previous buffer
		if (ip != IntPtr.Zero) Marshal.FreeCoTaskMem(ip);

		bitmap.Dispose();

		return bitmapClone;
	}

	/// <summary>
	///     Makes a snapshot of current frame
	/// </summary>
	/// <returns>Bitmap with snapshot</returns>
	public Bitmap SnapshotCurrentFrame()
	{
		if (_mSampleGrabber == null) throw new ApplicationException("SampleGrabber was not initialized");

		if (!_mBBufferSamplesOfCurrentFrame)
			throw new ApplicationException(
				"SampleGrabberHelper was created without buffering-mode (buffer of current frame)");

		// capture image
		var ip = GetCurrentFrame();

		var pixelFormat = _mVideoBitCount switch
		{
			24 => PixelFormat.Format24bppRgb,
			32 => PixelFormat.Format32bppRgb,
			48 => PixelFormat.Format48bppRgb,
			_ => throw new ApplicationException("Unsupported BitCount")
		};
		Bitmap bitmap = new(_mVideoWidth, _mVideoHeight, _mVideoBitCount / 8 * _mVideoWidth, pixelFormat, ip);

		var bitmapClone = bitmap.Clone(new Rectangle(0, 0, _mVideoWidth, _mVideoHeight), PixelFormat.Format24bppRgb);
		bitmapClone.RotateFlip(RotateFlipType.RotateNoneFlipY);


		// Release any previous buffer
		if (ip != IntPtr.Zero) Marshal.FreeCoTaskMem(ip);

		bitmap.Dispose();

		return bitmapClone;
	}

	/// <summary>
	///     Get the image from the Still pin.  The returned image can turned into a bitmap with
	///     Bitmap b = new Bitmap(cam.Width, cam.Height, cam.Stride, PixelFormat.Format24bppRgb, m_ip);
	///     If the image is upside down, you can fix it with
	///     b.RotateFlip(RotateFlipType.RotateNoneFlipY);
	/// </summary>
	/// <returns>Returned pointer to be freed by caller with Marshal.FreeCoTaskMem</returns>
	private IntPtr GetNextFrame()
	{
		// get ready to wait for new image
		_mPictureReady.Reset();
		_mIpBuffer = Marshal.AllocCoTaskMem(Math.Abs(_mVideoBitCount / 8 * _mVideoWidth) * _mVideoHeight);

		try
		{
			_mBWantOneFrame = true;

			// Start waiting
			if (!_mPictureReady.WaitOne(5000, false))
				throw new ApplicationException("Timeout while waiting to get a snapshot");
		}
		catch
		{
			Marshal.FreeCoTaskMem(_mIpBuffer);
			_mIpBuffer = IntPtr.Zero;
			throw;
		}

		// Got one
		return _mIpBuffer;
	}

	/// <summary>
	///     Grab a snapshot of the most recent image played.
	///     Returns A pointer to the raw pixel data.
	///     Caller must release this memory with Marshal.FreeCoTaskMem when it is no longer needed.
	/// </summary>
	/// <returns>A pointer to the raw pixel data</returns>
	private IntPtr GetCurrentFrame()
	{
		if (!_mBBufferSamplesOfCurrentFrame)
			throw new ApplicationException(
				"SampleGrabberHelper was created without buffering-mode (buffer of current frame)");

		var ip = IntPtr.Zero;
		var iBuffSize = 0;

		// Read the buffer size
		var hr = _mSampleGrabber.GetCurrentBuffer(ref iBuffSize, ip);
		DsError.ThrowExceptionForHr(hr);

		Debug.Assert(iBuffSize == _mImageSize, "Unexpected buffer size");

		// Allocate the buffer and read it
		ip = Marshal.AllocCoTaskMem(iBuffSize);

		hr = _mSampleGrabber.GetCurrentBuffer(ref iBuffSize, ip);
		DsError.ThrowExceptionForHr(hr);

		return ip;
	}
}