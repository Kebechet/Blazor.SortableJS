using System.Diagnostics.CodeAnalysis;

namespace Kebechet.Blazor.SortableJS;

/// <summary>
/// Converts an item arriving from a list of another item type, or declines to accept it.
/// </summary>
/// <typeparam name="TItem">The destination collection's item type.</typeparam>
/// <param name="item">The item leaving the source list.</param>
/// <param name="converted">The converted item, or null when the destination declines it.</param>
/// <returns>True when the item was accepted; false leaves both collections untouched.</returns>
public delegate bool SortableTryConvert<TItem>(object item, [NotNullWhen(true)] out TItem? converted);
