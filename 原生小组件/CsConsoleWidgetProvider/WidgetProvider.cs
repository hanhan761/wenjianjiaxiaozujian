// Derived from the Microsoft Windows App SDK Widgets sample (MIT).
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using CsConsoleWidgetProvider;
using Microsoft.Windows.Widgets.Providers;

[ComVisible(true)]
[ComDefaultInterface(typeof(IWidgetProvider))]
[Guid("46501BCC-A025-45B6-A4A0-83715FC33E6F")]
public sealed class WidgetProvider : IWidgetProvider
{
    private static readonly ConcurrentDictionary<string, FolderWidget> Instances = new();

    public void CreateWidget(WidgetContext context) => Update(context, "create");

    public void Activate(WidgetContext context) => Update(context, "activate");

    public void OnWidgetContextChanged(WidgetContextChangedArgs args) => Update(args.WidgetContext, "resize");

    private static void Update(WidgetContext context, string reason)
    {
        if (context.DefinitionId != FolderWidget.DefinitionId)
            return;
        Instances.GetOrAdd(context.Id, id => new FolderWidget(id, "")).QueueRefresh(context.Size, reason);
    }

    public void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        var context = args.WidgetContext;
        if (context.DefinitionId != FolderWidget.DefinitionId)
            return;
        Instances.GetOrAdd(context.Id, id => new FolderWidget(id, "")).QueueAction(args.Verb, args.Data);
    }

    public void Deactivate(string widgetId) { }

    public void DeleteWidget(string widgetId, string state) => Instances.TryRemove(widgetId, out _);
}
