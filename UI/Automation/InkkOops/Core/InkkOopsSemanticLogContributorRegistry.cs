using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace InkkSlinger;

public sealed class InkkOopsSemanticLogContributorRegistry
{
    private readonly List<IInkkOopsSemanticLogContributor> _contributors = new();

    public InkkOopsSemanticLogContributorRegistry Register<TElement>(
        InkkOopsSemanticLogTarget target,
        Expression<Func<TElement, object?>> propertyExpression,
        int order = 0)
        where TElement : UIElement
    {
        ArgumentNullException.ThrowIfNull(propertyExpression);

        var propertyName = GetPropertyName(propertyExpression);
        return Register(target, propertyName, propertyExpression.Compile(), order);
    }

    public InkkOopsSemanticLogContributorRegistry Register<TElement>(
        InkkOopsSemanticLogTarget target,
        string propertyName,
        Func<TElement, object?> selector,
        int order = 0)
        where TElement : UIElement
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(selector);

        _contributors.Add(new RegisteredContributor<TElement>(order, target, propertyName, selector));
        return this;
    }

    public IReadOnlyList<IInkkOopsSemanticLogContributor> Build()
    {
        return _contributors
            .OrderBy(static contributor => contributor.Order)
            .ToArray();
    }

    private static string GetPropertyName<TElement>(Expression<Func<TElement, object?>> propertyExpression)
        where TElement : UIElement
    {
        Expression body = propertyExpression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException("Property expression must access a readable member.", nameof(propertyExpression));
    }

    private sealed class RegisteredContributor<TElement> : IInkkOopsSemanticLogContributor
        where TElement : UIElement
    {
        private readonly InkkOopsSemanticLogTarget _target;
        private readonly string _propertyName;
        private readonly Func<TElement, object?> _selector;

        public RegisteredContributor(int order, InkkOopsSemanticLogTarget target, string propertyName, Func<TElement, object?> selector)
        {
            Order = order;
            _target = target;
            _propertyName = propertyName;
            _selector = selector;
        }

        public int Order { get; }

        public void Contribute(InkkOopsSemanticLogContext context, UIElement element, InkkOopsSemanticLogPropertyBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(builder);

            if (context.Target != _target || element is not TElement typedElement)
            {
                return;
            }

            builder.Add(_propertyName, _selector(typedElement));
        }
    }
}