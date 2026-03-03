using System;
using System.Collections.Generic;

namespace InkkSlinger;

public class Frame : ContentControl
{
    private readonly Stack<object> _backStack = new();
    private readonly Stack<object> _forwardStack = new();
    private readonly NavigationService _navigationService;
    private bool _isApplyingNavigationChange;
    private Page? _attachedPage;

    public Frame()
    {
        _navigationService = new NavigationService(this);
    }

    public bool CanGoBack => _backStack.Count > 0;

    public bool CanGoForward => _forwardStack.Count > 0;

    public NavigationService NavigationService => _navigationService;

    public bool Navigate(object content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Preserve WPF-like journal semantics: navigating to the current instance
        // still creates a journal entry instead of becoming a no-op.
        if (Content != null)
        {
            _backStack.Push(Content);
        }

        _forwardStack.Clear();
        SetContentFromNavigation(content);
        return true;
    }

    public void GoBack()
    {
        if (!CanGoBack)
        {
            throw new InvalidOperationException("Cannot navigate back because no back entry exists.");
        }

        if (Content != null)
        {
            _forwardStack.Push(Content);
        }

        var previous = _backStack.Pop();
        SetContentFromNavigation(previous);
    }

    public void GoForward()
    {
        if (!CanGoForward)
        {
            throw new InvalidOperationException("Cannot navigate forward because no forward entry exists.");
        }

        if (Content != null)
        {
            _backStack.Push(Content);
        }

        var next = _forwardStack.Pop();
        SetContentFromNavigation(next);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property != ContentProperty)
        {
            return;
        }

        UpdateAttachedPage(args.OldValue, args.NewValue);

        if (_isApplyingNavigationChange)
        {
            return;
        }

        ClearJournal();
    }

    private void SetContentFromNavigation(object content)
    {
        _isApplyingNavigationChange = true;
        try
        {
            Content = content;
        }
        finally
        {
            _isApplyingNavigationChange = false;
        }
    }

    private void UpdateAttachedPage(object? oldContent, object? newContent)
    {
        if (ReferenceEquals(oldContent, _attachedPage))
        {
            _attachedPage?.SetNavigationService(null);
            _attachedPage = null;
        }

        if (newContent is Page newPage)
        {
            newPage.SetNavigationService(_navigationService);
            _attachedPage = newPage;
        }
        else
        {
            _attachedPage = null;
        }
    }

    private void ClearJournal()
    {
        _backStack.Clear();
        _forwardStack.Clear();
    }
}
