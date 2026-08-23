using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ZXing;
using ZXing.QrCode;

namespace XBVault.Services;

public static class QRCodeService
{
    public static Bitmap? GenerateQrBitmap(string text, int size = 250)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var hints = new Dictionary<EncodeHintType, object>
            {
                { EncodeHintType.ERROR_CORRECTION, 'M' },
                { EncodeHintType.MARGIN, 1 },
                { EncodeHintType.CHARACTER_SET, "UTF-8" }
            };

            var writer = new QRCodeWriter();
            var matrix = writer.encode(text, BarcodeFormat.QR_CODE, size, size, hints);

            var moduleCount = matrix.Width;
            var moduleSize = Math.Max(1, size / moduleCount);
            var totalSize = moduleCount * moduleSize;
            var stride = totalSize * 4;
            var buffer = new byte[stride * totalSize];

            for (int y = 0; y < moduleCount; y++)
            {
                for (int my = 0; my < moduleSize; my++)
                {
                    var dstRow = y * moduleSize + my;
                    if (dstRow >= totalSize) break;

                    for (int x = 0; x < moduleCount; x++)
                    {
                        var isBlack = matrix[x, y];
                        var color = isBlack ? (byte)0 : (byte)255;
                        for (int mx = 0; mx < moduleSize; mx++)
                        {
                            var col = x * moduleSize + mx;
                            if (col >= totalSize) break;
                            var offset = dstRow * stride + col * 4;
                            buffer[offset] = color;     // B
                            buffer[offset + 1] = color; // G
                            buffer[offset + 2] = color; // R
                            buffer[offset + 3] = 255;   // A
                        }
                    }
                }
            }

            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    return new WriteableBitmap(
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul,
                        (IntPtr)ptr,
                        new PixelSize(totalSize, totalSize),
                        new Vector(96, 96),
                        stride);
                }
            }
        }
        catch
        {
            return null;
        }
    }
}
