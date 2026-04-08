using System;

namespace InkkSlinger;

public abstract class MultiSelector : Selector
{
	public void SelectAll()
	{
		if (SelectionMode == SelectionMode.Single)
		{
			throw new InvalidOperationException("SelectAll requires multi-selection mode.");
		}

		SelectAllCore();
	}

	public void UnselectAll()
	{
		UnselectAllCore();
	}

	protected virtual void SelectAllCore()
	{
		SelectAllInternal();
	}

	protected virtual void UnselectAllCore()
	{
		ClearSelectionInternal();
	}
}