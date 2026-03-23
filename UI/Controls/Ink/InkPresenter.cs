// InkPresenter.cs - 墨迹渲染器
// 版权声明：MIT License | Copyright (c) 2026 思捷娅科技 (SJYKJ)

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace InkkSlinger.UI.Controls.Ink
{
    /// <summary>
    /// 墨迹渲染器 - 负责绘制和管理墨迹笔触
    /// </summary>
    public class InkPresenter : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly List<InkStroke> _strokes;
        private Texture2D _circleTexture;
        private bool _disposed;

        /// <summary>
        /// 所有墨迹笔触集合
        /// </summary>
        public IReadOnlyList<InkStroke> Strokes => _strokes.AsReadOnly();

        /// <summary>
        /// 当前选中/活动的笔触
        /// </summary>
        public InkStroke ActiveStroke { get; set; }

        /// <summary>
        /// 默认笔触颜色
        /// </summary>
        public Color DefaultColor { get; set; } = Color.Black;

        /// <summary>
        /// 默认笔触宽度
        /// </summary>
        public float DefaultWidth { get; set; } = 2f;

        /// <summary>
        /// 默认笔触类型
        /// </summary>
        public InkStrokeType DefaultStrokeType { get; set; } = InkStrokeType.Hard;

        /// <summary>
        /// 是否启用抗锯齿
        /// </summary>
        public bool AntiAliasing { get; set; } = true;

        /// <summary>
        /// 笔触数量
        /// </summary>
        public int StrokeCount => _strokes.Count;

        public InkPresenter(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _strokes = new List<InkStroke>();
            CreateCircleTexture();
        }

        /// <summary>
        /// 创建圆形纹理用于绘制笔触点
        /// </summary>
        private void CreateCircleTexture()
        {
            int size = 16;
            _circleTexture = new Texture2D(_graphicsDevice, size, size);
            Color[] data = new Color[size * size];

            float radius = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    float distance = dx * dx + dy * dy;

                    if (distance <= radius * radius)
                    {
                        // 抗锯齿边缘
                        float alpha = MathHelper.Clamp(1.0f - (distance - radius * radius + radius) / radius, 0, 1);
                        data[y * size + x] = Color.White * alpha;
                    }
                    else
                    {
                        data[y * size + x] = Color.Transparent;
                    }
                }
            }

            _circleTexture.SetData(data);
        }

        /// <summary>
        /// 开始新笔触
        /// </summary>
        public void StartStroke(Vector2 startPoint)
        {
            ActiveStroke = new InkStroke
            {
                Color = DefaultColor,
                Width = DefaultWidth,
                StrokeType = DefaultStrokeType
            };
            ActiveStroke.Points.Add(startPoint);
        }

        /// <summary>
        /// 向当前笔触添加点
        /// </summary>
        public void AddPointToStroke(Vector2 point)
        {
            if (ActiveStroke != null)
            {
                ActiveStroke.Points.Add(point);
            }
        }

        /// <summary>
        /// 结束当前笔触
        /// </summary>
        public void EndStroke()
        {
            if (ActiveStroke != null && ActiveStroke.Points.Count > 0)
            {
                _strokes.Add(ActiveStroke);
                ActiveStroke = null;
            }
        }

        /// <summary>
        /// 取消当前笔触
        /// </summary>
        public void CancelStroke()
        {
            ActiveStroke = null;
        }

        /// <summary>
        /// 绘制所有墨迹
        /// </summary>
        public void Render(SpriteBatch spriteBatch)
        {
            if (_disposed)
                return;

            foreach (var stroke in _strokes)
            {
                RenderStroke(spriteBatch, stroke);
            }

            // 绘制活动笔触（正在绘制的）
            if (ActiveStroke != null)
            {
                RenderStroke(spriteBatch, ActiveStroke);
            }
        }

        /// <summary>
        /// 绘制单个笔触
        /// </summary>
        private void RenderStroke(SpriteBatch spriteBatch, InkStroke stroke)
        {
            if (stroke.Points.Count < 2)
                return;

            switch (stroke.StrokeType)
            {
                case InkStrokeType.Hard:
                    RenderHardStroke(spriteBatch, stroke);
                    break;
                case InkStrokeType.Brush:
                    RenderBrushStroke(spriteBatch, stroke);
                    break;
                case InkStrokeType.Highlighter:
                    RenderHighlighterStroke(spriteBatch, stroke);
                    break;
            }
        }

        /// <summary>
        /// 绘制硬笔笔触
        /// </summary>
        private void RenderHardStroke(SpriteBatch spriteBatch, InkStroke stroke)
        {
            for (int i = 1; i < stroke.Points.Count; i++)
            {
                Vector2 start = stroke.Points[i - 1];
                Vector2 end = stroke.Points[i];

                float distance = Vector2.Distance(start, end);
                float angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);

                spriteBatch.Draw(
                    _circleTexture,
                    new Rectangle(
                        (int)start.X,
                        (int)start.Y,
                        (int)stroke.Width,
                        (int)stroke.Width
                    ),
                    null,
                    stroke.Color,
                    angle,
                    new Vector2(_circleTexture.Width / 2f),
                    SpriteEffects.None,
                    0
                );
            }
        }

        /// <summary>
        /// 绘制毛笔笔触（带压感效果）
        /// </summary>
        private void RenderBrushStroke(SpriteBatch spriteBatch, InkStroke stroke)
        {
            // 简化实现：使用变化的宽度模拟压感
            for (int i = 1; i < stroke.Points.Count; i++)
            {
                Vector2 start = stroke.Points[i - 1];
                Vector2 end = stroke.Points[i];

                float distance = Vector2.Distance(start, end);
                float angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);

                // 根据速度变化宽度（简化）
                float width = stroke.Width * (0.8f + 0.4f * (float)Math.Sin(i * 0.1f));

                spriteBatch.Draw(
                    _circleTexture,
                    new Rectangle(
                        (int)start.X,
                        (int)start.Y,
                        (int)width,
                        (int)width
                    ),
                    null,
                    stroke.Color,
                    angle,
                    new Vector2(_circleTexture.Width / 2f),
                    SpriteEffects.None,
                    0
                );
            }
        }

        /// <summary>
        /// 绘制荧光笔（半透明）
        /// </summary>
        private void RenderHighlighterStroke(SpriteBatch spriteBatch, InkStroke stroke)
        {
            Color highlightColor = stroke.Color * 0.5f; // 50% 透明度

            for (int i = 1; i < stroke.Points.Count; i++)
            {
                Vector2 start = stroke.Points[i - 1];
                Vector2 end = stroke.Points[i];

                float distance = Vector2.Distance(start, end);
                float angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);

                float width = stroke.Width * 2f; // 荧光笔更宽

                spriteBatch.Draw(
                    _circleTexture,
                    new Rectangle(
                        (int)start.X,
                        (int)start.Y,
                        (int)width,
                        (int)width
                    ),
                    null,
                    highlightColor,
                    angle,
                    new Vector2(_circleTexture.Width / 2f),
                    SpriteEffects.None,
                    0
                );
            }
        }

        /// <summary>
        /// 清除所有墨迹
        /// </summary>
        public void Clear()
        {
            _strokes.Clear();
            ActiveStroke = null;
        }

        /// <summary>
        /// 删除最后一个笔触
        /// </summary>
        public void Undo()
        {
            if (_strokes.Count > 0)
            {
                _strokes.RemoveAt(_strokes.Count - 1);
            }
        }

        /// <summary>
        /// 导出墨迹为 SVG 格式
        /// </summary>
        public string ExportToSvg()
        {
            var svg = new System.Text.StringBuilder();
            svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\">");

            foreach (var stroke in _strokes)
            {
                if (stroke.Points.Count == 0)
                    continue;

                svg.Append($"<path d=\"M {stroke.Points[0].X} {stroke.Points[0].Y}");
                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    svg.Append($" L {stroke.Points[i].X} {stroke.Points[i].Y}");
                }
                svg.AppendLine($"\" stroke=\"{ColorToHex(stroke.Color)}\" stroke-width=\"{stroke.Width}\" fill=\"none\"/>");
            }

            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        private string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _circleTexture?.Dispose();
                _disposed = true;
            }
        }
    }
}
