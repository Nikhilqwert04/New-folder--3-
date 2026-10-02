using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IntelVibrance.Core;
using IntelVibrance.Windows;

namespace IntelVibrance.UI
{
    /// <summary>
    /// Custom dark-mode slider control with Intel-blue accent.
    /// Renders a track, filled region, and draggable thumb.
    /// </summary>
    public class VibranceSlider : Control
    {
        private int _min = 0, _max = 100, _value = 50;
        private bool _dragging = false;
        private int _dragOffset = 0;

        // Colors
        private static readonly Color TrackBg = Color.FromArgb(40, 40, 50);
        private static readonly Color TrackFill = Color.FromArgb(0, 120, 215);
        private static readonly Color TrackFillHigh = Color.FromArgb(0, 170, 255);
        private static readonly Color ThumbColor = Color.White;
        private static readonly Color ThumbBorder = Color.FromArgb(0, 120, 215);
        private static readonly Color TextColor = Color.FromArgb(200, 200, 220);

        public event EventHandler<int> ValueChanged;

        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Max(_min, Math.Min(_max, value));
                Invalidate();
            }
        }

        public VibranceSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = 60;
        }

        private Rectangle GetTrackRect()
        {
            int margin = 12;
            int trackHeight = 6;
            int y = (Height - trackHeight) / 2 - 4;
            return new Rectangle(margin, y, Width - margin * 2, trackHeight);
        }

        private Point GetThumbCenter()
        {
            var track = GetTrackRect();
            double ratio = (_value - _min) / (double)(_max - _min);
            int x = track.Left + (int)(ratio * track.Width);
            int y = track.Top + track.Height / 2;
            return new Point(x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(24, 24, 36));

            var track = GetTrackRect();
            var thumb = GetThumbCenter();

            // Draw track background
            using (var path = RoundedRect(track, 3))
            using (var brush = new SolidBrush(TrackBg))
                g.FillPath(brush, path);

            // Draw filled portion
            if (thumb.X > track.Left)
            {
                var filled = new Rectangle(track.Left, track.Top, thumb.X - track.Left, track.Height);
                using (var path = RoundedRect(filled, 3))
                using (var brush = new LinearGradientBrush(filled, TrackFill, TrackFillHigh, LinearGradientMode.Horizontal))
                    g.FillPath(brush, path);
            }

            // Draw thumb
            int r = 10;
            var thumbRect = new Rectangle(thumb.X - r, thumb.Y - r, r * 2, r * 2);
            using (var brush = new SolidBrush(ThumbColor))
                g.FillEllipse(brush, thumbRect);
            using (var pen = new Pen(ThumbBorder, 2.5f))
                g.DrawEllipse(pen, thumbRect);

            // Draw percentage label
            string label = $"{_value}%";
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                var size = g.MeasureString(label, font);
                int lx = thumb.X - (int)(size.Width / 2);
                int ly = track.Bottom + 10;
                // Clamp within control
                lx = Math.Max(0, Math.Min(Width - (int)size.Width, lx));
                using (var brush = new SolidBrush(TextColor))
                    g.DrawString(label, font, brush, lx, ly);
            }

            // 0% and 100% labels
            using (var font = new Font("Segoe UI", 8f))
            using (var brush = new SolidBrush(Color.FromArgb(100, 100, 120)))
            {
                g.DrawString("0%", font, brush, track.Left - 2, track.Bottom + 10);
                string maxStr = "100%";
                var sz = g.MeasureString(maxStr, font);
                g.DrawString(maxStr, font, brush, track.Right - (int)sz.Width + 2, track.Bottom + 10);
            }
        }

        private GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Top, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                UpdateValueFromX(e.X);
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
                UpdateValueFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            Capture = false;
        }

        private void UpdateValueFromX(int x)
        {
            var track = GetTrackRect();
            double ratio = (double)(x - track.Left) / track.Width;
            int newVal = _min + (int)(ratio * (_max - _min));
            newVal = Math.Max(_min, Math.Min(_max, newVal));
            if (newVal != _value)
            {
                _value = newVal;
                Invalidate();
                ValueChanged?.Invoke(this, _value);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int delta = e.Delta > 0 ? 1 : -1;
            Value += delta;
            ValueChanged?.Invoke(this, _value);
        }
    }

    /// <summary>
    /// A styled dark button with hover animation.
    /// </summary>
    public class DarkButton : Button
    {
        private bool _hovering = false;
        private Color _accentColor;

        public DarkButton(Color accent)
        {
            _accentColor = accent;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.FromArgb(35, 35, 45);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovering = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = _hovering
                ? Color.FromArgb(_accentColor.A, _accentColor.R, _accentColor.G, _accentColor.B)
                : Color.FromArgb(45, 45, 60);

            using (var path = RoundedRect(ClientRectangle, 6))
            using (var brush = new SolidBrush(bg))
                g.FillPath(brush, path);

            // Border
            Color borderColor = _hovering ? _accentColor : Color.FromArgb(70, 70, 90);
            using (var path = RoundedRect(ClientRectangle, 6))
            using (var pen = new Pen(borderColor, 1.5f))
                g.DrawPath(pen, path);

            // Text
            var textSize = g.MeasureString(Text, Font);
            float tx = (Width - textSize.Width) / 2;
            float ty = (Height - textSize.Height) / 2;
            using (var brush = new SolidBrush(ForeColor))
                g.DrawString(Text, Font, brush, tx, ty);
        }

        private GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Top, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
