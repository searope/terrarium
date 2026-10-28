//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------


using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using AlphaMode = Vortice.DCommon.AlphaMode;
using Bitmap = System.Drawing.Bitmap;
using PixelFormat = Vortice.DCommon.PixelFormat;

namespace Terrarium.Renderer.DirectX
{
    /// <summary>
    ///  A drawing surface backed by a Direct2D bitmap.  It keeps the shape of
    ///  the DirectDraw surface it replaces: blits between surfaces, color
    ///  fills and line drawing, plus DirectWrite text in place of GDI calls
    ///  on the surface's device context.
    /// </summary>
    public class DirectDrawSurface : IDisposable
    {
        private static readonly BitmapProperties1 SurfaceProperties =
            new BitmapProperties1(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), 96, 96,
                                  BitmapOptions.Target);

        private ID2D1Bitmap1 bitmap;

        private int generation;

        private String image;

        private RECT rect;

        /// <summary>
        ///  Creates an empty, transparent surface of the given size.
        /// </summary>
        public DirectDrawSurface(int x, int y)
        {
            // Direct2D can't create an empty bitmap, and the view asks for
            // its surfaces before the world (and so their size) exists
            rect.Right = Math.Max(1, x);
            rect.Bottom = Math.Max(1, y);
            image = "";

            CreateSurface();
        }

        /// <summary>
        ///  Creates a surface from a bitmap file.  Magenta pixels become
        ///  transparent, standing in for DirectDraw's source color key.
        /// </summary>
        public DirectDrawSurface(String imagePath)
        {
            image = imagePath;
            CreateSurface();
        }

        /// <summary>
        ///  Wraps a bitmap created elsewhere, such as a swap chain back buffer.
        /// </summary>
        internal DirectDrawSurface(ID2D1Bitmap1 bitmap)
        {
            this.bitmap = bitmap;
            generation = ManagedDirectX.Generation;
            image = "";

            var size = bitmap.PixelSize;
            rect.Right = size.Width;
            rect.Bottom = size.Height;
        }

        /// <summary>
        ///  The bitmap file the surface was loaded from.  Setting it reloads
        ///  the surface.
        /// </summary>
        public String ImagePath
        {
            get { return image; }
            set
            {
                image = value;
                CreateSurface();
            }
        }

        /// <summary>
        ///  The size of the surface, as a rectangle at the origin.
        /// </summary>
        public RECT Rect
        {
            get { return rect; }
        }

        /// <summary>
        ///  The Direct2D bitmap holding the surface's pixels.
        /// </summary>
        public ID2D1Bitmap1 Bitmap
        {
            get { return bitmap; }
        }

        /// <summary>
        ///  True when the surface has no bitmap or its bitmap belongs to a
        ///  device that was lost or replaced.
        /// </summary>
        public bool IsLost
        {
            get { return bitmap == null || generation != ManagedDirectX.Generation || ManagedDirectX.DeviceLost; }
        }

        /// <summary>
        ///  Releases the surface's bitmap.
        /// </summary>
        public void Dispose()
        {
            ManagedDirectX.ReleaseTarget(this);
            if (bitmap != null)
            {
                bitmap.Dispose();
                bitmap = null;
            }
        }

        /// <summary>
        ///  Recreates the surface when it has been lost.
        /// </summary>
        public void RestoreSurface()
        {
            if (IsLost)
            {
                CreateSurface();
            }
        }

        /// <summary>
        ///  Copies a rectangle of another surface into a rectangle of this one,
        ///  stretching it when the sizes differ.  Transparent source pixels
        ///  leave this surface unchanged.
        /// </summary>
        public void Blt(RECT destination, DirectDrawSurface source, RECT sourceRect)
        {
            if (source == null || source.bitmap == null || bitmap == null)
            {
                return;
            }

            var context = ManagedDirectX.BeginDraw(this);
            context.DrawBitmap(source.bitmap, destination.ToRectangleF(), 1.0f,
                               BitmapInterpolationMode.NearestNeighbor, sourceRect.ToRectangleF());
        }

        /// <summary>
        ///  Copies a rectangle of another surface to the given position.
        /// </summary>
        public void BltFast(int x, int y, DirectDrawSurface source, RECT sourceRect)
        {
            var destination = new RECT();
            destination.Left = x;
            destination.Top = y;
            destination.Right = x + (sourceRect.Right - sourceRect.Left);
            destination.Bottom = y + (sourceRect.Bottom - sourceRect.Top);

            Blt(destination, source, sourceRect);
        }

