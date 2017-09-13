using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FolderScan
{
    public delegate void IndexChangedEventHandler(object sender, EventArgs e);
    class Scroll : UserControl
    {
        public event IndexChangedEventHandler IndexChanged;
        int minV = 0, minH = 0, maxV = 0, maxH = 0, iSizeV = 0, iSizeH = 0, pSizeV = 0, pSizeH = 0, clickChange, grabOffsetX, grabOffsetY, viewW = 16, viewH = 16;
        float indexV, indexH = 0;
        bool mDown, grab, useSize = true, drawIndex = false;
        RectangleF slide;

        public Scroll()
        {
            DoubleBuffered = true;
            this.Height = 16;
            this.Width = 16;
        }

        public int ViewWidth
        {
            get { return viewW; }
            set { viewW = !useSize ? value : viewW; UpdateSlide(); }
        }

        public int ViewHeight
        {
            get { return viewH; }
            set { viewH = !useSize ? value : viewH; UpdateSlide(); }
        }

        public bool UseSize
        {
            set
            {
                useSize = value;
                if (useSize)
                {
                    viewW = this.Width;
                    viewH = this.Height;
                }
            }
            get { return useSize; }
        }

        public bool DrawIndex
        {
            set
            { drawIndex = value; }
            get { return drawIndex; }
        }

        private void UpdateSlide()
        {
            slide.Width = (float)Math.Max(Math.Min(((float)viewW / iSizeH) * viewW, viewW), 16);
            slide.Height = (float)Math.Max(Math.Min(((float)viewH / iSizeV) * viewH, viewH), 16);
            pSizeH = (int)(this.Width - slide.Width);
            pSizeV = (int)(this.Height - slide.Height);
            slide.X = iSizeH == 0 ? 0 : (indexH / iSizeH) * pSizeH;
            slide.Y = iSizeV == 0 ? 0 : (indexV / iSizeV) * pSizeV;
        }

        public float VerticalIndex
        {
            get { return minV + indexV; }
            set
            {
                if (value >= minV && value <= maxV)
                {
                    indexV = value - minV;
                    UpdateSlide();
                    OnIndexChanged(EventArgs.Empty);
                }
            }
        }

        public float HorizontalIndex
        {
            get { return minH + indexH; }
            set
            {
                if (value >= minH && value <= maxH)
                {
                    indexH = value - minH;
                    UpdateSlide();
                    OnIndexChanged(EventArgs.Empty);
                }
            }
        }

        public float HorizontalScrollPos
        {
            get { return iSizeH == 0 ? 0 : (HorizontalIndex / iSizeH * (iSizeH - this.Width)); }
        }

        public float VerticalScrollPos
        {
            get { return iSizeV == 0 ? 0 : (VerticalIndex / iSizeV * (iSizeV - this.Height)); }
        }

        public int VerticalMinimum
        {
            get { return minV; }
            set { if (value <= maxV) minV = value; UpdateSlide(); iSizeV = maxV - minV; }
        }

        public int HorizontalMinimum
        {
            get { return minH; }
            set { if (value <= maxH) minH = value; UpdateSlide(); iSizeH = maxH - minH; }
        }

        public int VerticalMaximum
        {
            get { return maxV; }
            set { if (value >= minV) { maxV = value; UpdateSlide(); iSizeV = maxV - minV; } }
        }

        public int HorizontalMaximum
        {
            get { return maxH; }
            set { if (value >= minH) { maxH = value; UpdateSlide(); iSizeH = maxH - minH; } }
        }

        protected virtual void OnIndexChanged(EventArgs e)
        {
            if (IndexChanged != null)
                IndexChanged(this, e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            grabOffsetX = (int)(e.X - slide.X);
            grabOffsetY = (int)(e.Y - slide.Y);
            if (grabOffsetX < slide.Width && grabOffsetX > 0 && grabOffsetY < slide.Height && grabOffsetY > 0)
                grab = true;
            else
                ScrollWheel(-grabOffsetY);
            mDown = true;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            mDown = false;
            grab = false;
            this.Refresh();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (grab)
            {
                float x = e.X - grabOffsetX;
                float y = e.Y - grabOffsetY;
                indexH = Math.Max(0, Math.Min(pSizeH == 0 ? 0 : (x / pSizeH) * iSizeH, iSizeH));
                indexV = Math.Max(0, Math.Min(pSizeV == 0 ? 0 : (y / pSizeV) * iSizeV, iSizeV));
                UpdateSlide();
                OnIndexChanged(new EventArgs());
                this.Refresh();
            }
        }

        public void ScrollWheel(int delta)
        {
            if (ModifierKeys == Keys.Shift)
            {
                HorizontalIndex = Math.Min(Math.Max(indexH + (delta > 0 ? 32 : -32), minH), maxH);
            }
            else
            {
                VerticalIndex = Math.Min(Math.Max(indexV - (delta > 0 ? 32 : -32), minV), maxV);
            }
            this.Refresh();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollWheel(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (useSize)
            {
                viewW = this.Width;
                viewH = this.Height;
            }
            indexH = Math.Max(0, Math.Min(pSizeH == 0 ? 0 : (slide.X / pSizeH) * iSizeH, iSizeH));
            indexV = Math.Max(0, Math.Min(pSizeV == 0 ? 0 : (slide.Y / pSizeV) * iSizeV, iSizeV));
            UpdateSlide();
            OnIndexChanged(new EventArgs());
            this.Refresh();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.Size != slide.Size)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.FillRectangle(new SolidBrush(this.BackColor), new Rectangle(new Point(), this.Size));
                g.FillRectangle(new SolidBrush(Color.FromArgb(5, 5, 5)), slide);
                g.FillRectangle(new SolidBrush(this.ForeColor), new RectangleF(slide.X + 1, slide.Y + 1, slide.Width - 2, slide.Height - 2));
            }
            //g.DrawString(indexH + ", " + indexV, this.Font, new SolidBrush(Color.White), new Point());
        }
    }
}
