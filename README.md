# 薄荷庭 / FanHua

使用 Unity 2022.3.62f3c1 打开仓库根目录（包含 Assets、Packages、ProjectSettings），等待 Package Manager 和资源首次导入完成。项目使用内置渲染管线，无需安装 URP/HDRP。

当前新机制试玩请打开 **Assets/Scenes/FanhuaWorldMap.unity**，或 Unity 菜单 **Fanhua > Playtest > Open World Map**，然后点 Play。庭院中 WASD 走近花瓣，Enter 进入；关卡内 Esc 返回。旧版仍可从 Assets/Level/Level01.unity 开始，构建场景列表见 ProjectSettings/EditorBuildSettings.asset。材质依赖 Assets/Scenes/PaperStone.shader 及其 meta，请勿遗漏；所有脚本分部文件和 meta 同样必须保留。

Git 应提交 Assets、Packages、ProjectSettings 和设计文档；Library、Temp、Logs、IDE 工程缓存和 ArtReview 本地预览副本不需要上传。首次导入会自动生成缓存，勿从其他项目复制 Library。

当前 prototype/WaterFireChange 分支包含水火关卡及完整配套脚本，水关卡位于 Assets/Level/Unit02/Unit02Water01.unity。Assets/Editor 中包含关卡构建和验证工具，需一并保留。ArtReview 仅用于本地预览和备份，不上传。


新九关位于 Assets/Level/Connected/Connected01…09.unity。背身走动展开翻花；展开前 Q 循环四组配色；贴近截面 E+方向推拉；右 Shift 切换；Z 撤回、R 重开。展开不可收拢，新增截面与折角合计最多10格，液化后蓝块不恢复，3D水冻结。设计目标、坐标参考解和当前边界记录于 PROJECT_PLAN.md 第8节。

这版为可试玩灰盒：程序纸褶与扇形庭院，不是最终美术。Assets/Editor 中 ValidateConnectedCampaign、ValidateFanhuaCombinations、ValidateGrayPlaytest 分别验证九关参考解、组合与预算、水/灰块和撤回；运行测试应使用工程副本，避免干扰正在打开的编辑器。
