//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System;
using System.Windows.Forms;
using Vortice.Direct2D1;
using Vortice.DXGI;
using AlphaMode = Vortice.DCommon.AlphaMode;
using PixelFormat = Vortice.DCommon.PixelFormat;

namespace Terrarium.Renderer.DirectX
{
    /// <summary>
    ///  A picture box that shows its frames through a DXGI flip model swap
    ///  chain.  ScreenSurface is the swap chain's back buffer; BackBufferSurface
    ///  is where a frame is composed before it is copied there.
    /// </summary>
    public class DirectDrawPictureBox : PictureBox
    {
        private IDXGISwapChain1 swapChain;

        /// <summary>
        ///  Creates the control.  The swap chain is created later by
        ///  CreateScreenSurface, once the control has a window.
        /// </summary>
        public DirectDrawPictureBox()
        {
            InitializeComponent();
        }

        /// <summary>
        ///  The swap chain's back buffer: what is drawn here is shown by
        ///  PresentScreen.
        /// </summary>
        protected DirectDrawSurface ScreenSurface { get; set; }

        /// <summary>
        ///  The surface a frame is composed on before it is copied to the
        ///  screen.
        /// </summary>
        protected DirectDrawSurface BackBufferSurface { get; set; }

        private void InitializeComponent()
        {
        }

        /// <summary>
        ///  Creates the swap chain for this control at its current size and
        ///  wraps its back buffer as ScreenSurface.
        /// </summary>
        protected void CreateScreenSurface()
        {
            // A window can only have one flip model swap chain
            ReleaseScreenSurface();

            var description = new SwapChainDescription1(
                Math.Max(1, ClientSize.Width),
                Math.Max(1, ClientSize.Height),
                Format.B8G8R8A8_UNorm,
                false,
                Usage.RenderTargetOutput,
                2,
                Scaling.Stretch,
                SwapEffect.FlipDiscard,
                Vortice.DXGI.AlphaMode.Ignore,
                SwapChainFlags.None);

            var factory = ManagedDirectX.DxgiFactory;
            swapChain = factory.CreateSwapChainForHwnd(ManagedDirectX.Device, Handle, description, null, null);

            // Alt+Enter would switch DXGI to exclusive full screen behind the
            // game's back; the game handles its own window layout
            factory.MakeWindowAssociation(Handle, WindowAssociationFlags.IgnoreAltEnter);

            using (var backBuffer = swapChain.GetBuffer<IDXGISurface>(0))
            {
                var properties = new BitmapProperties1(
                    new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Ignore), 96, 96,
                    BitmapOptions.Target | BitmapOptions.CannotDraw);
                ScreenSurface = new DirectDrawSurface(
                    ManagedDirectX.DeviceContext.CreateBitmapFromDxgiSurface(backBuffer, properties));
            }
        }

        /// <summary>
        ///  Shows what has been drawn into ScreenSurface.
        /// </summary>
        protected void PresentScreen()
        {
            ManagedDirectX.EndDraw();

            if (swapChain == null)
            {
                return;
            }

            var result = swapChain.Present(0, PresentFlags.None);
            if (result.Failure)
            {
                ManagedDirectX.MarkDeviceLost();
            }
        }

        /// <summary>
        ///  Releases the swap chain and its back buffer.
        /// </summary>
        protected void ReleaseScreenSurface()
        {
            if (ScreenSurface != null)
            {
                ScreenSurface.Dispose();
                ScreenSurface = null;
            }

            if (swapChain != null)
            {
                swapChain.Dispose();
                swapChain = null;
            }
        }

        /// <summary>
        ///  Releases the swap chain along with the window it draws to.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            ReleaseScreenSurface();
            base.OnHandleDestroyed(e);
        }

        /// <summary>
        ///  Only paints in the designer; at run time the swap chain owns
        ///  the window's pixels.
        /// </summary>
        /// <param name="e">Paint event arguments.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (DesignMode)
            {
                e.Graphics.Clear(BackColor);
            }
        }

        /// <summary>
        ///  Does nothing, so the background isn't painted over the frame.
        /// </summary>
        /// <param name="e">Paint event arguments.</param>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }
    }
}