        /// <summary>
        ///  Fills a rectangle with a color, replacing what was there (alpha
        ///  included).  An empty rectangle fills the whole surface.
        /// </summary>
        public void ColorFill(RECT area, Color color)
        {
            if (bitmap == null)
            {
                return;
            }

            var context = ManagedDirectX.BeginDraw(this);
            var fill = ManagedDirectX.ToColor4(color);

            if (area.IsEmpty)
            {
                context.Clear(fill);
                return;
            }

            context.PushAxisAlignedClip(new RawRectF(area.Left, area.Top, area.Right, area.Bottom),
                                        AntialiasMode.Aliased);
            context.Clear(fill);
            context.PopAxisAlignedClip();
        }

        /// <summary>
        ///  Draws a one pixel line between two pixels.
        /// </summary>
        public void DrawLine(int x1, int y1, int x2, int y2, Color color)
        {
            if (bitmap == null)
            {
                return;
            }

            // Pixel centers sit at half coordinates in Direct2D
            var context = ManagedDirectX.BeginDraw(this);
            context.DrawLine(new Vector2(x1 + 0.5f, y1 + 0.5f), new Vector2(x2 + 0.5f, y2 + 0.5f),
                             ManagedDirectX.GetBrush(color));
        }

        /// <summary>
        ///  Draws a one pixel outline just inside the given rectangle.
        /// </summary>
        public void DrawBox(int left, int top, int right, int bottom, Color color)
        {
            if (bitmap == null)
            {
                return;
            }

            var context = ManagedDirectX.BeginDraw(this);
            context.DrawRectangle(new RawRectF(left + 0.5f, top + 0.5f, right - 0.5f, bottom - 0.5f),
                                  ManagedDirectX.GetBrush(color));
        }

        /// <summary>
        ///  Draws text into a layout rectangle, wrapping at its right edge.
        /// </summary>
        public void DrawText(string text, IDWriteTextFormat format, RectangleF layout, Color color)
        {
            if (bitmap == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            var context = ManagedDirectX.BeginDraw(this);
            context.DrawText(text, format, layout, ManagedDirectX.GetBrush(color));
        }

        private void CreateSurface()
        {
#if TRACE
            ManagedDirectX.Profiler.Start("CreateSurface");
#endif
            Dispose();

            try
            {
                var context = ManagedDirectX.DeviceContext;

                if (string.IsNullOrEmpty(image))
                {
                    bitmap = context.CreateBitmap(new Size(rect.Right, rect.Bottom), IntPtr.Zero, 0,
                                                  SurfaceProperties);
                }
                else
                {
                    Trace.WriteLine(image);
                    if (!File.Exists(image))
                    {
                        Trace.WriteLine(
                            "Could not find the file '" + image +
                            "'.  This must be placed in the current directory.", "Picture Not Found");
                        rect = new RECT();
                    }
                    else
                    {
                        bitmap = LoadImage(context, image, out rect);
                    }
                }

                generation = ManagedDirectX.Generation;
            }
            catch (Exception exc)
            {
                throw new DirectXException("Error Creating a DirectDraw Surface", exc);
            }
#if TRACE
            ManagedDirectX.Profiler.End("DirectDrawSurface.CreateSurface");
#endif
        }

        private static ID2D1Bitmap1 LoadImage(ID2D1DeviceContext context, string path, out RECT rect)
        {
            using (var source = new Bitmap(path))
            {
                rect = new RECT();
                rect.Right = source.Width;
                rect.Bottom = source.Height;

                var bounds = new Rectangle(0, 0, source.Width, source.Height);
                var data = source.LockBits(bounds, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    // Format32bppArgb is laid out B, G, R, A in memory, which
                    // matches B8G8R8A8; only the alpha needs work
                    var pixels = new int[data.Stride/4*source.Height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    ApplyColorKey(pixels);

                    var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                    try
                    {
                        return context.CreateBitmap(new Size(source.Width, source.Height),
                                                    handle.AddrOfPinnedObject(), data.Stride, SurfaceProperties);
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                finally
                {
                    source.UnlockBits(data);
                }
            }
        }

        /// <summary>
        ///  Makes magenta pixels transparent and premultiplies the rest.  The
        ///  sprites were made for 16-bit color, where everything within one
        ///  5:6:5 step of pure magenta matched the color key, so this matches
        ///  the same range.
        /// </summary>
        private static void ApplyColorKey(int[] pixels)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                var a = (pixel >> 24) & 0xFF;
                var r = (pixel >> 16) & 0xFF;
                var g = (pixel >> 8) & 0xFF;
                var b = pixel & 0xFF;

                if (r >= 248 && g < 4 && b >= 248)
                {
                    pixels[i] = 0;
                }
                else if (a < 255)
                {
                    r = r*a/255;
                    g = g*a/255;
                    b = b*a/255;
                    pixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }
    }
}
