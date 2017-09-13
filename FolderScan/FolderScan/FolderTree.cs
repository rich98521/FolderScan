using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Text;
using System.Diagnostics;
using System.IO;

namespace FolderScan
{
    class FolderTree : UserControl
    {
        private FolderNode root = new FolderNode("");
        FolderNode selected;
        Font font;
        Scroll scrollbar;
        int fOffset;
        int height = 0;
        bool mLClick, mRClick;
        Point mPos;
        public FolderTree()
        {
            scrollbar = new Scroll();
            font = new Font("Segoe UI", 10);
            this.BorderStyle = BorderStyle.Fixed3D;
            this.SetStyle(ControlStyles.DoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            this.Controls.Add(scrollbar);
            scrollbar.Show();
            scrollbar.Dock = DockStyle.Right;
            scrollbar.IndexChanged += new IndexChangedEventHandler(scrl_IndexChanged);
            scrollbar.VerticalMaximum = 0;
            scrollbar.BackColor = Color.FromArgb(200, 200, 200);
            scrollbar.ForeColor = Color.FromArgb(28, 150, 200);
            this.BackColor = Color.White;
            ContextMenu cm = new ContextMenu();
            cm.MenuItems.Add(new MenuItem("Open Folder", CtxtOpenFolder));
            cm.Popup += new EventHandler(cm_Popup);
            ContextMenu = cm;
        }

        public FolderNode Selected
        {
            get { return selected; }
            set { selected = value; }
        }

        private void cm_Popup(object sender, EventArgs e)
        {
            this.Refresh();
        }

        private void CtxtOpenFolder(object sender, EventArgs e)
        {
            Process.Start(@selected.Path);
        }

        private void scrl_IndexChanged(object sender, EventArgs e)
        {
            fOffset = (int)scrollbar.VerticalScrollPos;
            this.Refresh();
        }

        public List<FolderNode> Nodes
        {
            get { return root.Nodes; }
            set { root.Nodes = value; }
        }

        private int DrawAllNodes(int height, int indent, Graphics g, List<FolderNode> nodes)
        {
            foreach (FolderNode n in nodes)
            {
                if (mPos.Y + fOffset > height && mPos.Y + fOffset < height + 15)
                {
                    if (mLClick)
                    {
                        if (mPos.X > 3 + indent * 20 && mPos.X < 17 + indent * 20 && n.Nodes.Count > 0)
                            n.Open = !n.Open;
                    }
                    if (mRClick || mLClick)
                    {
                        selected = n;
                        if (selected.Text.Contains(": "))
                            selected = n.Parent;
                    }
                }

                if (height + 15 > fOffset && height < fOffset + this.Height)
                {
                    if (n.Nodes.Count > 0)
                    {
                        g.DrawRectangle(new Pen(Color.DarkGray), new Rectangle(6 + indent * 20, height + 3 - fOffset, 8, 8));
                        g.DrawLine(new Pen(Color.Black), 8 + indent * 20, height + 7 - fOffset, 12 + indent * 20, height + 7 - fOffset);
                        if (!n.Open)
                        {
                            g.DrawLine(new Pen(Color.Black), 10 + indent * 20, height + 5 - fOffset, 10 + indent * 20, height + 9 - fOffset);
                        }
                        g.DrawLine(new Pen(Color.LightGray), 16 + indent * 20, height + 7 - fOffset, 20 + indent * 20, height + 7 - fOffset);
                    }
                    else
                    {
                        g.DrawLine(new Pen(Color.LightGray), 10 + indent * 20, height + 2 - fOffset, 10 + indent * 20, height + 12 - fOffset);
                        g.DrawLine(new Pen(Color.LightGray), 11 + indent * 20, height + 7 - fOffset, 20 + indent * 20, height + 7 - fOffset);
                    }
                    if (n.Nodes.Count > 0)
                        g.FillRectangle(new SolidBrush(Color.FromArgb(200, 28, 150, 200)), new RectangleF(new PointF((indent + 1) * 20, height - fOffset), new SizeF((this.Width - (indent + 1) * 20) * (float)(n.TotalSize / (double)root.Nodes[0].TotalSize), 15)));
                    g.DrawString(n.Text, font, new SolidBrush(Color.Black), new PointF((indent + 1) * 20, height - 2 - fOffset));
                    if (n == selected)
                        g.FillRectangle(new SolidBrush(Color.FromArgb(80, 12, 80, 140)), new RectangleF(new PointF(0, height - fOffset), new SizeF(this.Width, 15)));
                }
                height += 15;
                if (n.Open)
                {
                    height = DrawAllNodes(height, indent + 1, g, n.Nodes);
                }
            }
            return height;
        }


        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            mLClick = e.Button == MouseButtons.Left;
            mRClick = e.Button == MouseButtons.Right;
            this.Refresh();
            //if (mRClick)
            //    ContextMenu.Show(this, mPos);
            UpdateScrollBar();
            this.Refresh();
        }


        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            this.Refresh();
            mPos = e.Location;
            //scrollbar.Focus();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            this.Refresh();
            scrollbar.ScrollWheel(e.Delta);
        }

        public void AddNode(FolderNode n)
        {
            root.AddNode(n);
            UpdateScrollBar();
            this.Refresh();
        }

        public void RemoveNode(FolderNode n)
        {
            FolderNode parent = n.Parent;
            parent.RemoveNode(n);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateScrollBar();
            this.Refresh();
        }

        private void UpdateScrollBar()
        {
            scrollbar.VerticalMaximum = height;
        }

        public int InnerHeight
        {
            get { return height; }
            private set { height = value; UpdateScrollBar(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            InnerHeight = DrawAllNodes(0, 0, g, root.Nodes);
            mLClick = false;
            mRClick = false;
            base.OnPaint(e);
        }
    }

    [Serializable()]
    public class FolderNode
    {
        string path;
        string last;
        string text;
        long containedSize;
        long size;
        bool open;
        FolderNode parent = null;
        List<FolderNode> nodes = new List<FolderNode>();
        public FolderNode(string s)
        {
            path = s;
            last = path.Split('\\').Last();
            text = last;
        }
        public void SetTo(FolderNode fn)
        {
            text = fn.text;
            nodes = fn.Nodes;
            containedSize = fn.ContainedSize;
            size = fn.Size;
            open = fn.Open;
        }
        public void AddNode(FolderNode n)
        {
            n.Parent = this;
            nodes.Add(n);
            if (n.path.Split('\\').Length == 3)
                return;
        }
        public void RemoveNode(FolderNode n)
        {
            nodes.Remove(n);
        }
        public FolderNode(string s, string l)
        {
            path = s;
            last = l;
        }
        public string Text
        {
            get { return text; }
            set { text = value; }
        }
        public string Path
        {
            get { return path; }
            set { path = value; }
        }
        public List<FolderNode> Nodes
        {
            get { return nodes; }
            set { nodes = value; }
        }
        public FolderNode Parent
        {
            get { return parent; }
            set { parent = value; }
        }
        public bool Open
        {
            get { return open; }
            set { open = value; }
        }
        public string Last
        {
            get { return last; }
            set { last = value; }
        }
        public long ContainedSize
        {
            get { return containedSize; }
            set { containedSize = value; }
        }
        public long Size
        {
            get { return size; }
            set { size = value; }
        }
        public long TotalSize
        {
            get { return size + containedSize; }
        }
    }
}
