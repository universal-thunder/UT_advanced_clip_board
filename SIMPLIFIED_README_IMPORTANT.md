#简化版《使用须知》

##1.这个剪贴板程序面向windows系统，其开发的目的是实现“多次复制/剪切”功能，避免系统自带复制粘贴功能的覆盖现象；但需要注意的是，目前这个程序仅适用于临时保存复制/剪切

件，不具备长期储存能力，复制或剪切的内容在关闭程序或关机之后会被删除；复制/剪切件的临时储存上限是10个，每个占用一个“槽”，槽的序号，就是复制/剪切件的编号；可复制/剪切

的内容较多，文本、文件夹、多个文件、网络文本都可以纳入槽中，但由于目前程序的局限性，不排除出现不可复制、“报错”的罕见情况的可能；粘贴的时候，程序自动判断粘贴件格式的合

理性，比如将文本等内容向桌面粘贴则会引发报错；

##2.这个剪贴板程序无法将它的复制/剪切/粘贴功能写入菜单，所有功能依靠快捷键来实现：

ctrl+shift+c：用于复制文件/文本等，给复制件命名，以便之后粘贴时选择，默认的命名是复制键的编号，可以改动，按enter可以直接使用默认命名，跳过这一步；

ctrl+shift+b：用于快捷查看剪贴板后台，进行删除、整理、选择等操作；

ctrl+shift+v：用于粘贴文件/文本等，按下后会出现菜单，可在其中选择粘贴件；注意：菜单标注了是否粘贴到桌面的选项，如果想粘贴到桌面最好勾选，要不然可能失败；

ctrl+shift+个位数字：用于快捷粘贴；个位数字即为粘贴键的编号，需要注意的是0代表10号；如果格式不符，快捷粘贴会失败；

##3.用户使用：

打开release，直接选择下载AdvancedClipboard-Setup-v0.1.0-beta-win-x64.exe这一安装程序，随后打开该exe程序进行安装即可；推荐部署桌面快捷方式，方便使用；

开发者可在Code板块下载，跟据LICENSE的要求进行开发与修改。

# Quick Usage Guide

## 1. Purpose and limitations

This clipboard application is designed for Windows. It lets you keep multiple copied or cut items without replacing the previous item each time.

Items are stored temporarily, not permanently. **All slots are cleared when you exit the application or shut down your computer.** This clears the stored clipboard entries; it does not delete the original files.

The application supports up to **10 slots**, with each slot holding one item or group of items. The slot number is the item’s identifier.

Supported content includes text, folders, groups of files, and text copied from websites. Some applications or content formats may not be supported and may cause errors.

Pasting also depends on whether the destination accepts the content. For example, text cannot be pasted onto the desktop as a file using this application.

## 2. Keyboard shortcuts

The application does not add commands to Windows right-click menus. Use these keyboard shortcuts:

- **Ctrl+Shift+C — Advanced Copy**  
  Copy selected content and give it a name so you can identify it later. The default name is its slot number. You can change the name or press **Enter** to accept the default.

- **Ctrl+Shift+X — Advanced Cut**  
  Cut selected content and name it in the same way as a copied item.

- **Ctrl+Shift+B — View Clipboard**  
  Open the clipboard manager to view, delete, organize, and select items.

- **Ctrl+Shift+V — Advanced Paste**  
  Open a selection menu and choose the items to paste. When pasting files or folders onto the desktop, it is recommended to check **“Paste to desktop”**; otherwise, pasting may fail.

- **Ctrl+Shift+Number — Paste a Specific Slot**  
  Paste directly from the corresponding slot. Use **1–9** for slots 1–9 and **0** for slot 10. Pasting may fail if the destination does not support the content format.

## 3. Download and installation

**For users:** Open **Releases** and download `AdvancedClipboard-Setup-v0.1.0-beta-win-x64.exe`. Run the installer and follow the instructions. Creating a desktop shortcut is recommended for convenient access.

**For developers:** Download or clone the source code from the **Code** section. Use, modify, and distribute it according to the terms of the **LICENSE** file.