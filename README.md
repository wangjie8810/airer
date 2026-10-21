# 晾衣绳

Windows 10 和 Windows 11 上的截图晾衣绳。截下来的图挂在屏幕顶部的一根绳子上，指针靠近屏幕上沿时绳子滑下来，离开就收回去。

## 致谢

感谢 [Alejandro Buján](https://github.com/alejandrobujan) 和他的 [Tendedero](https://github.com/alejandrobujan/tendedero)。绳子的交互和排布来自这个 Mac 应用：截图挂在屏幕顶部下垂的绳子上，指针靠近时滑下来，点一下就复制。他把源码按 MIT 许可公开，这个 Windows 移植才做得成。

Tendedero 的名字和图标属于原作者，不在 MIT 许可范围内。晾衣绳使用自己的名字和图标，是独立移植，不是 Tendedero，也没有获得 Alejandro Buján 的背书。原作者的版权声明见 [LICENSE](LICENSE)。

## 相对原项目的修改和改进

原项目只支持 macOS 14 及以上。晾衣绳面向 Windows 10（1607 起）和 Windows 11，x64 与 ARM64。下面这些按原版保留：绳子下垂、卡片尺寸、单击复制、双击打开、长按标记、拖进文件夹则文件留下、拖进别的程序则只复制一份、空绳子看过一次再收起后不再被屏幕上沿唤醒。

为 Windows 改过的部分：

- 用 .NET 8 和 Avalonia 重写，不再使用 Swift / AppKit。
- 长按改为用画图打开，双击用系统默认程序打开。
- 复制时同时放入 PNG 和位图，画图、微信等程序可以粘贴。
- 自己截的图丢弃时进回收站。
- 截图目录是 `%AppData%\Airer\Captures`。第一次启动会监视「图片\Screenshots」，因此 Win+PrtScn 的文件会挂上来。
- 登录时打开写在当前用户的 Run 项里。
- 界面语言跟随系统，中文或英文。
- 每块屏幕按自己的缩放放绳子。点绳子以外的空白处会穿透，不挡住后面的窗口。

相对原版新增的部分：

- 快捷键区域截图，默认 Ctrl+Alt+S。遮罩上拖选，Esc 或右键取消。截取前先关掉遮罩，绳子和遮罩不会进图，文件也不落到桌面。
- 快捷键截取指针所在的整块屏幕，默认 Ctrl+Alt+3。
- 显示绳子、区域截图、全屏截图这三个快捷键都可以改，也可以设成无。必须带 Ctrl、Alt 或 Win，不占用系统的 Win+Shift+S。
- 可以再添加要监视的文件夹。
- 「挂上复制的图片」默认关闭，避免把普通剪贴板内容挂上来。

## 1.1.0 的改进

- 全屏截图时，绳子会立刻收到画面外，不再把正在收回的卡片截进图里。
- 监视的网络盘或映射盘暂时没挂上时，目录会留在设置里。盘重新挂上后继续监视，不会被悄悄删掉。目录所在的磁盘已经挂上、文件夹本身不在了，才会从设置里移除。
- 新图片的缩略图在后台生成。截完一张大图之后，绳子靠近上沿时的反应不再被解码卡住。

## 安装

从 [Releases](https://github.com/wangjie8810/airer/releases) 下载安装包：

| 文件 | 用在 |
| --- | --- |
| `Airer-Setup-win-x64.exe` | 常见的 64 位 Windows 10 / 11 |
| `Airer-Setup-win-arm64.exe` | ARM64 的 Windows 10 / 11 |

双击安装。程序装到当前用户的 `%LocalAppData%\Programs\Airer`，不需要管理员。开始菜单里是「晾衣绳」。在 Windows 的「应用」里卸载。卸载会去掉登录时打开，但不会删除 `%AppData%\Airer` 里的截图和设置。

安装包没有代码签名。Windows 第一次打开时如果提示无法验证发布者，选择仍要运行。

不想安装的话，同一个 Release 里还有 zip，解压后直接运行里面的 `Airer.exe`。

## 快捷键

默认都可以在托盘菜单里改。一个快捷键需要带 Ctrl、Alt 或 Win。

| 快捷键 | 作用 |
| --- | --- |
| Ctrl+Alt+S | 区域截图。拖一块区域，松开后图片直接挂上，不经过桌面。Esc 取消。 |
| Ctrl+Alt+3 | 截取指针所在的那块屏幕。 |
| Ctrl+Alt+T | 显示或隐藏绳子。 |

Win+Shift+S 仍然是系统截图。打开托盘里的「挂上复制的图片」后，复制到剪贴板的纯图片也会挂上来。Win+PrtScn 存在「图片\Screenshots」里的文件会被监视到。

## 图片怎么用

| 操作 | 结果 |
| --- | --- |
| 单击 | 复制图片，可以粘贴到别的程序。 |
| 双击 | 用默认程序打开。 |
| 按住 | 用画图打开，方便标记。 |
| 拖到文件夹 | 文件留下，离开绳子。 |
| 拖到别的程序 | 对方拿到一份，绳子上还在。 |
| 角上的叉 | 自己截的图进回收站；别处监视来的图只从绳子上取下。 |
| 右键 | 复制、打开、在资源管理器中显示、取下左侧或右侧全部。 |

绳子上是空的时候，看过一次再收起后，光靠屏幕上沿不会再把它叫出来。快捷键和新的截图仍然可以。

## 环境

- Windows 10（1607 及以后）和 Windows 11
- x64 和 ARM64
- 每个显示器按自己的缩放显示，截图像素和屏幕像素对齐

## 自己编译

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```bash
dotnet test
dotnet run --project src/Airer/Airer.csproj
```

在 Windows 上打出可以直接拷走的程序：

```bash
dotnet publish src/Airer/Airer.csproj -c Release -r win-x64 --self-contained true -o dist/win-x64
dotnet publish src/Airer/Airer.csproj -c Release -r win-arm64 --self-contained true -o dist/win-arm64
```

`dist/win-x64/Airer.exe` 不需要另外安装运行时。从 macOS 交叉编译、并打出安装包：

```bash
scripts/publish-windows.sh
```

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 和 [NSIS](https://nsis.sourceforge.io/)（macOS 上是 `brew install makensis`）。安装包出现在 `dist/Airer-Setup-win-x64.exe` 和 `dist/Airer-Setup-win-arm64.exe`。

截图存在 `%AppData%\Airer\Captures`。设置在 `%AppData%\Airer\settings.json`。程序不联网。
