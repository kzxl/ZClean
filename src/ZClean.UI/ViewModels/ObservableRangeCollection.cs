using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ZClean.UI.ViewModels;

/// <summary>
/// An ObservableCollection that supports batch updates (AddRange, ReplaceRange) 
/// to eliminate UI notification storms and improve rendering performance.
/// </summary>
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public ObservableRangeCollection() : base() { }

    public ObservableRangeCollection(IEnumerable<T> collection) : base(collection) { }

    public ObservableRangeCollection(List<T> list) : base(list) { }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }

    /// <summary>
    /// Replaces the current contents with the provided collection using a single Reset notification.
    /// </summary>
    public void ReplaceRange(IEnumerable<T> collection)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        _suppressNotification = true;
        try
        {
            Items.Clear();
            foreach (var item in collection)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Appends a collection of items using a single Reset notification.
    /// </summary>
    public void AddRange(IEnumerable<T> collection)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        var list = collection as IList<T> ?? collection.ToList();
        if (list.Count == 0) return;

        _suppressNotification = true;
        try
        {
            foreach (var item in list)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
