// Derived from Microsoft's CountingWidget sample (MIT).
using FolderLauncher;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.Storage;

namespace CsConsoleWidgetProvider
{
    internal sealed class FolderWidget : WidgetImplBase
    {
        public static string DefinitionId => "FolderCards";
        private static readonly Lazy<string> Template = new(
            () => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Templates", "FolderWidget.json")));
        private static readonly object Gate = new();
        private string message = "";
        private bool manage;
        private int page;
        private int pageSize = 4;

        public FolderWidget(string widgetId, string initialState) : base(widgetId, initialState) { }

        private static Library Load() =>
            new(Path.Combine(ApplicationData.Current.LocalFolder.Path, "folders.xml"));

        internal void QueueRefresh(WidgetSize size, string reason) => Queue(() => Refresh(size, reason));

        internal void QueueAction(string verb, string data) => Queue(() => InvokeAction(verb, data));

        private Task pending = Task.CompletedTask;
        private readonly object queueLock = new();

        private void Queue(Action work)
        {
            lock (queueLock)
                pending = pending.ContinueWith(_ =>
                {
                    try { work(); }
                    catch (Exception e) { WidgetTrace.Failure("Callback", e); }
                }, TaskScheduler.Default);
        }

        internal void Refresh(WidgetSize size, string reason)
        {
            lock (Gate)
                pageSize = size == WidgetSize.Large ? 8 : 4;
            Update(reason);
        }

        private void InvokeAction(string verb, string rawData)
        {
            string folderToOpen = null;
            lock (Gate)
            {
                try
                {
                    var data = string.IsNullOrWhiteSpace(rawData) ? null : JsonNode.Parse(rawData) as JsonObject;
                    var library = Load();
                    message = "";
                    switch (verb)
                    {
                        case "add":
                            Add(library, data?["path"]?.GetValue<string>() ?? "", data?["name"]?.GetValue<string>() ?? "");
                            break;
                        case "open":
                        case "remove":
                            var path = data?["folder"]?.GetValue<string>();
                            var saved = library.Items.Find(item => Library.SamePath(item.Path, path));
                            if (saved == null)
                                break;
                            if (verb == "remove" && manage)
                                library.Save(library.Items.Where(item => item != saved).ToList());
                            else if (verb == "open")
                                folderToOpen = saved.Path;
                            break;
                        case "previous":
                            page = Math.Max(0, page - 1);
                            break;
                        case "next":
                            page++;
                            break;
                        case "manage":
                            manage = !manage;
                            break;
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is JsonException || e is System.ComponentModel.Win32Exception)
                {
                    message = "操作失败，请检查文件夹或存储位置。";
                }
            }
            if (folderToOpen != null)
            {
                try
                {
                    if (Directory.Exists(folderToOpen))
                        ExplorerWindow.OpenFolder(folderToOpen);
                    else
                        message = "找不到这个文件夹。";
                }
                catch (System.ComponentModel.Win32Exception e)
                {
                    WidgetTrace.Failure("Open folder", e);
                    message = "无法打开这个文件夹。";
                }

                if (message.Length == 0)
                    return;
            }
            Update("action");
        }

        private void Add(Library library, string input, string name)
        {
            string path;
            try { path = Library.Normalize(input); }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is NotSupportedException)
            {
                message = "请粘贴完整的文件夹路径。";
                return;
            }

            if (!Directory.Exists(path))
            {
                message = "找不到这个文件夹，请检查路径。";
                return;
            }
            if (library.Items.Any(item => Library.SamePath(item.Path, path)))
            {
                message = "这个文件夹已经添加。";
                return;
            }
            string caption = name.Trim();
            if (caption.Length > 64 || caption.Any(char.IsControl))
            {
                message = "名称请使用一行，最多 64 个字符。";
                return;
            }

            var items = library.Items.ToList();
            items.Insert(0, new Favorite {
                Name = caption.Length == 0 ? Library.Caption(path) : caption, Path = path, Group = Library.DefaultGroup
            });
            library.Save(items);
            page = 0;
            manage = false;
        }

        private void Update(string reason)
        {
            var options = new WidgetUpdateRequestOptions(Id)
            {
                Template = GetTemplateForWidget(), Data = GetDataForWidget(), CustomState = State
            };
            WidgetManager.GetDefault().UpdateWidget(options);
            WidgetTrace.Write("Published: " + reason);
        }

        public override string GetTemplateForWidget() => Template.Value;

        public override string GetDataForWidget()
        {
            Favorite[] items;
            lock (Gate)
            {
                try { items = Load().Items.ToArray(); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
                {
                    items = Array.Empty<Favorite>();
                    message = "无法读取收藏。";
                }
            }
            int lastPage = Math.Max(0, (items.Length - 1) / pageSize);
            page = Math.Min(page, lastPage);
            var rows = BuildRows(items.Skip(page * pageSize).Take(pageSize).ToArray(), manage);
            return new JsonObject
            {
                ["rows"] = rows,
                ["hasFolders"] = items.Length > 0,
                ["previous"] = page > 0,
                ["next"] = page < lastPage,
                ["manageLabel"] = manage ? "完成" : "管理",
                ["message"] = message,
                ["hasMessage"] = !string.IsNullOrEmpty(message)
            }.ToJsonString();
        }

        internal static JsonArray BuildRows(Favorite[] items, bool manage)
        {
            var rows = new JsonArray();
            for (int i = 0; i < items.Length; i += 2)
            {
                var row = new JsonObject
                {
                    ["left"] = BuildTile(items[i], manage),
                    ["hasRight"] = i + 1 < items.Length
                };
                if (i + 1 < items.Length)
                    row["right"] = BuildTile(items[i + 1], manage);
                rows.Add(row);
            }
            return rows;
        }

        private static JsonObject BuildTile(Favorite item, bool manage)
        {
            return new JsonObject
            {
                ["folder"] = item.Path,
                ["action"] = manage ? "remove" : "open",
                ["label"] = manage ? "移除 " + item.Name : "打开 " + item.Name,
                ["text"] = item.Name,
                ["icon"] = manage ? "−" : "📁"
            };
        }
    }
}
