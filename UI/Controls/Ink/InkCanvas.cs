// InkCanvas.cs - 墨迹画布控件
// 版权声明：MIT License | Copyright (c) 2026 思捷娅科技 (SJYKJ)

using InkkSlinger.UI.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace InkkSlinger.UI.Controls.Ink
{
    /// <summary>
    /// 墨迹画布控件 - 支持手写输入的 UI 控件
    /// </summary>
    public class InkCanvas : Control
    {
        private readonly InkPresenter _inkPresenter;
        private bool _isDrawing;
        private Vector2 _lastPosition;
        private MouseState _previousMouseState;

        /// <summary>
        /// 获取底层的墨迹渲染器
        /// </summary>
        public InkPresenter Presenter => _inkPresenter;

        /// <summary>
        /// 是否启用绘图
        /// </summary>
        public bool IsDrawingEnabled { get; set; } = true;

        /// <summary>
        /// 是否显示光标
        /// </summary>
        public bool ShowCursor { get; set; } = true;

        /// <summary>
        /// 背景颜色
        /// </summary>
        public Color BackgroundColor { get; set; } = Color.White;

        /// <summary>
        /// 是否启用触摸输入
        /// </summary>
        public bool TouchEnabled { get; set; } = true;

        /// <summary>
        /// 笔触颜色
        /// </summary>
        public Color StrokeColor
        {
            get => _inkPresenter.DefaultColor;
            set => _inkPresenter.DefaultColor = value;
        }

        /// <summary>
        /// 笔触宽度
        /// </summary>
        public float StrokeWidth
        {
            get => _inkPresenter.DefaultWidth;
            set => _inkPresenter.DefaultWidth = value;
        }

        /// <summary>
        /// 笔触类型
        /// </summary>
        public InkStrokeType StrokeType
        {
            get => _inkPresenter.DefaultStrokeType;
            set => _inkPresenter.DefaultStrokeType = value;
        }

        /// <summary>
        /// 笔触数量
        /// </summary>
        public int StrokeCount => _inkPresenter.StrokeCount;

        public InkCanvas(UiRoot root) : base(root)
        {
            _inkPresenter = new InkPresenter(root.GraphicsDevice);
            
            // 设置默认大小
            Width = 400;
            Height = 300;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsDrawingEnabled || !IsVisible)
                return;

            MouseState mouseState = Mouse.GetState();

            // 检查鼠标输入
            if (mouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                // 鼠标按下 - 开始绘制
                Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);
                if (IsPointInControl(mousePos))
                {
                    StartDrawing(mousePos);
                }
            }
            else if (mouseState.LeftButton == ButtonState.Pressed && _isDrawing)
            {
                // 鼠标移动中 - 继续绘制
                Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);
                ContinueDrawing(mousePos);
            }
            else if (mouseState.LeftButton == ButtonState.Released && _isDrawing)
            {
                // 鼠标释放 - 结束绘制
                StopDrawing();
            }

            _previousMouseState = mouseState;
        }

        public override void Render(SpriteBatch spriteBatch)
        {
            if (!IsVisible)
                return;

            // 绘制背景
            spriteBatch.Draw(
                Root.WhiteTexture,
                new Rectangle((int)X, (int)Y, (int)Width, (int)Height),
                BackgroundColor
            );

            // 绘制墨迹
            spriteBatch.Begin();
            _inkPresenter.Render(spriteBatch);
            spriteBatch.End();

            // 绘制边框
            spriteBatch.Draw(
                Root.WhiteTexture,
                new Rectangle((int)X, (int)Y, (int)Width, 2),
                Color.Gray
            );
            spriteBatch.Draw(
                Root.WhiteTexture,
                new Rectangle((int)X, (int)Y + (int)Height - 2, (int)Width, 2),
                Color.Gray
            );
            spriteBatch.Draw(
                Root.WhiteTexture,
                new Rectangle((int)X, (int)Y, 2, (int)Height),
                Color.Gray
            );
            spriteBatch.Draw(
                Root.WhiteTexture,
                new Rectangle((int)X + (int)Width - 2, (int)Y, 2, (int)Height),
                Color.Gray
            );

            // 绘制光标
            if (ShowCursor && IsMouseOver())
            {
                Vector2 mousePos = new Vector2(Mouse.GetState().X, Mouse.GetState().Y);
                if (IsPointInControl(mousePos))
                {
                    // 绘制圆形光标
                    float cursorSize = StrokeWidth * 2;
                    spriteBatch.Draw(
                        Root.WhiteTexture,
                        new Rectangle(
                            (int)(mousePos.X - cursorSize / 2),
                            (int)(mousePos.Y - cursorSize / 2),
                            (int)cursorSize,
                            (int)cursorSize
                        ),
                        StrokeColor * 0.5f
                    );
                }
            }

            base.Render(spriteBatch);
        }

        /// <summary>
        /// 开始绘制
        /// </summary>
        private void StartDrawing(Vector2 position)
        {
            _isDrawing = true;
            _lastPosition = position;

            // 转换为控件本地坐标
            Vector2 localPos = position - new Vector2(X, Y);
            _inkPresenter.StartStroke(localPos);

            OnDrawingStarted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 继续绘制
        /// </summary>
        private void ContinueDrawing(Vector2 position)
        {
            if (!_isDrawing)
                return;

            // 转换为控件本地坐标
            Vector2 localPos = position - new Vector2(X, Y);
            _inkPresenter.AddPointToStroke(localPos);
            _lastPosition = position;

            OnStrokeUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 停止绘制
        /// </summary>
        private void StopDrawing()
        {
            if (!_isDrawing)
                return;

            _inkPresenter.EndStroke();
            _isDrawing = false;

            OnDrawingEnded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 检查点是否在控件内
        /// </summary>
        private bool IsPointInControl(Vector2 point)
        {
            return point.X >= X && point.X <= X + Width &&
                   point.Y >= Y && point.Y <= Y + Height;
        }

        /// <summary>
        /// 检查鼠标是否在控件上
        /// </summary>
        private bool IsMouseOver()
        {
            MouseState mouseState = Mouse.GetState();
            return IsPointInControl(new Vector2(mouseState.X, mouseState.Y));
        }

        /// <summary>
        /// 清除所有墨迹
        /// </summary>
        public void Clear()
        {
            _inkPresenter.Clear();
            OnCleared?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 撤销最后一个笔触
        /// </summary>
        public void Undo()
        {
            _inkPresenter.Undo();
            OnUndo?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 导出为 SVG
        /// </summary>
        public string ExportToSvg()
        {
            return _inkPresenter.ExportToSvg();
        }

        /// <summary>
        /// 保存为 PNG 图片
        /// </summary>
        public void SaveAsPng(string filePath)
        {
            // TODO: 实现 PNG 导出
            throw new NotImplementedException("PNG 导出功能待实现");
        }

        // 事件
        public event EventHandler OnDrawingStarted;
        public event EventHandler OnStrokeUpdated;
        public event EventHandler OnDrawingEnded;
        public event EventHandler OnCleared;
        public event EventHandler OnUndo;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inkPresenter?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
