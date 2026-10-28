//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System;
using System.Drawing;
using SharpGen.Runtime;
using Terrarium.Tools;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Color4 = Vortice.Mathematics.Color4;
using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using FeatureLevel = Vortice.Direct3D.FeatureLevel;
using DWriteFactoryType = Vortice.DirectWrite.FactoryType;
using FontStyle = Vortice.DirectWrite.FontStyle;

namespace Terrarium.Renderer.DirectX
{
    /// <summary>
    ///  Owns the Direct3D 11 device and the Direct2D device context that every
    ///  DirectDrawSurface draws with.  The context draws into one surface at a
    ///  time: drawing into a different surface ends the current batch and
    ///  begins a new one, and EndDraw flushes the last batch before a present.
    /// </summary>
    public class ManagedDirectX
    {
        static readonly FeatureLevel[] featureLevels =
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3
            };

        static ID3D11Device        d3dDevice;
        static IDXGIFactory2       dxgiFactory;
        static ID2D1Factory1       d2dFactory;
        static ID2D1Device         d2dDevice;
        static ID2D1DeviceContext  deviceContext;
        static ID2D1SolidColorBrush brush;
        static IDWriteFactory      writeFactory;
        static DirectDrawSurface   currentTarget;
        static bool                deviceLost;
        static int                 generation;

#if TRACE
        static Profiler  directDrawProfiler;

        /// <summary>
        ///  Profiler used to time surface creation.
        /// </summary>
        public static Profiler Profiler
        {
            get
            {
                if (directDrawProfiler == null)
                {
                    directDrawProfiler = new Profiler();
                }

                return directDrawProfiler;
            }
        }
#endif

        /// <summary>
        ///  The Direct2D device context all surfaces draw with.
        /// </summary>
        public static ID2D1DeviceContext DeviceContext
        {
            get
            {
                EnsureDevice();
                return deviceContext;
            }
        }

        /// <summary>
        ///  The Direct3D 11 device that swap chains are created for.
        /// </summary>
        public static ID3D11Device Device
        {
            get
            {
                EnsureDevice();
                return d3dDevice;
            }
        }

        /// <summary>
        ///  The DXGI factory of the adapter the device runs on.
        /// </summary>
        public static IDXGIFactory2 DxgiFactory
        {
            get
            {
                EnsureDevice();
                return dxgiFactory;
            }
        }

        /// <summary>
        ///  True once Direct2D or DXGI has reported that the device is gone
        ///  (driver update, GPU reset, remote desktop switch).  Every surface
        ///  reports itself lost until ResetDevice builds a new device.
        /// </summary>
        public static bool DeviceLost
        {
            get { return deviceLost; }
        }

        /// <summary>
        ///  Increases each time a device is created, so a surface can tell
        ///  that its bitmap belongs to a device that no longer exists.
        /// </summary>
        internal static int Generation
        {
            get { return generation; }
        }

        private static void EnsureDevice()
        {
            if (deviceContext != null)
            {
                return;
            }

            try
            {
                // Fall back to the WARP software rasterizer when there is no
                // usable GPU, for example in a virtual machine
                var result = D3D11.D3D11CreateDevice(IntPtr.Zero, DriverType.Hardware,
                                                     DeviceCreationFlags.BgraSupport, featureLevels, out d3dDevice);
                if (result.Failure)
                {
                    D3D11.D3D11CreateDevice(IntPtr.Zero, DriverType.Warp, DeviceCreationFlags.BgraSupport,
                                            featureLevels, out d3dDevice).CheckError();
                }

                using (var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>())
                {
                    using (var adapter = dxgiDevice.GetAdapter())
                    {
                        dxgiFactory = adapter.GetParent<IDXGIFactory2>();
                    }

                    d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>(D2DFactoryType.SingleThreaded, DebugLevel.None);
                    d2dDevice = d2dFactory.CreateDevice(dxgiDevice);
                }

                deviceContext = d2dDevice.CreateDeviceContext(DeviceContextOptions.None);

                // Sprites are pixel art: keep edges hard, as DirectDraw blits were
                deviceContext.AntialiasMode = AntialiasMode.Aliased;
                deviceContext.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;

                brush = deviceContext.CreateSolidColorBrush(new Color4(0, 0, 0, 1), null);

                if (writeFactory == null)
                {
                    writeFactory = DWrite.DWriteCreateFactory<IDWriteFactory>(DWriteFactoryType.Shared);
                }

                deviceLost = false;
                generation++;
            }
            catch (Exception exc)
            {
                ReleaseDevice();
                throw new DirectXException("Error creating the Direct2D device", exc);
            }
        }

