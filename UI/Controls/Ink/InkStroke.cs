// InkStroke.cs - 墨迹笔触数据结构
// 版权声明：MIT License | Copyright (c) 2026 思捷娅科技 (SJYKJ)

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace InkkSlinger.UI.Controls.Ink
{
    /// <summary>
    /// 表示单个墨迹笔触
    /// </summary>
    public class InkStroke
    {
        /// <summary>
        /// 笔触点列表
        /// </summary>
        public List<Vector2> Points { get; set; } = new List<Vector2>();

        /// <summary>
        /// 笔触颜色
        /// </summary>
        public Color Color { get; set; } = Color.Black;

        /// <summary>
        /// 笔触宽度
        /// </summary>
        public float Width { get; set; } = 2f;

        /// <summary>
        /// 笔触类型（硬笔/毛笔/荧光笔）
        /// </summary>
        public InkStrokeType StrokeType { get; set; } = InkStrokeType.Hard;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 获取笔触的边界矩形
        /// </summary>
        public RectangleF GetBounds()
        {
            if (Points.Count == 0)
                return new RectangleF(0, 0, 0, 0);

            float minX = Points[0].X;
            float minY = Points[0].Y;
            float maxX = Points[0].X;
            float maxY = Points[0].Y;

            foreach (var point in Points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 深拷贝笔触
        /// </summary>
        public InkStroke Clone()
        {
            return new InkStroke
            {
                Points = new List<Vector2>(this.Points),
                Color = this.Color,
                Width = this.Width,
                StrokeType = this.StrokeType,
                CreatedAt = this.CreatedAt
            };
        }
    }

    /// <summary>
    /// 笔触类型枚举
    /// </summary>
    public enum InkStrokeType
    {
        /// <summary>
        /// 硬笔（铅笔/圆珠笔）
        /// </summary>
        Hard,

        /// <summary>
        /// 毛笔（书法笔）
        /// </summary>
        Brush,

        /// <summary>
        /// 荧光笔
        /// </summary>
        Highlighter
    }

    /// <summary>
    /// 矩形结构（浮点数版本）
    /// </summary>
    public struct RectangleF
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public RectangleF(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Contains(Vector2 point)
        {
            return point.X >= X && point.X <= X + Width &&
                   point.Y >= Y && point.Y <= Y + Height;
        }
    }
}
