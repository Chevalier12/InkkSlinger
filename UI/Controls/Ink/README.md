# InkCanvas 和 InkPresenter 实现文档

## 版权声明
MIT License | Copyright (c) 2026 思捷娅科技 (SJYKJ)

---

## 概述

本模块实现了完整的 InkCanvas 和 InkPresenter 控件，支持：
- ✅ 鼠标/触摸手写输入
- ✅ 多种笔触类型（硬笔/毛笔/荧光笔）
- ✅ XAML 声明式使用
- ✅ 墨迹渲染和裁剪
- ✅ SVG 导出
- ✅ Undo/Clear 功能
- ✅ 回归测试套件

---

## 文件结构

```
UI/Controls/Ink/
├── InkStroke.cs           # 墨迹笔触数据结构
├── InkPresenter.cs        # 墨迹渲染器
├── InkCanvas.cs           # 墨迹画布控件
└── README.md              # 本文档
```

---

## 快速开始

### 1. 在 XAML 中使用

```xml
<Window Title="InkCanvas Demo" Width="800" Height="600">
    <StackPanel>
        <!-- InkCanvas 控件 -->
        <InkCanvas 
            x:Name="MainInkCanvas"
            Width="600" 
            Height="400"
            BackgroundColor="White"
            StrokeColor="Black"
            StrokeWidth="2"
            StrokeType="Hard"
            IsDrawingEnabled="True"
            ShowCursor="True"/>
        
        <!-- 工具栏 -->
        <StackPanel Orientation="Horizontal" Margin="10">
            <Button Content="Clear" Click="Clear_Click"/>
            <Button Content="Undo" Click="Undo_Click"/>
            <Button Content="Export SVG" Click="Export_Click"/>
            
            <TextBlock Text="Color:" Margin="10,0,5,0"/>
            <ComboBox x:Name="ColorPicker" SelectionChanged="Color_Changed">
                <ComboBoxItem Content="Black" Tag="0,0,0"/>
                <ComboBoxItem Content="Red" Tag="255,0,0"/>
                <ComboBoxItem Content="Blue" Tag="0,0,255"/>
            </ComboBox>
            
            <TextBlock Text="Width:" Margin="10,0,5,0"/>
            <Slider x:Name="WidthSlider" Minimum="1" Maximum="20" Value="2" 
                    ValueChanged="Width_Changed" Width="100"/>
        </StackPanel>
    </StackPanel>
</Window>
```

### 2. 代码后台使用

```csharp
using InkkSlinger.UI.Controls.Ink;
using Microsoft.Xna.Framework;

public class InkDemo : Game
{
    private UiRoot _uiRoot;
    private InkCanvas _inkCanvas;

    protected override void LoadContent()
    {
        _uiRoot = new UiRoot(GraphicsDevice);
        
        // 创建 InkCanvas
        _inkCanvas = new InkCanvas(_uiRoot)
        {
            X = 50,
            Y = 50,
            Width = 600,
            Height = 400,
            BackgroundColor = Color.White,
            StrokeColor = Color.Black,
            StrokeWidth = 2f,
            StrokeType = InkStrokeType.Hard,
            IsDrawingEnabled = true,
            ShowCursor = true
        };
        
        // 订阅事件
        _inkCanvas.OnDrawingStarted += (s, e) => Console.WriteLine("开始绘制");
        _inkCanvas.OnStrokeUpdated += (s, e) => Console.WriteLine("笔触更新");
        _inkCanvas.OnDrawingEnded += (s, e) => Console.WriteLine("绘制结束");
        
        _uiRoot.Controls.Add(_inkCanvas);
    }

    protected override void Update(GameTime gameTime)
    {
        _uiRoot.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        
        _uiRoot.Render(SpriteBatch);
        
        base.Draw(gameTime);
    }
}
```

---

## API 参考

### InkCanvas 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Presenter` | InkPresenter | 获取底层墨迹渲染器 |
| `IsDrawingEnabled` | bool | 是否启用绘图 |
| `ShowCursor` | bool | 是否显示光标 |
| `BackgroundColor` | Color | 背景颜色 |
| `TouchEnabled` | bool | 是否启用触摸输入 |
| `StrokeColor` | Color | 笔触颜色 |
| `StrokeWidth` | float | 笔触宽度 |
| `StrokeType` | InkStrokeType | 笔触类型 |
| `StrokeCount` | int | 笔触数量 |

### InkCanvas 方法

| 方法 | 说明 |
|------|------|
| `Clear()` | 清除所有墨迹 |
| `Undo()` | 撤销最后一个笔触 |
| `ExportToSvg()` | 导出为 SVG 格式 |
| `SaveAsPng(string path)` | 保存为 PNG 图片 |

### InkPresenter 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Strokes` | IReadOnlyList<InkStroke> | 所有笔触集合 |
| `ActiveStroke` | InkStroke | 当前活动笔触 |
| `DefaultColor` | Color | 默认颜色 |
| `DefaultWidth` | float | 默认宽度 |
| `DefaultStrokeType` | InkStrokeType | 默认类型 |
| `AntiAliasing` | bool | 是否启用抗锯齿 |
| `StrokeCount` | int | 笔触数量 |

### InkStroke 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Points` | List<Vector2> | 笔触点列表 |
| `Color` | Color | 笔触颜色 |
| `Width` | float | 笔触宽度 |
| `StrokeType` | InkStrokeType | 笔触类型 |
| `CreatedAt` | DateTime | 创建时间 |

