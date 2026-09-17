# 随附运行时

安装包与便携版随附 Microsoft.NETCore.App 和 Microsoft.WindowsDesktop.App Windows x64 运行时。当前本地发行包版本为 10.0.8。后续构建使用 .NET SDK 对应的运行时补丁版本，发布时应同步检查其许可与第三方声明。

dotnet-LICENSE.txt、dotnet-THIRD-PARTY-NOTICES.txt 和 windowsdesktop-LICENSE.txt 从 Microsoft 官方 NuGet 运行时包原样保留。项目的 MIT 许可不替代第三方自身许可。

安装器与卸载器使用 Windows 自带的 .NET Framework 4.x，不把该系统组件复制进发行包。
