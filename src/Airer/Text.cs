using System.Globalization;

namespace Airer;

public static class Text
{
    public static bool Zh { get; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";

    public static string AppName => Zh ? "晾衣绳" : "Airer";
    public static string EmptyHint => Zh ? "截个图，它就会挂在这里" : "Take a screenshot and it will hang here";
    public static string Copied => Zh ? "已复制" : "Copied";
    public static string ShowLine => Zh ? "显示晾衣绳" : "Show line";
    public static string HideLine => Zh ? "隐藏晾衣绳" : "Hide line";
    public static string RegionShot => Zh ? "区域截图" : "Region screenshot";
    public static string ScreenShot => Zh ? "全屏截图" : "Screen screenshot";
    public static string Keep => Zh ? "绳上保留" : "Keep on line";
    public static string AsManyAsFit => Zh ? "放得下的数量" : "As many as fit";
    public static string Photos(int n) => Zh ? $"{n} 张" : $"{n} photos";
    public static string Size => Zh ? "大小" : "Size";
    public static string Small => Zh ? "小" : "Small";
    public static string Medium => Zh ? "中" : "Medium";
    public static string Large => Zh ? "大" : "Large";
    public static string Sounds => Zh ? "声音" : "Sounds";
    public static string HangCopied => Zh ? "挂上复制的图片" : "Hang copied images";
    public static string RevealOnNew => Zh ? "有新图时放下绳子" : "Bring the line down for a new photo";
    public static string RevealOnTop => Zh ? "指针靠近屏幕顶部时放下" : "Bring the line down at the top edge";
    public static string Watch => Zh ? "同时监视文件夹" : "Also watch folders";
    public static string AddFolder => Zh ? "添加文件夹…" : "Add Folder…";
    public static string StopWatching => Zh ? "停止监视" : "Stop Watching";
    public static string OpenFolder => Zh ? "打开截图文件夹" : "Open screenshots folder";
    public static string TakeAllDown => Zh ? "全部取下" : "Take everything down";
    public static string Shortcuts => Zh ? "快捷键" : "Shortcuts";
    public static string Change => Zh ? "更改…" : "Change…";
    public static string None => Zh ? "无" : "None";
    public static string OpenAtLogin => Zh ? "登录时打开" : "Open at login";
    public static string Quit => Zh ? "退出" : "Quit";
    public static string Thanks => Zh ? "致谢" : "Thanks";
    public static string ThanksTitle => Zh ? "感谢 Alejandro Buján" : "Thanks to Alejandro Buján";
    public static string ThanksBody => Zh
        ? "感谢 Alejandro Buján。绳子的交互和排布来自他的 Mac 应用 Tendedero（https://github.com/alejandrobujan/tendedero），他把源码以 MIT 许可公开，这个移植才做得到。名称和图标属于原作者，不在许可之内。晾衣绳是独立移植，不是 Tendedero，也没有得到他的背书。\n\n相对原版：绳子上的手势按原样保留。为 Windows 改了画图、回收站、剪贴板和截图目录。新增了可以修改的区域截图和全屏截图快捷键。"
        : "Thanks to Alejandro Buján. The line interaction and layout come from his Mac app Tendedero (https://github.com/alejandrobujan/tendedero); this Windows port exists because he released the source under the MIT License. The name and icon belong to him and are outside that grant. Airer is an independent port, not Tendedero, and is not endorsed by him.\n\nKept: the gestures on the line. Changed for Windows: Paint, the Recycle Bin, the clipboard, and the capture folder. Added: changeable shortcuts for a region snip and a full-screen shot.";
    public static string Copy => Zh ? "复制" : "Copy";
    public static string Open => Zh ? "打开" : "Open";
    public static string Edit => Zh ? "标记" : "Markup";
    public static string ShowInFolder => Zh ? "在资源管理器中显示" : "Show in folder";
    public static string SaveToDesktop => Zh ? "保存到桌面" : "Save to Desktop";
    public static string Discard => Zh ? "丢弃" : "Discard";
    public static string TakeDown => Zh ? "取下" : "Take down";
    public static string Trash => Zh ? "移到回收站" : "Move to Recycle Bin";
    public static string TakeLeft => Zh ? "取下左侧全部" : "Take down everything to the left";
    public static string TakeRight => Zh ? "取下右侧全部" : "Take down everything to the right";
    public static string EmptyTip => Zh ? "晾衣绳上是空的" : "The line is empty";
    public static string CountTip(int n) => Zh ? $"晾衣绳上有 {n} 张" : $"{n} on the line";
    public static string BusyHotkey => Zh ? "（占用）" : " (in use)";
    public static string WelcomeTitle => Zh ? "截图会挂在绳子上" : "Screenshots hang on the line";
    public static string WelcomeBody => Zh
        ? "Ctrl+Alt+S 框选一块区域，Ctrl+Alt+3 截取当前屏幕。图片直接挂上，不落到桌面。\n\n单击复制，双击打开，按住用画图标记，拖进文件夹就留下，点角上的叉丢掉。Ctrl+Alt+T 显示或隐藏绳子。快捷键都能在托盘菜单里改。"
        : "Ctrl+Alt+S snips a region, Ctrl+Alt+3 grabs the current screen. The picture hangs at once and skips the Desktop.\n\nClick to copy, double-click to open, hold to mark it up, drag into a folder to keep it, or click the cross to let it go. Ctrl+Alt+T shows or hides the line. Change any shortcut from the tray menu.";
    public static string GotIt => Zh ? "知道了" : "Got it";
    public static string PressShortcut => Zh ? "请按下新的快捷键" : "Press the new shortcut";
    public static string ShortcutHint => Zh ? "需要带 Ctrl、Alt 或 Win。Esc 取消，Delete 移除快捷键。" : "Include Ctrl, Alt, or Win. Escape cancels. Delete removes the shortcut.";
    public static string Cancel => Zh ? "取消" : "Cancel";
    public static string SnipHint => Zh ? "拖拽选择区域，Esc 取消" : "Drag to choose a region. Esc cancels.";
    public static string CaptureFailed => Zh ? "没有截到图。如果刚拒绝了屏幕录制权限，请在系统设置里允许后再试。" : "Could not take the screenshot.";
    public static string AddFolderTitle => Zh ? "监视这个文件夹里的新图片" : "Watch new images saved to this folder";
}
