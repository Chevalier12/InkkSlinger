# UI Folder Map (Upgraded)

Immediate parent class + one-line purpose per file.

```
UI/
  Animation
    Core
      AnimationManager.cs           → (helper)         Drives storyboard playback
      AnimationPropertyPathResolver.cs → (helper)     Resolves property paths for animation
    Easing
      Easing.cs                    → (static helper)   Easing function types (Back, Bounce, Cubic, Elastic, Quad, Quart, Quint, Sine)
    KeyFrames
      KeyFrames.cs                 → (abstract)        Base for key frame types
      KeyFrameTiming.cs            → (enum)            KeyFrame timing modes
      KeySpline.cs                 → (class)           Bezier control points for key splines
      ObjectKeyFrames.cs           → KeyFrames         Object value key frames
    Timelines
      AnimationTimeline.cs         → Timeline          Base for time-based animation
      Storyboard.cs                → Timeline          Begin/Pause/Resume/Stop/Seek storyboard
      Timeline.cs                  → (abstract)        Clock timeline contract
    Types
      AnimationPrimitives.cs      → (helper)          Boolean/Double/Color/Point animation primitives
      Int32Animations.cs           → (helper)          Int32 animation primitives
      PointThicknessAnimations.cs → (helper)          Point/Thickness animation types
  Automation
    AutomationManager.cs           → (sealed class)    Peer tree management and automation
    AutomationPeer.cs              → (abstract)        UIA provider base for controls
    AutomationPeerFactory.cs       → (static helper)   Creates peers on demand
    AutomationProperties.cs        → (static helper)   Name/Id/HelpText/Type/Status attached props
    AutomationTypes.cs             → (enums/EventArgs) Selection/ExpandCollapse/Grid/Table/Value patterns
  Binding
    Collections
      CollectionView.cs            → (abstract)        ICollectionView around a collection
      CollectionViewFactory.cs     → (static helper)   Creates views for collections
      CollectionViewGroup.cs       → (class)           Group node in grouped view
      CollectionViewSource.cs       → (class)           XAML view source with grouping/sorting
      GroupDescription.cs          → (abstract)        Groups items by property/path
      ICollectionView.cs           → (interface)       View interface with filtering/sorting/grouping
      ListCollectionView.cs        → CollectionView   List-specific ICollectionView
      PropertyGroupDescription.cs  → GroupDescription Groups by property value
      SortDescription.cs           → (struct)          Sort by property/direction
    Commands
      RelayCommand.cs              → (sealed class)    ICommand implementation
    Converters
      DelimitedMultiValueConverter.cs → IMultiValueConverter  Joins multi-values with delimiter
      IdentityValueConverter.cs   → IValueConverter   Pass-through converter
      IMultiValueConverter.cs      → (interface)       Multi-value conversion contract
      IValueConverter.cs           → (interface)       Single-value conversion contract
    Core
      Binding.cs                   → BindingBase       Path/Source/ElementName/RelativeSource/Converter
      BindingBase.cs               → (abstract)        Base for Binding/MultiBinding/PriorityBinding
      BindingExpression.cs         → IBindingExpression Implements two-way binding sync
      BindingExpressionUtilities.cs → (static helper)  Binding expression utilities
      BindingGroup.cs              → (class)           Validates all bindings on a scope
      BindingOperations.cs          → (static helper)   SetBinding/ClearBinding helpers
      IBindingExpression.cs        → (interface)       Binding expression contract
      MultiBinding.cs              → BindingBase       Multi-value binding
      MultiBindingExpression.cs    → IBindingExpression MultiBinding implementation
      PriorityBinding.cs           → BindingBase       Priority-based multi-binding
      PriorityBindingExpression.cs → IBindingExpression PriorityBinding implementation
    Types
      BindingEnums.cs              → (enums)           BindingMode, UpdateSourceTrigger, RelativeSourceMode
    Validation
      UpdateSourceExceptionFilterCallback.cs → (delegate)  Exception filter for binding errors
      Validation.cs                → (static helper)   HasError/Add/Remove error helpers
      ValidationError.cs           → (sealed class)    Single validation error
      ValidationResult.cs          → (sealed class)    Validation outcome with error
      ValidationRule.cs            → (abstract)        Base for custom validation
  Commanding
    CanExecuteRoutedEventArgs.cs  → EventArgs         CanExecute callback args
    CommandBinding.cs             → (sealed class)     Command→handler mapping
    CommandManager.cs             → (static helper)    Execute/CanExecute routing and requery
    CommandSourceExecution.cs     → (static helper)   Executes ICommandSource on CanExecute
    CommandTargetResolver.cs      → (static helper)   Resolves command targets
    EditingCommands.cs            → (static class)     ~60 routed editing commands
    ExecutedRoutedEventArgs.cs    → EventArgs         Execute callback args
    ICommandSource.cs             → (interface)       Marks element as command source
    NavigationCommands.cs         → (static class)    ~8 routed navigation commands
    RoutedCommand.cs              → (class)           ICommand with routed execution
    RoutedUICommand.cs            → RoutedCommand     Adds Text property
  Controls
    Adorners
      Adorner.cs                  → FrameworkElement Overlay visual for an element
      AdornerDecorator.cs         → Decorator         Hosts AdornerLayer as sibling to child
      AdornerLayer.cs              → Panel             Manages adorners for an element
      AdornerTrackingMode.cs       → (enum)            Adorner tracking modes
      AnchoredAdorner.cs           → Adorner           Tracks render bounds or layout slot
      HandleKinds.cs               → (enums)           Resize handle kinds and drag args
      HandlesAdornerBase.cs        → AnchoredAdorner   Resize handles via Thumb drag
    Base
      ContentControl.cs            → Control           Single content host + ContentTemplate
      Control.cs                   → FrameworkElement  Template + background/foreground/border props
      Decorator.cs                 → FrameworkElement  Single-child wrapper
      FrameworkElement.cs          → UIElement         Layout (Measure/Arrange), styles, data context, name scope
      ItemsControl.cs               → Control           ItemsSource/ItemTemplate/ItemsPanel/ItemContainerStyle
      MultiSelector.cs             → Selector          Multi-selection bridge (SelectAll/UnselectAll)
      Panel.cs                     → FrameworkElement  Children collection, ZIndex, background fill
      Selector.cs                  → ItemsControl       SelectedIndex/SelectedItem/SelectedValue
      UIElement.cs                 → DependencyObject  Visual tree, hit testing, routed events, input
    Buttons
      Button.cs                    → ButtonBase        Clickable with auto-style text rendering
      CheckBox.cs                  → ToggleButton      Checkbox glyph + text (three-state)
      RadioButton.cs               → ToggleButton      GroupName mutual exclusion
      RepeatButton.cs              → ButtonBase        Fires repeatedly when held
      Thumb.cs                     → Control           Draggable handle for resize/move
      ToggleButton.cs              → ButtonBase        IsChecked state (bool?)
    Containers
      DocumentViewer.cs            → Control           Paginated document view
      ExpandDirection.cs            → (enum)            Expander expand directions
      Expander.cs                  → ContentControl     Collapsible header/content
      Frame.cs                     → ContentControl     Journal-based navigation
      GridResizeBehavior.cs         → (enum)            GridSplitter resize behavior
      GridResizeDirection.cs        → (enum)            GridSplitter resize direction
      GridSplitter.cs               → Control           Resize adjacent grid cells
      GroupBox.cs                  → ContentControl     Titled border container
      NavigationService.cs         → (class)           Frame navigation service
      Page.cs                      → ContentControl     Navigation page
      Popup.cs                     → ContentControl     Overlay window
      PopupPlacementMode.cs         → (enum)            Popup placement modes
      ResizeGrip.cs                → Control           Window resize grip
      StatusBar.cs                 → ItemsControl      Status bar
      StatusBarItem.cs             → ContentControl     Status bar item
      ToolBar.cs                   → ItemsControl       Toolbar
      ToolBarTray.cs               → Panel             Arranges toolbars
      ToolTip.cs                   → Popup              Tooltip with ShowFor helper
      ToolTipService.cs             → (static helper)   Attached props for tooltip timing
      UserControl.cs               → ContentControl     Composite user control
      Viewbox.cs                   → ContentControl     Scales child to fill
      Window.cs                    → (class)           Window with native adapter
      WindowThemeBinding.cs         → (helper)          Propagates Window theme to root
    DataGrid
      DataGrid.cs                  → MultiSelector      Full grid: rows, columns, editing, virtualization, automation
      DataGridCell.cs              → Control            Single cell with editing state
      DataGridCellInfo.cs           → (struct)          Immutable cell coordinate
      DataGridColumn.cs            → FrameworkElement   Column definition base
      DataGridColumnHeader.cs      → Button             Per-column header with sort
      DataGridColumnHeadersPresenter.cs → FrameworkElement Hosts column header lane
      DataGridDetailsPresenter.cs  → ContentControl     Row details section
      DataGridEditingEventArgs.cs  → (EventArgs)        Edit begin/commit/cancel context
      DataGridEnums.cs             → (enums)            SelectionMode/SelectionUnit/GridLines/EditAction
      DataGridRow.cs               → Control            Row container with cells
      DataGridRowHeader.cs         → Control            Row number display lane
      DataGridRowHeaderLaneCoordinator.cs → (helper)   Syncs frozen row header lane offsets
      DataGridRowsPresenter.cs    → VirtualizingStackPanel Realizes row containers
      DataGridState.cs             → (helper)           Editing/committed/cancelled state objects
    Inputs
      Calendar.cs                  → UserControl        Date picker calendar
      CalendarDateRange.cs          → (readonly struct)  Date range for Calendar
      CalendarDayButton.cs          → Button             Calendar day cell button
      CalendarSelectionMode.cs       → (enum)            Calendar selection modes
      DatePicker.cs                → UserControl        Date picker with popup calendar
      IHyperlinkHoverHost.cs       → (interface)       Marks control that hosts hyperlinks
      ITextInputControl.cs          → (interface)        Text input contract
      PasswordBox.cs                → Control            Masked text input
      RichTextBox.cs                → Control            Multi-line rich text editor (partial group)
      RichTextBox.FormattingEngine.cs → (helper)         Formatting operations
      RichTextBox.ListOperations.cs → (helper)           List/paragraph operations
      RichTextBox.Navigation.cs    → (helper)           Navigation gestures
      RichTextBox.TableOperations.cs → (helper)         Table structure operations
      RichTextBoxPerformanceTracker.cs → (helper)       Performance instrumentation
      Slider.cs                     → RangeBase         Tick/bar track control
      SliderTypes.cs                → (enums)           TickPlacement/AutoToolTipPlacement
      SpellCheck.cs                 → (static helper)   Attached IsEnabled + CustomDictionaries
      TextBox.cs                    → Control            Single-line text input
    Items
      ComboBox.cs                  → Selector           Dropdown selector
      ComboBoxItem.cs               → ListBoxItem        ComboBox list item
      ContextMenu.cs               → ItemsControl       Context popup menu
      ListBox.cs                   → Selector           Virtualizable item list
      ListBoxItem.cs                → ContentControl     Selected/Unselected events
      ListView.cs                  → ListBox            ListView with View property
      ListViewItem.cs               → ListBoxItem        ListView item
      Menu.cs                      → ItemsControl       Menu bar
      MenuAccessText.cs             → (helper)          Parses "_File" access key markers
      MenuItem.cs                   → ItemsControl       Menu item with Header/InputGestureText
      TabControl.cs                → Selector           Tabbed panel
      TabItem.cs                   → ContentControl     Tab with Header string
      TreeView.cs                  → ItemsControl       Tree with SelectedItemProperty
      TreeViewItem.cs               → ItemsControl       Expandable tree node
    Panels
      Canvas.cs                    → Panel              Absolute positioned children
      DockPanel.cs                  → Panel              Dock-attached child layout
      Grid.cs                       → Panel              Row/column grid layout
      StackPanel.cs                 → Panel              Linear stacking (Orientation)
      ToolBarOverflowPanel.cs        → (helper)          Overflow layout for toolbar
      ToolBarPanel.cs               → (helper)           Toolbar shared panel logic
      UniformGrid.cs                → Panel              Uniform rows/columns grid
      VirtualizingStackPanel.cs     → Panel              IScrollTransform-backed virtualization
      WrapPanel.cs                  → Panel              Wrapping line flow
    Presenters
      GridViewRowPresenter.cs      → ContentPresenter   GridView row presenter
      GroupItem.cs                  → HeaderedItemsControl Group container
      Presenters.cs                 → (contains multiple types)
        ContentPresenter            → FrameworkElement   Presents single content
        ItemsPresenter              → FrameworkElement   Presents items
        HeaderedContentControl      → ContentControl    Header + content
        HeaderedItemsControl        → ItemsControl       Header + items
    Primitives
      AccessText.cs                → TextBlock           Underlines access keys ("_File")
      Border.cs                    → Decorator           Border with CornerRadius rendering
      Image.cs                     → SurfacePresenterBase Image from Texture2D
      ImageSource.cs               → (sealed class)     Texture2D wrapper
      Label.cs                     → ContentControl      Text label (obsolete ContentControl)
      ProgressBar.cs               → RangeBase           Progress indicator with fill
      RangeBase.cs                 → Control             Minimum/Maximum/Value base
      RenderSurface.ManagedBackend.cs → (helper)        Managed render surface backend
      RenderSurface.cs             → SurfacePresenterBase Hosts managed graphics surface
      Separator.cs                 → Control             Horizontal/vertical separator
      Shape.cs                     → FrameworkElement    Abstract vector graphics (Fill/Stroke)
      SurfacePresenterBase.cs      → FrameworkElement    Image/RenderSurface base
      TickBar.cs                   → FrameworkElement    Slider tick marks rendering
      TextBlock.cs                  → FrameworkElement    Static text display
    Scrolling
      IScrollTransformContent.cs   → (interface)         Marks content that owns a scroll transform
      ScrollBar.cs                 → RangeBase           Scrollbar chrome: thumb drag, line/page stepping
      ScrollBarVisibility.cs        → (enum)             ScrollBar visibility modes
      Track.cs                     → Panel               Thumb + track region management
      ScrollViewer.cs               → ContentControl      Extents/viewport/offset owner, scroll sync host
      VirtualizationEnums.cs        → (enums)            VirtualizationMode/CacheLengthUnit
    Selection
      SelectionMode.cs             → (enum)             Single/Multiple/Extended
      SelectionModel.cs             → (sealed class)     Tracks selected indices/items
      SelectionModelChangedEventArgs.cs → EventArgs      Selection change args
  Core
    DoubleCollection.cs             → (helper)           Double collection for geometry
    DependencyProperties
      DependencyObject.cs          → (helper)           Effective value resolution, property storage
      DependencyProperty.cs         → (helper)           Registry and metadata for registered properties
      DependencyPropertyChangedEventArgs.cs → EventArgs Property change args
      DependencyPropertyValueSource.cs → (enum)           Local/StaticResource/DynamicResource/Style/Template
      DependencyValueCoercion.cs    → (helper)           Value coercion helpers
      FrameworkPropertyMetadata.cs  → (class)            Framework metadata options + callbacks
      FrameworkPropertyMetadataOptions.cs → (enum/Flags)  Framework metadata flags
      PropertyCallbacks.cs          → (helper)           Property change callback delegates
    Naming
      NameScope.cs                  → (class)            XAML namescope registration
      NameScopeService.cs           → (static helper)     Namescope resolution
    Threading
      Dispatcher.cs                 → (static helper)    Thread affinity verification
    Freezable.cs                    → (abstract)         Freeze-once objects (Brush/Geometry/Effect)
  Diagnostics
    CatalogDatagridOpenLag          → (helper)           Datagrid open lag diagnostic
    DatagridSortClickLag             → (helper)           Sort click lag diagnostic
    XamlDiagnostic.cs               → (class)             Diagnostic/trace types
    XamlDiagnosticCode.cs           → (class)             Diagnostic code types
  Events
    Args
      FocusChangedRoutedEventArgs.cs → RoutedEventArgs   Focus change args
      HyperlinkNavigateRoutedEventArgs.cs → RoutedEventArgs Hyperlink navigation args
      KeyRoutedEventArgs.cs         → RoutedEventArgs     Keyboard args
      MouseRoutedEventArgs.cs       → RoutedEventArgs     Mouse move/button args
      MouseWheelRoutedEventArgs.cs  → RoutedEventArgs     Wheel delta args
      RoutedDragEventArgs.cs        → RoutedEventArgs     Drag args
      RoutedSimpleEventArgs.cs      → RoutedEventArgs     Simple routed event args
      SelectionChangedEventArgs.cs   → RoutedEventArgs     Selection change args
      TextInputRoutedEventArgs.cs   → RoutedEventArgs     Text input args
    Core
      EventManager.cs               → (static helper)    Class-level routed event handler registration
      RoutedEvent.cs                → (sealed class)     Event identifier with routing strategy
      RoutedEventArgs.cs            → (class)            Base args with Handled property
    Types
      RoutingStrategy.cs             → (enum)             Bubble/Tunnel/Direct
  Geometry
    Core
      Geometry.cs                  → (abstract)          FillRule, GeometryCombineMode, GeometryFigure
      Transform.cs                 → (abstract)          2D transform matrix
    Parsing
      PathMarkupParser.cs           → (helper)           Parses Path markup syntax
  Input
    Core
      AccessKeyService.cs           → (helper)           Access key input handling
      FocusManager.cs               → (static helper)     Focus helpers
      InputGestureService.cs         → (helper)            Input gesture resolution
      InputManager.cs               → (sealed class)      Keyboard/mouse state capture
    State
      InputDelta.cs                 → (struct)            Input delta for pointer
      InputDispatchState.cs         → (class)              Dispatch state snapshot
      InputSnapshot.cs               → (struct)            Input state snapshot
    Types
      InputBinding.cs               → (class)             Input gesture binding
      KeyBinding.cs                 → InputBinding         KeyGesture binding
      KeyGesture.cs                 → (class)              Key + modifiers
      ModifierKeys.cs                → (enum/Flags)         Keyboard modifiers
      MouseBinding.cs               → InputBinding          MouseGesture binding
      MouseButton.cs                → (enum)               Mouse buttons
      MouseGesture.cs               → (class)              Mouse button + modifiers
  Layout
    Types
      Alignment.cs                  → (enums)             HorizontalAlignment/VerticalAlignment
      CornerRadius.cs               → (struct)            Border corner radius
      Dock.cs                       → (enum)               Left/Top/Right/Bottom
      LayoutRect.cs                 → (readonly struct)    Layout rectangle
      Orientation.cs                → (enum)               Horizontal/Vertical
      Stretch.cs                    → (enum)               Stretch modes
      StretchDirection.cs           → (enum)               Up/Down/Both
      Thickness.cs                  → (struct)             Thickness (left/top/right/bottom)
      Visibility.cs                 → (enum)               Visible/Hidden/Collapsed
  Managers
    Layout
      FrameworkElementExtensions.cs → (helper)             FindName extension
      LayoutManager.cs              → (sealed class)      Layout pass orchestration
    Root
      Services
        IUiRootUpdateParticipant.cs → (interface)         Marks component as frame-update participant
        UiRootDirtyRegionOps.cs     → (helper)             Dirty region operations
        UiRootDraw.cs               → (helper)            Render scheduling and retained list sync
        UiRootFrameState.cs         → (helper)             Frame-local input/pointer caches
        UiRootFrameUpdates.cs       → (helper)             Frame update participants
        UiRootInputPipeline.cs      → (helper)             Pointer-target resolution, hover retargeting, overlay dismiss
        UiRootLayoutScheduler.cs    → (helper)             Layout scheduling
        UiRootRetainedTree.cs       → (helper)             Retained render tree management
        UiRootVisualIndex.cs        → (helper)            Visual index and hit-test ordering
      UiRoot.cs                     → (sealed partial)     Visual root, render pipeline, input pipeline
      UiRootTypes.cs                → (enums/records)     Telemetry types
    Tree
      VisualTreeHelper.cs           → (static helper)     HitTest, GetAncestors, GetChildren
  Rendering
    Core
      UiDrawing.cs                  → (static helper)     SpriteBatch draw helpers
    DirtyRegions
      DirtyRegionTracker.cs         → (class)              Dirty region tracking
      IRenderDirtyBoundsHintProvider.cs → (interface)      Dirty bounds hint provider
    Text
      UiRuntimeFontBackend.cs       → (helper)             Font backend and glyph atlas
      UiTextRenderer.cs              → (helper)            Text rendering with font/texture cache
      UiTextTypes.cs                 → (structs/enums)     Glyph atlas, typography, metrics
  Resources
    Core
      ResourceDictionary.cs          → (IDictionary)       Merged resource dictionaries
      ResourceReferenceExpressions.cs → (helper)            Resource reference expressions
      ResourceResolver.cs            → (static helper)      Resource lookup
      UiApplication.cs               → (sealed singleton)   Application resources
    Types
      Brush.cs                      → Freezable            Base brush type
      ResourceDictionaryChangedEventArgs.cs → EventArgs    Resource change args
      SolidColorBrush.cs            → Brush                 Solid color fill
  Styling
    Actions
      SetValueAction.cs             → TriggerAction         Sets property value
      StoryboardActions.cs          → TriggerAction         BeginStoryboard action
      TriggerAction.cs               → (abstract)           Base for trigger actions
    Core
      EventSetter.cs                → SetterBase           Event handler attachment
      ImplicitStylePolicy.cs         → (static helper)     Implicit style lookup policy
      Setter.cs                     → SetterBase           Property value setter
      SetterBase.cs                  → (abstract)           Setter base
      Style.cs                      → (sealed class)       CWT-based style application
      StyleSelector.cs               → (abstract)          Custom style selection
      StyleValueCloneUtility.cs      → (helper)            Style freeze clone
      VisualStateManager.cs          → (static helper)      GoToState with template root
      VisualStates.cs                → (classes)           VisualState/VisualStateGroup
    Effects
      Effects.cs                    → (DropShadowEffect)   Drop shadow effect (Freezable)
    GroupStyle.cs                    → (sealed class)      GroupStyle for items
    Triggers
      Condition.cs                  → (sealed class)       Trigger condition
      DataTrigger.cs                → TriggerBase           Binding-based condition
      EventTrigger.cs               → TriggerBase           Routed event condition
      MultiDataTrigger.cs           → TriggerBase           AND of multiple binding conditions
      MultiTrigger.cs               → TriggerBase           AND of multiple property conditions
      Trigger.cs                    → TriggerBase           Single property condition
      TriggerBase.cs                 → (abstract)           Base for all triggers
  Templating
    Core
      ControlTemplate.cs            → (sealed class)      Control template factory + TemplateBinding
      ItemsPanelTemplate.cs          → (sealed class)       ItemsPanel template
      TemplateBinding.cs             → (sealed class)       TemplateBinding markup
      TemplateTriggerEngine.cs       → (static helper)       Template trigger resolution
    Data
      DataTemplate.cs               → (sealed class)        Data→UIElement factory
      DataTemplateResolver.cs        → (static helper)       Template resolution
      DataTemplateSelector.cs        → (abstract)            Custom template selection
    Types
      TemplatePartAttribute.cs       → (Attribute)          Template part declaration
  Text
    Core
      AccessTextParser.cs            → (static helper)      Parses "_File" access key markers
      TextLayout.cs                  → (class)               Text measurement and layout
    Documents
      LogicalDirection.cs            → (enum)               Backward/Forward
      Operations
        DocumentOperations.cs        → (interfaces)         Document operation contracts
      DocumentEditing.cs             → (class)             Undo/redo and grouping policy
      DocumentModel.cs                → (class)             TextElement→DependencyObject tree, FlowDocument root
      DocumentPointers.cs             → (structs)           TextPointer/TextRange/DocumentTextSelection
      FlowDocumentSerialization.cs   → (static class)       XAML XML round-trip
      SpellingError.cs               → (sealed class)       Spelling error info
    Editing
      TextClipboard.cs               → (static class)       Win32 clipboard interop
      TextEditingBuffer.cs           → (sealed class)       Piece table text storage
      TextSelection.cs               → (struct)             Anchor + caret positions
    Layout
      DocumentLayoutEngine.cs        → (class)              Document layout with lines/runs
    Types
      TextWrapping.cs                → (enum)               NoWrap/Wrap/WrapWithOverflow
    Viewing
      DocumentPageMap.cs             → (helper)             Page→offset mapping
      DocumentViewerInteractionState.cs → (helper)          Multi-click detection
      DocumentViewportController.cs  → (static class)       Scroll/clamp/hit-test helpers
  Xaml
    Core
      XamlLoader.Attributes.cs       → (partial)            Attribute application
      XamlLoader.Bindings.cs        → (partial)             Binding parsing/application
      XamlLoader.cs                  → (partial)             Main XAML parser entry points
      XamlLoader.Diagnostics.cs      → (partial)             Contextual error reporting
      XamlLoader.Document.cs         → (partial)            Flow/rich text content handling
      XamlLoader.Elements.cs         → (partial)            Element construction/tree wiring
      XamlLoader.MarkupExtensions.cs → (partial)            Markup extension parsing
      XamlLoader.Resources.cs        → (partial)            Resource dictionaries and merge
      XamlLoader.RichText.cs         → (partial)            Rich text XAML handling
      XamlLoader.Session.cs          → (partial)            Load session state
      XamlLoader.StylesTemplates.cs  → (partial)            Styles/templates/triggers
      XamlLoader.Types.cs            → (partial)            Type resolution
      XamlLoader.Values.cs           → (partial)            Scalar/object value conversion
      XamlLoadSession.cs             → (class)               Load session state
      XamlObjectFactory.cs            → (class)              Object instantiation
      XamlTypeResolver.cs            → (class)              XAML type resolution
```
