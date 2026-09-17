# 开发与贡献

## 环境

Windows 10/11 x64，.NET 10 SDK，Windows PowerShell 5.1 或更新版本。安装器由 Windows 自带的 .NET Framework C# 编译器构建，不需要额外安装 Inno Setup。首次构建会从 NuGet 下载官方运行时组件。

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

可只编译主程序：

```powershell
dotnet build src/AdvancedClipboard/AdvancedClipboard.csproj -c Release
```

有本地运行时 NuGet 包源时，可向构建脚本传入 `-OfflineRuntimeFeed <目录>`；`-PackageCache <目录>` 可指定 NuGet 缓存位置。不要上传缓存或 artifacts。

## 修改范围

保留十槽、命名、快捷键、普通剪贴板恢复、剪切确认等现有交互。修复问题应附复现步骤；只有涉及数据处理或生命周期变化时增加相应自检。不要仅为重构改动稳定的焦点和粘贴逻辑。

安装器安装到独立空文件夹；卸载仅删除安装清单记录的文件及指向该安装目录的快捷方式，不递归删除未知用户文件。卸载器会创建一个小型临时 EXE 以释放自身文件锁，系统临时目录清理时可移除它。

## 发布

提交源码、文档、许可证与构建脚本；将 `artifacts/dist/` 中的附件放入 GitHub Release，不提交二进制产物到源码仓库。版本号目前在项目、安装器和构建脚本中指定，发布新版本需一起更新。

本地自检与安装测试不等同于跨电脑兼容性测试。正式稳定版应在未安装 .NET 10 的独立 Windows 电脑验证安装、启动、复制粘贴和卸载。