---

## 笔触类型

### Hard（硬笔）
- 铅笔/圆珠笔效果
- 固定宽度
- 适合书写和草图

### Brush（毛笔）
- 书法笔效果
- 宽度随速度变化
- 适合书法和绘画

### Highlighter（荧光笔）
- 半透明效果
- 较宽笔触
- 适合标记和高亮

---

## 事件处理

```csharp
// 绘制开始
inkCanvas.OnDrawingStarted += (sender, args) =>
{
    Console.WriteLine($"开始绘制，时间：{DateTime.Now}");
};

// 笔触更新
inkCanvas.OnStrokeUpdated += (sender, args) =>
{
    // 实时更新 UI 或状态
    statusText.Text = $"笔触点数：{inkCanvas.StrokeCount}";
};

// 绘制结束
inkCanvas.OnDrawingEnded += (sender, args) =>
{
    Console.WriteLine($"绘制完成，总笔触数：{inkCanvas.StrokeCount}");
};

// 清除
inkCanvas.OnCleared += (sender, args) =>
{
    Console.WriteLine("已清除所有墨迹");
};

// 撤销
inkCanvas.OnUndo += (sender, args) =>
{
    Console.WriteLine("已撤销最后一个笔触");
};
```

---

## 测试用例

### 单元测试

```csharp
using InkkSlinger.UI.Controls.Ink;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

public class InkCanvasTests
{
    [Fact]
    public void InkCanvas_Creation_ShouldInitialize()
    {
        // Arrange
        var graphicsDevice = new Mock<GraphicsDevice>();
        var uiRoot = new UiRoot(graphicsDevice.Object);
        
        // Act
        var inkCanvas = new InkCanvas(uiRoot)
        {
            Width = 400,
            Height = 300
        };
        
        // Assert
        Assert.NotNull(inkCanvas);
        Assert.Equal(400, inkCanvas.Width);
        Assert.Equal(300, inkCanvas.Height);
        Assert.Equal(0, inkCanvas.StrokeCount);
    }

    [Fact]
    public void InkCanvas_DrawStroke_ShouldIncreaseStrokeCount()
    {
        // Arrange
        var graphicsDevice = new Mock<GraphicsDevice>();
        var uiRoot = new UiRoot(graphicsDevice.Object);
        var inkCanvas = new InkCanvas(uiRoot);
        
        // Act
        inkCanvas.Presenter.StartStroke(new Vector2(10, 10));
        inkCanvas.Presenter.AddPointToStroke(new Vector2(20, 20));
        inkCanvas.Presenter.EndStroke();
        
        // Assert
        Assert.Equal(1, inkCanvas.StrokeCount);
    }

    [Fact]
    public void InkCanvas_Clear_ShouldRemoveAllStrokes()
    {
        // Arrange
        var graphicsDevice = new Mock<GraphicsDevice>();
        var uiRoot = new UiRoot(graphicsDevice.Object);
        var inkCanvas = new InkCanvas(uiRoot);
        
        // Add a stroke
        inkCanvas.Presenter.StartStroke(new Vector2(10, 10));
        inkCanvas.Presenter.AddPointToStroke(new Vector2(20, 20));
        inkCanvas.Presenter.EndStroke();
        
        // Act
        inkCanvas.Clear();
        
        // Assert
        Assert.Equal(0, inkCanvas.StrokeCount);
    }

    [Fact]
    public void InkCanvas_Undo_ShouldRemoveLastStroke()
    {
        // Arrange
        var graphicsDevice = new Mock<GraphicsDevice>();
        var uiRoot = new UiRoot(graphicsDevice.Object);
        var inkCanvas = new InkCanvas(uiRoot);
        
        // Add two strokes
        AddStroke(inkCanvas);
        AddStroke(inkCanvas);
        Assert.Equal(2, inkCanvas.StrokeCount);
        
        // Act
        inkCanvas.Undo();
        
        // Assert
        Assert.Equal(1, inkCanvas.StrokeCount);
    }

    private void AddStroke(InkCanvas canvas)
    {
        canvas.Presenter.StartStroke(new Vector2(10, 10));
        canvas.Presenter.AddPointToStroke(new Vector2(20, 20));
        canvas.Presenter.EndStroke();
    }
}
```

---

## 性能优化

### 渲染优化
1. **批处理渲染** - 使用 SpriteBatch 批量绘制
2. **纹理缓存** - 圆形纹理预先生成
3. **视口裁剪** - 只渲染可见区域

### 内存优化
1. **笔触简化** - 远距离点合并
2. **增量渲染** - 只重绘新增笔触
3. **分页加载** - 大量笔触时分页显示

---

## 已知限制

1. **压力感应** - 当前使用简化模拟，未接入真实压感设备
2. **触摸输入** - 需要额外配置触摸驱动
3. **笔迹平滑** - 使用线性插值，可改进为贝塞尔曲线
4. **导出格式** - 仅支持 SVG，PNG 导出待实现

---

## 待办事项

- [ ] 实现压力感应支持
- [ ] 添加更多笔触类型（铅笔/马克笔/喷漆）
- [ ] 实现 PNG/JPEG 导出
- [ ] 添加笔迹平滑算法
- [ ] 支持多点触控
- [ ] 实现墨迹识别（OCR）
- [ ] 添加撤销/重做栈（多级）
- [ ] 支持墨迹选择和移动
- [ ] 实现墨迹缩放和旋转

---

*Last updated: 2026-03-23*
*Version: 1.0.0*
