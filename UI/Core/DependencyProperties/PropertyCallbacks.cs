namespace InkkSlinger;

public delegate void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e);

public delegate object? CoerceValueCallback(DependencyObject d, object? baseValue);

public delegate bool ValidateValueCallback(object? value);
