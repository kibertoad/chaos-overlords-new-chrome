namespace Rechaos.OriginalProbe;

// The copies of the drawing area that a shot or an endpoint capture writes.
internal sealed partial class NewGameSession
{
    // RULE-GFX-002: the 640-by-460 drawing area starts at the client area's top-left corner. The
    // capture is written twice from the window's device context. PrintWindow is unsuitable here:
    // it can repaint over animation drawn directly to the window rather than its backing surface.
    // The copies are <file>.bmp and <file>-repeat.bmp; the result is the marker frame and the
    // pump's counter they show, with the control lights' bytes and the selected sector, null when
    // no two agreeing copies were taken.
    private (int MarkerFrame, int PumpCounter, int[] Lamps, int SelectedSector)? CaptureDrawingArea(
        IntPtr window, string file)
    {
        const int width = CaptureFixture.Width, height = CaptureFixture.Height;
        if (!ClientAreaHoldsDrawingArea(window)) return null;
        // FND-UI-038: the counter increments after drawing. Require two agreeing window copies
        // and a stable counter; a repainting capture cannot use this frame relationship. The
        // pump's counter, which picks the selected-sector frame and the lights' blink phase
        // (FND-UI-017, FND-EVENT-006), is kept as read: the frame on screen is the one drawn
        // for the counter less one (FND-UI-048).
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var before = BitConverter.ToInt16(_process.Read(OriginalAddresses.MarkerCounter, 2));
            var pumpBefore = _process.ReadInt32(OriginalAddresses.PumpCounter);
            var copiesAgree = CaptureDrawingArea(window, file, width, height);
            var after = BitConverter.ToInt16(_process.Read(OriginalAddresses.MarkerCounter, 2));
            var pumpAfter = _process.ReadInt32(OriginalAddresses.PumpCounter);
            if (before != after || pumpBefore != pumpAfter || !copiesAgree) continue;
            // FND-EVENT-006: whether each light is wanted and whether the pump last drew it lit.
            int[] lamps =
            [
                _process.Read(OriginalAddresses.EventsPending, 1)[0],
                _process.Read(OriginalAddresses.EventsLampDrawn, 1)[0],
                _process.Read(OriginalAddresses.ComlinkPending, 1)[0],
                _process.Read(OriginalAddresses.ComlinkLampDrawn, 1)[0],
            ];
            // FND-SAVE-003: the sector the city frames and the console's sector values show.
            return ((before + 11) % 12, pumpBefore, lamps, _process.ReadInt32(OriginalAddresses.SelectedSector));
        }
        _notes.Add($"Capture {file} rejected: a counter moved or the synchronized copies disagreed.");
        return null;
    }

    private bool CaptureDrawingArea(IntPtr window, string file, int width, int height)
    {
        byte[]? firstCopy = null;
        var copiesAgree = false;
        foreach (var name in new[] { file + ".bmp", file + "-repeat.bmp" })
        {
            var info = new byte[40];
            BitConverter.GetBytes(40).CopyTo(info, 0);
            BitConverter.GetBytes(width).CopyTo(info, 4);
            BitConverter.GetBytes(-height).CopyTo(info, 8);
            BitConverter.GetBytes((short)1).CopyTo(info, 12);
            BitConverter.GetBytes((short)32).CopyTo(info, 14);
            var screen = Native.GetDC(window);
            var memory = IntPtr.Zero;
            var bitmap = IntPtr.Zero;
            var old = IntPtr.Zero;
            try
            {
                if (screen == IntPtr.Zero) throw new InvalidOperationException("Cannot acquire the capture window DC.");
                if (firstCopy is null)
                {
                    const int bitsPixel = 12, planes = 14;
                    var hostDepth = Native.GetDeviceCaps(screen, bitsPixel) * Native.GetDeviceCaps(screen, planes);
                    var gameDepth = _process.ReadInt32(OriginalAddresses.DisplayDepth);
                    _notes.Add($"Capture depths: original records {gameDepth}; probe window DC reports {hostDepth}.");
                    if (gameDepth != hostDepth)
                        _notes.Add(settings.WhiteKey
                            ? "Capture depth mismatch: --white-key passed RGB(255,255,255) for the 16-bit key (FND-PLATFORM-014)."
                            : "Capture depth mismatch: evaluate colour-key conversion before accepting presentation evidence.");
                }
                memory = Native.CreateCompatibleDC(screen);
                if (memory == IntPtr.Zero) throw new InvalidOperationException("Cannot create the capture memory DC.");
                bitmap = Native.CreateDIBSection(screen, info, 0, out var bits, IntPtr.Zero, 0);
                if (bitmap == IntPtr.Zero || bits == IntPtr.Zero)
                    throw new InvalidOperationException("Cannot allocate the capture bitmap.");
                old = Native.SelectObject(memory, bitmap);
                if (old == IntPtr.Zero || old == new IntPtr(-1))
                    throw new InvalidOperationException("Cannot select the capture bitmap.");
                var ok = Native.BitBlt(memory, 0, 0, width, height, screen, 0, 0, 0x00CC0020);
                if (!ok) throw new InvalidOperationException($"{name}: the copy failed.");
                // CreateDIBSection requires GDI drawing to finish before its bits are read directly.
                // https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection
                if (!Native.GdiFlush()) throw new InvalidOperationException($"{name}: flushing the copy failed.");
                var pixels = new byte[width * height * 4];
                System.Runtime.InteropServices.Marshal.Copy(bits, pixels, 0, pixels.Length);
                if (firstCopy is null) firstCopy = pixels;
                else copiesAgree = firstCopy.AsSpan().SequenceEqual(pixels);
                WriteBitmap(Path.Combine(outputDirectory, name), width, height, pixels);
            }
            finally
            {
                if (old != IntPtr.Zero && old != new IntPtr(-1)) Native.SelectObject(memory, old);
                if (bitmap != IntPtr.Zero) Native.DeleteObject(bitmap);
                if (memory != IntPtr.Zero) Native.DeleteDC(memory);
                if (screen != IntPtr.Zero) Native.ReleaseDC(window, screen);
            }
        }
        return copiesAgree;
    }

    private static void WriteBitmap(string path, int width, int height, byte[] topDownBgra)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B'); writer.Write((byte)'M');
        writer.Write(54 + topDownBgra.Length); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(width); writer.Write(-height);
        writer.Write((short)1); writer.Write((short)32); writer.Write(0);
        writer.Write(topDownBgra.Length); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        writer.Write(topDownBgra);
    }
}