        /// <summary>
        ///  Throws away the current device so the next draw creates a new one.
        ///  Surfaces created on the old device report themselves lost.
        /// </summary>
        public static void ResetDevice()
        {
            ReleaseDevice();
        }

        private static void ReleaseDevice()
        {
            currentTarget = null;

            if (brush != null) { brush.Dispose(); brush = null; }
            if (deviceContext != null) { deviceContext.Dispose(); deviceContext = null; }
            if (d2dDevice != null) { d2dDevice.Dispose(); d2dDevice = null; }
            if (d2dFactory != null) { d2dFactory.Dispose(); d2dFactory = null; }
            if (dxgiFactory != null) { dxgiFactory.Dispose(); dxgiFactory = null; }
            if (d3dDevice != null) { d3dDevice.Dispose(); d3dDevice = null; }
        }

        internal static void MarkDeviceLost()
        {
            deviceLost = true;
        }

        /// <summary>
        ///  Points the device context at a surface, ending the batch that was
        ///  drawing into another surface first.
        /// </summary>
        internal static ID2D1DeviceContext BeginDraw(DirectDrawSurface target)
        {
            var context = DeviceContext;
            if (currentTarget == target)
            {
                return context;
            }

            EndDraw();

            context.Target = target.Bitmap;
            context.BeginDraw();
            currentTarget = target;
            return context;
        }

        /// <summary>
        ///  Finishes the current drawing batch, if there is one.  Call before
        ///  presenting, so everything drawn reaches the swap chain.
        /// </summary>
        public static void EndDraw()
        {
            if (currentTarget == null)
            {
                return;
            }

            currentTarget = null;
            var result = deviceContext.EndDraw();
            deviceContext.Target = null;

            if (result.Code == Vortice.Direct2D1.ResultCode.RecreateTarget.Code)
            {
                deviceLost = true;
            }
            else
            {
                result.CheckError();
            }
        }

        /// <summary>
        ///  Ends the current batch when it draws into the given surface, so the
        ///  surface can be disposed.
        /// </summary>
        internal static void ReleaseTarget(DirectDrawSurface target)
        {
            if (currentTarget == target)
            {
                EndDraw();
            }
        }

        /// <summary>
        ///  The shared solid brush, set to the given color.
        /// </summary>
        internal static ID2D1SolidColorBrush GetBrush(Color color)
        {
            EnsureDevice();
            brush.Color = ToColor4(color);
            return brush;
        }

        /// <summary>
        ///  Creates a DirectWrite text format.  The size is in points, like
        ///  System.Drawing.Font, and is converted to 96 DPI pixels.
        /// </summary>
        public static IDWriteTextFormat CreateTextFormat(string fontFamily, float pointSize, bool bold)
        {
            EnsureDevice();
            var format = writeFactory.CreateTextFormat(
                fontFamily,
                bold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle.Normal,
                FontStretch.Normal,
                pointSize*96f/72f);
            format.WordWrapping = WordWrapping.Wrap;
            return format;
        }

        internal static Color4 ToColor4(Color color)
        {
            return new Color4(color.R/255f, color.G/255f, color.B/255f, color.A/255f);
        }
    }
}
