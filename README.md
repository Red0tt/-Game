# 薄荷庭 / FanHua

使用 Unity 2022.3.62f3c1 打开仓库根目录（包含 Assets、Packages、ProjectSettings），等待 Package Manager 和资源首次导入完成。项目使用内置渲染管线，无需安装 URP/HDRP。

从 Assets/Level/Level01.unity 开始，构建场景列表见 ProjectSettings/EditorBuildSettings.asset。材质依赖 Assets/Scenes/PaperStone.shader 及其 meta，请勿遗漏；所有脚本分部文件和 meta 同样必须保留。

Git 应提交 Assets、Packages、ProjectSettings 和设计文档；Library、Temp、Logs、IDE 工程缓存和 ArtReview 本地预览副本不需要上传。首次导入会自动生成缓存，勿从其他项目复制 Library。
