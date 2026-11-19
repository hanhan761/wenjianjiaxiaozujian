# wenjianjiaxiaozujian

「文件夹卡片」是一个运行在 Windows 11 小组件面板中的本地文件夹收藏工具。按 `Win + W` 添加或打开常用文件夹。

## 使用

- 添加卡片时粘贴文件夹绝对路径，名称可以留空。已有收藏保存在本机应用数据中。
- 点击卡片打开对应的文件夹。通过「管理」可以移除收藏。
- 点击卡片底部的 **关闭所有文件夹窗口**，即可关闭当前 Windows 会话中的文件资源管理器文件夹窗口。最小化的文件夹窗口和同一窗口里的标签页也会关闭。关闭请求等同于点窗口右上角的关闭按钮。
- `Win + W` 仍用于打开 Windows 小组件面板。

浏览器、终端、编辑器和桌面外壳不属于关闭目标。

## 构建与本地注册

需要 Windows 11 x64、.NET 10 SDK、Windows SDK（包含 `makeappx.exe`）及 Windows App Runtime 1.8。当前源码使用 Windows App SDK 1.8。启用开发者模式后，在项目根目录执行：

```powershell
.\原生小组件\Build.ps1 -MakeAppx "C:\Program Files (x86)\Windows Kits\10\bin\<版本>\x64\makeappx.exe"
.\原生小组件\注册小组件.ps1
```

然后按 `Win + W`，在添加小组件菜单固定「文件夹卡片」。这是本地开发包。已有收藏在应用包升级时保留。

## 上游和许可

COM 注册和小组件实现参考 Microsoft 的 Windows App SDK Widgets 样例。上游 MIT 许可证随源码保留。 [WindowsAppSDK-Samples](https://github.com/microsoft/WindowsAppSDK-Samples/tree/main/Samples/Widgets/cs-console-packaged) · [MIT 许可证](原生小组件/LICENSE)
