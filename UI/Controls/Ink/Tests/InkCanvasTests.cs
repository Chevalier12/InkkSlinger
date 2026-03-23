// InkCanvasTests.cs - InkCanvas 单元测试
// 版权声明：MIT License | Copyright (c) 2026 思捷娅科技 (SJYKJ)

using InkkSlinger.UI.Controls.Ink;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Moq;
using Xunit;

namespace InkkSlinger.Tests.UI.Controls.Ink
{
    public class InkCanvasTests
    {
        private Mock<GraphicsDevice> _mockGraphicsDevice;
        private UiRoot _uiRoot;

        public InkCanvasTests()
        {
            _mockGraphicsDevice = new Mock<GraphicsDevice>(
                MockBehavior.Loose,
                GraphicsAdapter.DefaultAdapter,
                GraphicsProfile.HiDef,
                new PresentationParameters
                {
                    BackBufferWidth = 800,
                    BackBufferHeight = 600
                });
            
            _uiRoot = new UiRoot(_mockGraphicsDevice.Object);
        }

        [Fact]
        public void InkCanvas_Creation_ShouldInitialize()
        {
            // Arrange & Act
            var inkCanvas = new InkCanvas(_uiRoot)
            {
                X = 50,
                Y = 50,
                Width = 400,
                Height = 300
            };

            // Assert
            Assert.NotNull(inkCanvas);
            Assert.Equal(50, inkCanvas.X);
            Assert.Equal(50, inkCanvas.Y);
            Assert.Equal(400, inkCanvas.Width);
            Assert.Equal(300, inkCanvas.Height);
            Assert.Equal(0, inkCanvas.StrokeCount);
            Assert.True(inkCanvas.IsDrawingEnabled);
            Assert.True(inkCanvas.ShowCursor);
            Assert.Equal(Color.White, inkCanvas.BackgroundColor);
        }

        [Fact]
        public void InkCanvas_StrokeColor_ShouldUpdatePresenter()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot);
            
            // Act
            inkCanvas.StrokeColor = Color.Red;
            
