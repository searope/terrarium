//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System.Drawing;

namespace Terrarium.Renderer.DirectX
{
    /// <summary>
    ///  A pixel rectangle with exclusive Right and Bottom edges, laid out
    ///  like the Win32 RECT the DirectDraw renderer used.
    /// </summary>
    public struct RECT
    {
        /// <summary>
        ///  The left edge.
        /// </summary>
        public int Left;

        /// <summary>
        ///  The top edge.
        /// </summary>
        public int Top;

        /// <summary>
        ///  The right edge, one past the last column.
        /// </summary>
        public int Right;

        /// <summary>
        ///  The bottom edge, one past the last row.
        /// </summary>
        public int Bottom;

        /// <summary>
        ///  True when the rectangle covers no pixels.  Fill calls treat an
        ///  empty rectangle as the whole surface, as DirectDraw did.
        /// </summary>
        public bool IsEmpty
        {
            get { return Right <= Left || Bottom <= Top; }
        }

        internal RectangleF ToRectangleF()
        {
            return RectangleF.FromLTRB(Left, Top, Right, Bottom);
        }
    }
}