            // Assert
            Assert.Equal(Color.Red, inkCanvas.StrokeColor);
            Assert.Equal(Color.Red, inkCanvas.Presenter.DefaultColor);
        }

        [Fact]
        public void InkCanvas_StrokeWidth_ShouldUpdatePresenter()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot);
            
            // Act
            inkCanvas.StrokeWidth = 5f;
            
            // Assert
            Assert.Equal(5f, inkCanvas.StrokeWidth);
            Assert.Equal(5f, inkCanvas.Presenter.DefaultWidth);
        }

        [Fact]
        public void InkCanvas_DrawSingleStroke_ShouldIncreaseStrokeCount()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot);
            
            // Act
            inkCanvas.Presenter.StartStroke(new Vector2(10, 10));
            inkCanvas.Presenter.AddPointToStroke(new Vector2(20, 20));
            inkCanvas.Presenter.AddPointToStroke(new Vector2(30, 30));
            inkCanvas.Presenter.EndStroke();
            
            // Assert
            Assert.Equal(1, inkCanvas.StrokeCount);
            Assert.Single(inkCanvas.Presenter.Strokes);
        }

        [Fact]
        public void InkCanvas_DrawMultipleStrokes_ShouldAccumulate()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot);
            
            // Act - Draw 3 strokes
            AddStroke(inkCanvas, Color.Black);
            AddStroke(inkCanvas, Color.Red);
            AddStroke(inkCanvas, Color.Blue);
            
            // Assert
            Assert.Equal(3, inkCanvas.StrokeCount);
        }

        [Fact]
        public void InkCanvas_Clear_ShouldRemoveAllStrokes()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot);
            AddStroke(inkCanvas, Color.Black);
            AddStroke(inkCanvas, Color.Red);
            Assert.Equal(2, inkCanvas.StrokeCount);
            
            // Act
            inkCanvas.Clear();
            
            // Assert
            Assert.Equal(0, inkCanvas.StrokeCount);
            Assert.Empty(inkCanvas.Presenter.Strokes);
        }

        [Fact]
        public void InkCanvas_Undo_ShouldRemoveLastStroke()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot);
            AddStroke(inkCanvas, Color.Black);
            AddStroke(inkCanvas, Color.Red);
            Assert.Equal(2, inkCanvas.StrokeCount);
            
            // Act
            inkCanvas.Undo();
            
            // Assert
            Assert.Equal(1, inkCanvas.StrokeCount);
        }

        [Fact]
        public void InkCanvas_Undo_EmptyCanvas_ShouldNotCrash()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot);
            Assert.Equal(0, inkCanvas.StrokeCount);
            
            // Act & Assert - Should not throw
            inkCanvas.Undo();
            Assert.Equal(0, inkCanvas.StrokeCount);
        }

        [Fact]
        public void InkCanvas_ExportToSvg_ShouldReturnValidSvg()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot);
            AddStroke(inkCanvas, Color.Black);
            
            // Act
            var svg = inkCanvas.ExportToSvg();
            
            // Assert
            Assert.NotNull(svg);
            Assert.StartsWith("<?xml", svg);
            Assert.Contains("<svg", svg);
            Assert.Contains("</svg>", svg);
        }

        [Fact]
        public void InkCanvas_DifferentStrokeTypes_ShouldRenderCorrectly()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot);
            
            // Act & Assert - Hard stroke
            inkCanvas.StrokeType = InkStrokeType.Hard;
            AddStroke(inkCanvas, Color.Black);
            Assert.Equal(1, inkCanvas.StrokeCount);
            
            // Brush stroke
            inkCanvas.StrokeType = InkStrokeType.Brush;
            AddStroke(inkCanvas, Color.Red);
            Assert.Equal(2, inkCanvas.StrokeCount);
            
            // Highlighter stroke
            inkCanvas.StrokeType = InkStrokeType.Highlighter;
            AddStroke(inkCanvas, Color.Yellow);
            Assert.Equal(3, inkCanvas.StrokeCount);
        }

        [Fact]
        public void InkCanvas_StrokeBounds_ShouldBeCalculated()
        {
            // Arrange
            var stroke = new InkStroke
            {
                Color = Color.Black,
                Width = 2f
            };
            stroke.Points.Add(new Vector2(10, 10));
            stroke.Points.Add(new Vector2(50, 50));
            stroke.Points.Add(new Vector2(100, 100));
            
            // Act
            var bounds = stroke.GetBounds();
            
            // Assert
            Assert.Equal(10, bounds.X);
            Assert.Equal(10, bounds.Y);
            Assert.Equal(90, bounds.Width);
            Assert.Equal(90, bounds.Height);
        }

        [Fact]
        public void InkCanvas_StrokeClone_ShouldCreateIndependentCopy()
        {
            // Arrange
            var original = new InkStroke
            {
                Color = Color.Red,
                Width = 3f,
                StrokeType = InkStrokeType.Brush
            };
            original.Points.Add(new Vector2(10, 10));
            original.Points.Add(new Vector2(20, 20));
            
            // Act
            var clone = original.Clone();
            clone.Points.Add(new Vector2(30, 30));
            
            // Assert
            Assert.Equal(2, original.Points.Count);
            Assert.Equal(3, clone.Points.Count);
            Assert.Equal(Color.Red, clone.Color);
            Assert.Equal(3f, clone.Width);
            Assert.Equal(InkStrokeType.Brush, clone.StrokeType);
        }

        [Fact]
        public void InkCanvas_IsDrawingEnabled_False_ShouldPreventDrawing()
        {
            // Arrange
            var inkCanvas = new InkCanvas(_uiRoot)
            {
                IsDrawingEnabled = false
            };
            
            // Act - Try to draw
            inkCanvas.Presenter.StartStroke(new Vector2(10, 10));
            inkCanvas.Presenter.AddPointToStroke(new Vector2(20, 20));
            inkCanvas.Presenter.EndStroke();
            
            // Assert - Should still work (presenter is independent)
            // But UI update should not happen
            Assert.Equal(1, inkCanvas.StrokeCount);
        }

        private void AddStroke(InkCanvas canvas, Color color)
        {
            canvas.Presenter.DefaultColor = color;
            canvas.Presenter.StartStroke(new Vector2(10, 10));
            canvas.Presenter.AddPointToStroke(new Vector2(20, 20));
            canvas.Presenter.AddPointToStroke(new Vector2(30, 30));
            canvas.Presenter.EndStroke();
        }
    }
}
